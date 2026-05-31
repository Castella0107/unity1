using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Pvp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RhythmGame.Server.Data;

namespace RhythmGame.Server.Services
{
    /// <summary>
    /// PVP 試合の作成・リプレイ提出・確定・取得を司る REST エンドポイント群。
    /// 同期型 finalize: 両プレイヤー submit が揃った時点で結果計算 + レーティング更新を 1 トランザクションで行う。
    /// </summary>
    [ApiController]
    [Route("api/pvp")]
    public class PvpController : ControllerBase
    {
        private readonly ILogger<PvpController> _logger;
        private readonly AppDbContext _db;
        private readonly ActiveMatchStore _matches;
        private readonly ReplayValidationCore _validator;
        private readonly MatchmakingQueueService _queue;

        public PvpController(
            ILogger<PvpController> logger,
            AppDbContext db,
            ActiveMatchStore matches,
            ReplayValidationCore validator,
            MatchmakingQueueService queue)
        {
            _logger = logger;
            _db = db;
            _matches = matches;
            _validator = validator;
            _queue = queue;
        }

        // ── Queue ──────────────────────────────────────────────────────────────

        public class QueueRequestDto { public string UserId { get; set; } = ""; }

        public class QueueResponseDto
        {
            public string Status      { get; set; } = "";   // idle / queued / matched
            public string MatchId     { get; set; } = "";
            public string OpponentId  { get; set; } = "";
            public List<SongPickDto> Songs { get; set; } = new();
            public int    QueueDepth  { get; set; }
        }

        [HttpPost("queue/join")]
        public async Task<ActionResult<QueueResponseDto>> QueueJoin([FromBody] QueueRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            await GetOrCreateUserAsync(req.UserId);
            var s = _queue.Join(req.UserId);
            return BuildQueueDto(s);
        }

        [HttpPost("queue/leave")]
        public ActionResult<QueueResponseDto> QueueLeave([FromBody] QueueRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            var s = _queue.Leave(req.UserId);
            return BuildQueueDto(s);
        }

        [HttpGet("queue/status")]
        public ActionResult<QueueResponseDto> QueueStatus([FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest("userId required");
            var s = _queue.GetStatus(userId);
            return BuildQueueDto(s);
        }

        private static QueueResponseDto BuildQueueDto(MatchmakingQueueService.Snapshot s) => new()
        {
            Status     = s.Status.ToString().ToLowerInvariant(),
            MatchId    = s.MatchId,
            OpponentId = s.OpponentId,
            Songs      = (s.Songs ?? new List<ActiveMatchStore.SongPick>())
                          .Select(p => new SongPickDto { SongId = p.SongId, Difficulty = p.Difficulty }).ToList(),
            QueueDepth = s.QueueDepth,
        };

        // ── Create ─────────────────────────────────────────────────────────────

        public class CreateRequestDto
        {
            public string UserIdA { get; set; } = "";
            public string UserIdB { get; set; } = "";
            public string[] PoolSongIds { get; set; }   // 任意。null なら MatchPool.CreateBootstrapPool() から
        }

        public class SongPickDto
        {
            public string SongId     { get; set; } = "";
            public string Difficulty { get; set; } = "";
        }

        public class CreateResponseDto
        {
            public string                 MatchId { get; set; } = "";
            public string                 UserIdA { get; set; } = "";
            public string                 UserIdB { get; set; } = "";
            public List<SongPickDto>      Songs   { get; set; } = new();
        }

        [HttpPost("match/create")]
        public async Task<ActionResult<CreateResponseDto>> Create([FromBody] CreateRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserIdA) || string.IsNullOrEmpty(req.UserIdB))
                return BadRequest("UserIdA / UserIdB required");
            if (req.UserIdA == req.UserIdB)
                return BadRequest("UserIdA == UserIdB not allowed");

            // 両 User を UPSERT (存在しなければ作成、Glicko2 初期値で)
            await GetOrCreateUserAsync(req.UserIdA);
            await GetOrCreateUserAsync(req.UserIdB);

            // 楽曲プールから 3 曲ランダム選択
            MatchPool pool;
            if (req.PoolSongIds != null && req.PoolSongIds.Length > 0)
            {
                var entries = req.PoolSongIds.Select(s => new MatchPoolEntry(s, "extra", 10)).ToList();
                pool = new MatchPool("custom", entries);
            }
            else
            {
                pool = MatchPool.CreateBootstrapPool();
            }
            if (pool.Entries.Count < 3)
                return BadRequest($"MatchPool has only {pool.Entries.Count} songs, need >= 3");

            var rng = new Random();
            var shuffled = pool.Entries.OrderBy(_ => rng.Next()).Take(3).ToList();
            var songs = shuffled.Select(e => new ActiveMatchStore.SongPick
            {
                SongId = e.SongId, Difficulty = e.Difficulty
            }).ToList();

            var active = _matches.Create(req.UserIdA, req.UserIdB, songs);

            _logger.LogInformation("[Pvp] Match created {MatchId}: {A} vs {B}, songs={Songs}",
                active.MatchId, req.UserIdA, req.UserIdB,
                string.Join(",", songs.Select(s => s.SongId)));

            return new CreateResponseDto
            {
                MatchId = active.MatchId,
                UserIdA = active.UserIdA,
                UserIdB = active.UserIdB,
                Songs   = songs.Select(s => new SongPickDto { SongId = s.SongId, Difficulty = s.Difficulty }).ToList(),
            };
        }

        // ── Draft (PICK/BAN) ─────────────────────────────────────────────────────
        // queue 経由のマッチは曲未確定 (DraftDone=false) で始まる。クライアントが
        // pick → ban を polling で進め、両者完了で 3 曲が確定する。ブラインド方式
        // (相手の選択は両者完了まで伏せる)。サーバー追加は最小 (既存 polling と同形)。

        public class DraftActionDto
        {
            public string UserId { get; set; } = "";
            public string SongId { get; set; } = "";
        }

        public class DraftStateDto
        {
            public string             Phase      { get; set; } = "";   // pick / ban / done
            public bool               APicked    { get; set; }
            public bool               BPicked    { get; set; }
            public bool               ABanned    { get; set; }
            public bool               BBanned    { get; set; }
            public string             PickA      { get; set; } = "";   // 両PICK完了まで伏せる
            public string             PickB      { get; set; } = "";
            public List<string>       Candidates { get; set; } = new(); // 両PICK後の3曲
            public string             BanA       { get; set; } = "";   // 両BAN完了まで伏せる
            public string             BanB       { get; set; } = "";
            public List<SongPickDto>  Songs      { get; set; } = new(); // 確定3曲 (done時)
            public List<string>       Pool       { get; set; } = new(); // PICK候補=プール全曲ID
        }

        [HttpPost("match/{id}/draft/pick")]
        public ActionResult<DraftStateDto> DraftPick(string id, [FromBody] DraftActionDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId) || string.IsNullOrEmpty(req.SongId))
                return BadRequest("userId / songId required");
            var m = _matches.TryGet(id);
            if (m == null) return NotFound("match not found");
            var pool = MatchPool.CreateBootstrapPool();
            var (ok, err) = _matches.ApplyPick(m, req.UserId, req.SongId, pool.Entries);
            if (!ok) return BadRequest(err);
            return BuildDraftDto(m, pool);
        }

        [HttpPost("match/{id}/draft/ban")]
        public ActionResult<DraftStateDto> DraftBan(string id, [FromBody] DraftActionDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId) || string.IsNullOrEmpty(req.SongId))
                return BadRequest("userId / songId required");
            var m = _matches.TryGet(id);
            if (m == null) return NotFound("match not found");
            var pool = MatchPool.CreateBootstrapPool();
            var (ok, err) = _matches.ApplyBan(m, req.UserId, req.SongId, pool.Entries);
            if (!ok) return BadRequest(err);
            if (m.DraftDone)
                _logger.LogInformation("[Pvp] Draft resolved {MatchId}: songs={Songs}",
                    m.MatchId, string.Join(",", m.Songs.Select(s => s.SongId)));
            return BuildDraftDto(m, pool);
        }

        [HttpGet("match/{id}/draft")]
        public ActionResult<DraftStateDto> DraftGet(string id)
        {
            var m = _matches.TryGet(id);
            if (m == null) return NotFound("match not found");
            return BuildDraftDto(m, MatchPool.CreateBootstrapPool());
        }

        private static DraftStateDto BuildDraftDto(ActiveMatchStore.ActiveMatch m, MatchPool pool)
        {
            var phase = ActiveMatchStore.GetPhase(m);
            bool bothPicked = !string.IsNullOrEmpty(m.PickA) && !string.IsNullOrEmpty(m.PickB);
            bool bothBanned = !string.IsNullOrEmpty(m.BanA) && !string.IsNullOrEmpty(m.BanB);
            return new DraftStateDto
            {
                Phase      = phase.ToString().ToLowerInvariant(),
                APicked    = !string.IsNullOrEmpty(m.PickA),
                BPicked    = !string.IsNullOrEmpty(m.PickB),
                ABanned    = !string.IsNullOrEmpty(m.BanA),
                BBanned    = !string.IsNullOrEmpty(m.BanB),
                PickA      = bothPicked ? m.PickA : "",   // ブラインド: 両者完了まで伏せる
                PickB      = bothPicked ? m.PickB : "",
                Candidates = new List<string>(m.BanCandidates),
                BanA       = bothBanned ? m.BanA : "",
                BanB       = bothBanned ? m.BanB : "",
                Songs      = m.Songs.Select(s => new SongPickDto { SongId = s.SongId, Difficulty = s.Difficulty }).ToList(),
                Pool       = pool.Entries.Select(e => e.SongId).ToList(),
            };
        }

        // ── Submit ─────────────────────────────────────────────────────────────

        public class SubmitSongDto
        {
            public string SongId           { get; set; } = "";
            public string ReplayDataBase64 { get; set; } = "";
        }

        public class SubmitRequestDto
        {
            public string             UserId { get; set; } = "";
            public List<SubmitSongDto> Songs { get; set; } = new();
        }

        public class SubmitResponseDto
        {
            public bool   Accepted        { get; set; }
            public string Error           { get; set; } = "";
            public bool   MatchFinalized  { get; set; }
            public MatchResultDto Result  { get; set; }    // Finalized 時のみ非 null
        }

        [HttpPost("match/{matchId}/submit")]
        public async Task<ActionResult<SubmitResponseDto>> Submit(string matchId, [FromBody] SubmitRequestDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId))
                return BadRequest("userId required");

            var m = _matches.TryGet(matchId);
            if (m == null) return NotFound(new SubmitResponseDto { Accepted = false, Error = "match not found or already finalized" });
            if (m.Finalized) return new SubmitResponseDto { Accepted = false, Error = "match already finalized" };

            bool isA = req.UserId == m.UserIdA;
            bool isB = req.UserId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            var mySub = isA ? m.SubmissionA : m.SubmissionB;
            if (mySub.Submitted) return new SubmitResponseDto { Accepted = false, Error = "already submitted" };

            if (req.Songs == null || req.Songs.Count != m.Songs.Count)
                return BadRequest($"songs.Count must be {m.Songs.Count}");

            // 各曲を順番に検証 (順序はマッチ作成時の m.Songs 順に合わせる必要がある)
            var sectorScores   = new int[m.Songs.Count][];
            var sectorTieBreaks = new int[m.Songs.Count][];
            for (int i = 0; i < m.Songs.Count; i++)
            {
                var expected = m.Songs[i];
                var actual   = req.Songs[i];
                if (actual.SongId != expected.SongId)
                {
                    return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] mismatch: expected={expected.SongId}, got={actual.SongId}" });
                }

                byte[] bytes;
                try { bytes = Convert.FromBase64String(actual.ReplayDataBase64 ?? ""); }
                catch { return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] base64 decode failed" }); }

                // chartHash は replay 内部に格納されている → デコード後に取得 + 検証
                ReplayData replay;
                try { replay = ReplayDecoder.Decode(bytes); }
                catch (Exception ex) { return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] decode: {ex.Message}" }); }

                var chartHash = Convert.ToHexString(replay.Metadata.ChartHash ?? Array.Empty<byte>());
                var vr = await _validator.ValidateAsync(chartHash, bytes);
                if (!vr.Ok)
                {
                    return BadRequest(new SubmitResponseDto { Accepted = false, Error = $"songs[{i}] validate: {vr.Error}" });
                }

                // sector scores 5 件を取り出す (足りない場合は 0 詰め)
                var s = vr.Snapshot.SectorScores ?? new int[5];
                var copy = new int[5];
                for (int k = 0; k < 5 && k < s.Length; k++) copy[k] = s[k];
                sectorScores[i]    = copy;
                sectorTieBreaks[i] = ExtractTieBreaks(vr.Snapshot);
            }

            mySub.Submitted       = true;
            mySub.SectorScores    = sectorScores;
            mySub.SectorTieBreaks = sectorTieBreaks;

            // 両者揃ったら finalize
            if (m.SubmissionA.Submitted && m.SubmissionB.Submitted)
            {
                var result = await FinalizeMatchAsync(m);
                return new SubmitResponseDto { Accepted = true, MatchFinalized = true, Result = result };
            }
            return new SubmitResponseDto { Accepted = true, MatchFinalized = false };
        }

        // ── Per-song submit (完全同期: 曲ごと提出 + 開示 + 8pt クリンチ) ──────────
        // 想定契約。K のサーバー完全同期実装と統合予定 (additive・既存 /submit は不変)。
        // 各曲プレイ直後に submit し、相手も同曲を提出済みなら相手のセクタースコアを開示する。
        // 2 曲目(index 1)以降で累計が 8.0pt 以上に達した側がいれば早期決着 (3 曲目を省略)。

        public class SongSubmitDto
        {
            public string UserId           { get; set; } = "";
            public int    SongIndex        { get; set; }
            public string SongId           { get; set; } = "";
            public string Difficulty       { get; set; } = "";
            public string ReplayDataBase64 { get; set; } = "";
        }

        public class SongResultDto
        {
            public int    SongIndex      { get; set; }
            public bool   BothSubmitted  { get; set; }
            public List<int> SelfSectors { get; set; } = new();   // 提出者の 5 セクター (常に返す)
            public List<int> OppSectors  { get; set; } = new();   // 相手の 5 セクター (両者提出時のみ)
            public List<int> SelfSectorTieBreaks { get; set; } = new();  // 提出者の 5 セクターのタイブレーク値 (同点表示解決用)
            public List<int> OppSectorTieBreaks  { get; set; } = new();  // 相手の 5 セクターのタイブレーク値 (両者提出時のみ)
            public double SelfSongPoints { get; set; }            // この曲の自分pt (難易度倍率込み, 両者提出時)
            public double OppSongPoints  { get; set; }
            public double SelfCumulative { get; set; }            // 提出済み曲の累計 (両者提出時)
            public double OppCumulative  { get; set; }
            public bool   Clinch         { get; set; }            // 早期決着が発生したか
            public bool   MatchOver      { get; set; }            // clinch または全曲完了
            public MatchResultDto Result { get; set; }            // MatchOver 時のみ非 null
        }

        [HttpPost("match/{matchId}/song/submit")]
        public async Task<ActionResult<SongResultDto>> SongSubmit(string matchId, [FromBody] SongSubmitDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            var m = _matches.TryGet(matchId);
            if (m == null) return NotFound("match not found or already finalized");
            if (m.Finalized) return BadRequest("match already finalized");
            if (!m.DraftDone) return BadRequest("draft not done");

            bool isA = req.UserId == m.UserIdA;
            bool isB = req.UserId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            int idx = req.SongIndex;
            if (idx < 0 || idx >= m.Songs.Count) return BadRequest($"songIndex out of range (0..{m.Songs.Count - 1})");
            if (req.SongId != m.Songs[idx].SongId)
                return BadRequest($"songId mismatch: expected={m.Songs[idx].SongId}, got={req.SongId}");

            ActiveMatchStore.EnsurePerSong(m);
            var mine    = isA ? m.PerSongScoresA    : m.PerSongScoresB;
            var mineTie = isA ? m.PerSongTieBreaksA : m.PerSongTieBreaksB;
            if (mine[idx] != null) return BadRequest("song already submitted");

            // リプレイ検証 → セクタースコア抽出
            byte[] bytes;
            try { bytes = Convert.FromBase64String(req.ReplayDataBase64 ?? ""); }
            catch { return BadRequest("base64 decode failed"); }
            ReplayData replay;
            try { replay = ReplayDecoder.Decode(bytes); }
            catch (Exception ex) { return BadRequest("decode: " + ex.Message); }
            var chartHash = Convert.ToHexString(replay.Metadata.ChartHash ?? Array.Empty<byte>());
            var vr = await _validator.ValidateAsync(chartHash, bytes);
            if (!vr.Ok) return BadRequest("validate: " + vr.Error);

            var s = vr.Snapshot.SectorScores ?? new int[5];
            var copy = new int[5];
            for (int k = 0; k < 5 && k < s.Length; k++) copy[k] = s[k];
            mine[idx]    = copy;
            mineTie[idx] = ExtractTieBreaks(vr.Snapshot);

            // 各自が選んだ難易度を反映 (倍率計算に効く)。空でなければ採用。
            if (!string.IsNullOrEmpty(req.Difficulty))
                m.Songs[idx].Difficulty = req.Difficulty;

            var dto = new SongResultDto { SongIndex = idx };
            dto.SelfSectors = new List<int>(copy);
            dto.SelfSectorTieBreaks = new List<int>(mineTie[idx]);

            if (!ActiveMatchStore.BothSubmittedSong(m, idx))
            {
                dto.BothSubmitted = false;
                return dto;   // 相手待ち (ブラインド)
            }

            // 両者提出 → この曲の結果 + 累計を集計して開示
            dto.BothSubmitted = true;
            var selfArr = (isA ? m.PerSongScoresA : m.PerSongScoresB)[idx];
            var oppArr  = (isA ? m.PerSongScoresB : m.PerSongScoresA)[idx];
            dto.SelfSectors = new List<int>(selfArr);
            dto.OppSectors  = new List<int>(oppArr);
            dto.SelfSectorTieBreaks = new List<int>((isA ? m.PerSongTieBreaksA : m.PerSongTieBreaksB)[idx] ?? new int[5]);
            dto.OppSectorTieBreaks  = new List<int>((isA ? m.PerSongTieBreaksB : m.PerSongTieBreaksA)[idx] ?? new int[5]);

            // この曲のポイント (難易度倍率込み)
            var songPairs = new List<SectorPair>(5);
            for (int sec = 0; sec < 5; sec++)
                songPairs.Add(new SectorPair(m.Songs[idx].SongId, sec,
                    m.PerSongScoresA[idx][sec], m.PerSongScoresB[idx][sec], m.Songs[idx].Difficulty,
                    Tie(m.PerSongTieBreaksA, idx, sec), Tie(m.PerSongTieBreaksB, idx, sec)));
            var songOutcome = MatchScoring.Score(songPairs);
            dto.SelfSongPoints = isA ? songOutcome.TotalPointsA : songOutcome.TotalPointsB;
            dto.OppSongPoints  = isA ? songOutcome.TotalPointsB : songOutcome.TotalPointsA;

            // 両者提出済みの全曲で累計を集計
            var (cumA, cumB, bothCount) = AccumulateBothSubmitted(m);
            dto.SelfCumulative = isA ? cumA : cumB;
            dto.OppCumulative  = isA ? cumB : cumA;

            // 8pt クリンチ判定: 2 曲目(index>=1)以降で、残り曲を全勝しても逆転不能なら早期決着。
            // 簡易仕様: どちらかの累計が 8.0 以上に到達したら決着 (15pt 満点・過半数)。
            bool lastSong = idx == m.Songs.Count - 1;
            bool clinch   = idx >= 1 && (cumA >= 8.0 || cumB >= 8.0);
            dto.Clinch    = clinch;
            dto.MatchOver = clinch || (lastSong && bothCount == m.Songs.Count);

            if (dto.MatchOver)
            {
                m.ClinchedAfterSongIndex = clinch && !lastSong ? idx : -1;
                dto.Result = await FinalizePerSongAsync(m, bothCount);
            }
            return dto;
        }

        [HttpGet("match/{matchId}/song/{songIndex}/result")]
        public async Task<ActionResult<SongResultDto>> SongResultGet(string matchId, int songIndex, [FromQuery] string userId)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest("userId required");
            var m = _matches.TryGet(matchId);
            if (m == null)
            {
                // ストアに無い → finalize 済みかも。DB から最終結果を返す (matchOver)。
                var saved = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId);
                if (saved == null) return NotFound("match not found");
                return new SongResultDto { SongIndex = songIndex, BothSubmitted = true, MatchOver = true, Result = BuildResultDto(saved) };
            }

            bool isA = userId == m.UserIdA;
            bool isB = userId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            int idx = songIndex;
            var dto = new SongResultDto { SongIndex = idx };
            if (m.PerSongScoresA != null && idx >= 0 && idx < m.PerSongScoresA.Length)
            {
                var mineArr = (isA ? m.PerSongScoresA : m.PerSongScoresB)[idx];
                if (mineArr != null) dto.SelfSectors = new List<int>(mineArr);
                var mineTieArr = (isA ? m.PerSongTieBreaksA : m.PerSongTieBreaksB)?[idx];
                if (mineTieArr != null) dto.SelfSectorTieBreaks = new List<int>(mineTieArr);
            }

            if (!ActiveMatchStore.BothSubmittedSong(m, idx)) { dto.BothSubmitted = false; return dto; }

            dto.BothSubmitted = true;
            dto.SelfSectors = new List<int>((isA ? m.PerSongScoresA : m.PerSongScoresB)[idx]);
            dto.OppSectors  = new List<int>((isA ? m.PerSongScoresB : m.PerSongScoresA)[idx]);
            dto.SelfSectorTieBreaks = new List<int>((isA ? m.PerSongTieBreaksA : m.PerSongTieBreaksB)[idx] ?? new int[5]);
            dto.OppSectorTieBreaks  = new List<int>((isA ? m.PerSongTieBreaksB : m.PerSongTieBreaksA)[idx] ?? new int[5]);

            var songPairs = new List<SectorPair>(5);
            for (int sec = 0; sec < 5; sec++)
                songPairs.Add(new SectorPair(m.Songs[idx].SongId, sec,
                    m.PerSongScoresA[idx][sec], m.PerSongScoresB[idx][sec], m.Songs[idx].Difficulty,
                    Tie(m.PerSongTieBreaksA, idx, sec), Tie(m.PerSongTieBreaksB, idx, sec)));
            var so = MatchScoring.Score(songPairs);
            dto.SelfSongPoints = isA ? so.TotalPointsA : so.TotalPointsB;
            dto.OppSongPoints  = isA ? so.TotalPointsB : so.TotalPointsA;

            var (cumA, cumB, _) = AccumulateBothSubmitted(m);
            dto.SelfCumulative = isA ? cumA : cumB;
            dto.OppCumulative  = isA ? cumB : cumA;

            if (m.Finalized)
            {
                dto.MatchOver = true;
                dto.Clinch    = m.ClinchedAfterSongIndex >= 0;
                var saved = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId);
                if (saved != null) dto.Result = BuildResultDto(saved);
            }
            return dto;
        }

        // 両者提出済みの曲だけで A/B 累計ポイントを集計する。
        private static (double a, double b, int count) AccumulateBothSubmitted(ActiveMatchStore.ActiveMatch m)
        {
            var pairs = new List<SectorPair>();
            int count = 0;
            for (int i = 0; i < m.Songs.Count; i++)
            {
                if (!ActiveMatchStore.BothSubmittedSong(m, i)) continue;
                count++;
                for (int sec = 0; sec < 5; sec++)
                    pairs.Add(new SectorPair(m.Songs[i].SongId, sec,
                        m.PerSongScoresA[i][sec], m.PerSongScoresB[i][sec], m.Songs[i].Difficulty,
                        Tie(m.PerSongTieBreaksA, i, sec), Tie(m.PerSongTieBreaksB, i, sec)));
            }
            var o = MatchScoring.Score(pairs);
            return (o.TotalPointsA, o.TotalPointsB, count);
        }

        // スナップショットから 5 セクターのタイブレーク値 (Σ 2×P+ + P) を 0 詰めで取り出す。
        private static int[] ExtractTieBreaks(PlayProgressSnapshot snap)
        {
            var src  = snap?.SectorTieBreaks ?? new int[5];
            var copy = new int[5];
            for (int k = 0; k < 5 && k < src.Length; k++) copy[k] = src[k];
            return copy;
        }

        // tiebreak[song][sector] を null 安全に取得 (未保存/旧データは 0 = 引分のまま)。
        private static int Tie(int[][] arr, int song, int sector)
        {
            if (arr == null || song < 0 || song >= arr.Length) return 0;
            var row = arr[song];
            if (row == null || sector < 0 || sector >= row.Length) return 0;
            return row[sector];
        }

        // ── Progress (in-match real-time) ──────────────────────────────────────

        public class ProgressUpdateDto
        {
            public string UserId       { get; set; } = "";
            public int    SongIndex    { get; set; }
            public int    PercentX1000 { get; set; }
            public int    Score        { get; set; }
        }

        public class ProgressSideDto
        {
            public string UserId          { get; set; } = "";
            public int    SongIndex       { get; set; }
            public int    PercentX1000    { get; set; }
            public int    Score           { get; set; }
            public long   UpdatedAtUnixMs { get; set; }
        }

        public class ProgressSnapshotDto
        {
            public string          MatchId { get; set; } = "";
            public ProgressSideDto A       { get; set; } = new();
            public ProgressSideDto B       { get; set; } = new();
            public bool            Finalized { get; set; }
        }

        [HttpPost("match/{matchId}/progress")]
        public ActionResult<ProgressSnapshotDto> PostProgress(string matchId, [FromBody] ProgressUpdateDto req)
        {
            if (req == null || string.IsNullOrEmpty(req.UserId)) return BadRequest("userId required");
            var m = _matches.TryGet(matchId);
            if (m == null) return NotFound();
            bool isA = req.UserId == m.UserIdA;
            bool isB = req.UserId == m.UserIdB;
            if (!isA && !isB) return Forbid();

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var p = isA ? m.ProgressA : m.ProgressB;
            p.SongIndex       = req.SongIndex;
            p.PercentX1000    = System.Math.Max(0, System.Math.Min(100000, req.PercentX1000));
            p.Score           = req.Score;
            p.UpdatedAtUnixMs = nowMs;

            return BuildProgressSnapshot(m);
        }

        [HttpGet("match/{matchId}/progress")]
        public ActionResult<ProgressSnapshotDto> GetProgress(string matchId)
        {
            var m = _matches.TryGet(matchId);
            if (m != null) return BuildProgressSnapshot(m);
            // 既に finalize 済みなら 200 + Finalized=true (クライアント側 polling を止めるシグナル)
            return new ProgressSnapshotDto { MatchId = matchId, Finalized = true };
        }

        private static ProgressSnapshotDto BuildProgressSnapshot(ActiveMatchStore.ActiveMatch m)
        {
            return new ProgressSnapshotDto
            {
                MatchId = m.MatchId,
                Finalized = m.Finalized,
                A = new ProgressSideDto
                {
                    UserId          = m.UserIdA,
                    SongIndex       = m.ProgressA.SongIndex,
                    PercentX1000    = m.ProgressA.PercentX1000,
                    Score           = m.ProgressA.Score,
                    UpdatedAtUnixMs = m.ProgressA.UpdatedAtUnixMs,
                },
                B = new ProgressSideDto
                {
                    UserId          = m.UserIdB,
                    SongIndex       = m.ProgressB.SongIndex,
                    PercentX1000    = m.ProgressB.PercentX1000,
                    Score           = m.ProgressB.Score,
                    UpdatedAtUnixMs = m.ProgressB.UpdatedAtUnixMs,
                },
            };
        }

        // ── User PVP stats (ロビー用) ────────────────────────────────────────────
        // ロビー右パネルの TOTAL MATCH / MATCH WIN / WIN RATIO を埋める実データ。
        // ティア/LP/シーズン/ラダー順位/難易度別スタッツは K のレーティング設計ドメインのため
        // ここでは返さない (クライアントは UNRANKED / -- のプレースホルダー表示)。

        public class UserPvpStatsDto
        {
            public string UserId      { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public int    TotalMatches{ get; set; }
            public int    Wins        { get; set; }
            public int    Losses      { get; set; }
            public int    Draws       { get; set; }
            public double WinRatio    { get; set; }   // 0..1
            public double Rating      { get; set; }   // Glicko-2 raw (ティア変換は K 側)
        }

        [HttpGet("user/{userId}/stats")]
        public async Task<ActionResult<UserPvpStatsDto>> UserStats(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return BadRequest("userId required");
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            if (u == null)
            {
                // 未登録ユーザーは 0 戦績で返す (初回ロビー表示)。
                return new UserPvpStatsDto { UserId = userId, DisplayName = userId };
            }
            return new UserPvpStatsDto
            {
                UserId       = u.UserId,
                DisplayName  = u.DisplayName,
                TotalMatches = u.TotalPvpMatches,
                Wins         = u.PvpWins,
                Losses       = u.PvpLosses,
                Draws        = u.PvpDraws,
                WinRatio     = u.TotalPvpMatches > 0 ? (double)u.PvpWins / u.TotalPvpMatches : 0.0,
                Rating       = u.Rating,
            };
        }

        // ── Get ────────────────────────────────────────────────────────────────

        public class MatchResultDto
        {
            public string  MatchId          { get; set; } = "";
            public string  UserIdA          { get; set; } = "";
            public string  UserIdB          { get; set; } = "";
            public List<SongPickDto> Songs  { get; set; } = new();
            public List<int> SectorScoresA  { get; set; } = new();   // 15 件
            public List<int> SectorScoresB  { get; set; } = new();
            public double TotalPointsA      { get; set; }
            public double TotalPointsB      { get; set; }
            public int    OutcomeKind       { get; set; }            // 0=Draw, 1=AWins, 2=BWins
            public double RatingABefore     { get; set; }
            public double RatingAAfter      { get; set; }
            public double RatingBBefore     { get; set; }
            public double RatingBAfter      { get; set; }
            public long   CompletedAtUnixMs { get; set; }
        }

        [HttpGet("match/{matchId}")]
        public async Task<ActionResult<MatchResultDto>> Get(string matchId)
        {
            // 確定済みなら DB を見る
            var saved = await _db.Matches.AsNoTracking().FirstOrDefaultAsync(x => x.MatchId == matchId);
            if (saved != null) return BuildResultDto(saved);

            // 進行中なら ActiveMatchStore (snapshot 用に部分情報のみ)
            var active = _matches.TryGet(matchId);
            if (active == null) return NotFound();

            return new MatchResultDto
            {
                MatchId = active.MatchId,
                UserIdA = active.UserIdA,
                UserIdB = active.UserIdB,
                Songs   = active.Songs.Select(s => new SongPickDto { SongId = s.SongId, Difficulty = s.Difficulty }).ToList(),
                OutcomeKind = -1,   // 未確定
            };
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private async Task<MatchResultDto> FinalizeMatchAsync(ActiveMatchStore.ActiveMatch m)
        {
            // SectorPair[15] を構築 (3 songs × 5 sectors)
            var pairs = new List<SectorPair>(15);
            for (int songIdx = 0; songIdx < m.Songs.Count; songIdx++)
            {
                var sA = m.SubmissionA.SectorScores[songIdx];
                var sB = m.SubmissionB.SectorScores[songIdx];
                for (int sec = 0; sec < 5; sec++)
                {
                    pairs.Add(new SectorPair(m.Songs[songIdx].SongId, sec, sA[sec], sB[sec],
                                             m.Songs[songIdx].Difficulty,
                                             Tie(m.SubmissionA.SectorTieBreaks, songIdx, sec),
                                             Tie(m.SubmissionB.SectorTieBreaks, songIdx, sec)));
                }
            }

            var flatA = m.SubmissionA.SectorScores.SelectMany(a => a);
            var flatB = m.SubmissionB.SectorScores.SelectMany(a => a);
            return await FinalizeCoreAsync(m, MatchScoring.Score(pairs), m.Songs, flatA, flatB, removeFromStore: true);
        }

        // 完全同期 (曲ごと提出) の finalize。両者提出済みの曲だけを採点して確定する
        // (8pt クリンチで 2 曲で終わる場合も対応)。
        private async Task<MatchResultDto> FinalizePerSongAsync(ActiveMatchStore.ActiveMatch m, int bothCount)
        {
            var pairs       = new List<SectorPair>(bothCount * 5);
            var playedSongs = new List<ActiveMatchStore.SongPick>(bothCount);
            var flatA       = new List<int>(bothCount * 5);
            var flatB       = new List<int>(bothCount * 5);
            for (int i = 0; i < m.Songs.Count; i++)
            {
                if (!ActiveMatchStore.BothSubmittedSong(m, i)) continue;
                playedSongs.Add(m.Songs[i]);
                for (int sec = 0; sec < 5; sec++)
                {
                    pairs.Add(new SectorPair(m.Songs[i].SongId, sec,
                        m.PerSongScoresA[i][sec], m.PerSongScoresB[i][sec], m.Songs[i].Difficulty,
                        Tie(m.PerSongTieBreaksA, i, sec), Tie(m.PerSongTieBreaksB, i, sec)));
                    flatA.Add(m.PerSongScoresA[i][sec]);
                    flatB.Add(m.PerSongScoresB[i][sec]);
                }
            }
            // per-song パスはストアに残す (相手側が GET で最終結果を取得できるように)。
            return await FinalizeCoreAsync(m, MatchScoring.Score(pairs), playedSongs, flatA, flatB, removeFromStore: false);
        }

        // 採点結果からレーティング更新 + MatchEntity 永続化 + ストア除去を行う共通コア。
        // playedSongs / flatA / flatB は実際に採点した曲のみ (クリンチ時は 2 曲)。
        // removeFromStore=false の場合は Finalized=true でストアに残す (per-song の相手 GET 用)。
        private async Task<MatchResultDto> FinalizeCoreAsync(
            ActiveMatchStore.ActiveMatch m, MatchOutcome outcome,
            List<ActiveMatchStore.SongPick> playedSongs, IEnumerable<int> flatA, IEnumerable<int> flatB,
            bool removeFromStore)
        {
            // 両ユーザーの BEFORE 状態でレーティング更新
            var userA = await _db.Users.FindAsync(m.UserIdA);
            var userB = await _db.Users.FindAsync(m.UserIdB);
            if (userA == null) userA = await GetOrCreateUserAsync(m.UserIdA);
            if (userB == null) userB = await GetOrCreateUserAsync(m.UserIdB);

            var pA = new Glicko2Player(userA.Rating, userA.RatingDeviation, userA.Volatility);
            var pB = new Glicko2Player(userB.Rating, userB.RatingDeviation, userB.Volatility);

            var newA = Glicko2Calculator.Update(pA, outcome.ToGlicko2ResultsForA(pB.Rating, pB.RatingDeviation).ToList());
            var newB = Glicko2Calculator.Update(pB, outcome.ToGlicko2ResultsForB(pA.Rating, pA.RatingDeviation).ToList());

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var ratingABefore = userA.Rating;
            var ratingBBefore = userB.Rating;
            userA.Rating          = newA.Rating;
            userA.RatingDeviation = newA.RatingDeviation;
            userA.Volatility      = newA.Volatility;
            userA.LastRatedAtUnixMs = nowMs;
            userA.TotalPvpMatches++;
            userB.Rating          = newB.Rating;
            userB.RatingDeviation = newB.RatingDeviation;
            userB.Volatility      = newB.Volatility;
            userB.LastRatedAtUnixMs = nowMs;
            userB.TotalPvpMatches++;

            switch (outcome.Kind)
            {
                case MatchOutcomeKind.AWins: userA.PvpWins++;   userB.PvpLosses++; break;
                case MatchOutcomeKind.BWins: userA.PvpLosses++; userB.PvpWins++;   break;
                case MatchOutcomeKind.Draw:  userA.PvpDraws++;  userB.PvpDraws++;  break;
            }

            // MatchEntity 保存
            var entity = new MatchEntity
            {
                MatchId          = m.MatchId,
                UserIdA          = m.UserIdA,
                UserIdB          = m.UserIdB,
                SongIdsCsv       = string.Join(",", playedSongs.Select(s => s.SongId)),
                DifficultiesCsv  = string.Join(",", playedSongs.Select(s => s.Difficulty)),
                SectorScoresA    = string.Join(",", flatA),
                SectorScoresB    = string.Join(",", flatB),
                TotalPointsAx1000 = (int)Math.Round(outcome.TotalPointsA * 1000),
                TotalPointsBx1000 = (int)Math.Round(outcome.TotalPointsB * 1000),
                OutcomeKind      = (int)outcome.Kind,
                CreatedAtUnixMs  = m.CreatedAtUnixMs,
                CompletedAtUnixMs = nowMs,
                RatingABefore    = ratingABefore,
                RatingAAfter     = newA.Rating,
                RatingBBefore    = ratingBBefore,
                RatingBAfter     = newB.Rating,
            };
            _db.Matches.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Pvp] Finalized {MatchId}: {A}({Ar:F1}→{Ar2:F1}) vs {B}({Br:F1}→{Br2:F1}), {Pa}-{Pb}",
                m.MatchId, m.UserIdA, ratingABefore, newA.Rating, m.UserIdB, ratingBBefore, newB.Rating,
                outcome.TotalPointsA, outcome.TotalPointsB);

            m.Finalized        = true;
            m.CompletedMatchId = m.MatchId;
            if (removeFromStore) _matches.Remove(m.MatchId);

            return BuildResultDto(entity);
        }

        private async Task<UserEntity> GetOrCreateUserAsync(string userId)
        {
            var u = await _db.Users.FindAsync(userId);
            if (u == null)
            {
                u = new UserEntity
                {
                    UserId            = userId,
                    DisplayName       = userId,
                    FirstSeenUnixMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    LastSeenUnixMs    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Rating            = 1500.0,
                    RatingDeviation   = 350.0,
                    Volatility        = 0.06,
                };
                _db.Users.Add(u);
                await _db.SaveChangesAsync();
            }
            return u;
        }

        private static MatchResultDto BuildResultDto(MatchEntity e)
        {
            var songIds = (e.SongIdsCsv ?? "").Split(',');
            var diffs   = (e.DifficultiesCsv ?? "").Split(',');
            var songs   = new List<SongPickDto>();
            for (int i = 0; i < songIds.Length; i++)
            {
                songs.Add(new SongPickDto
                {
                    SongId     = songIds[i],
                    Difficulty = i < diffs.Length ? diffs[i] : "extra",
                });
            }
            return new MatchResultDto
            {
                MatchId       = e.MatchId,
                UserIdA       = e.UserIdA,
                UserIdB       = e.UserIdB,
                Songs         = songs,
                SectorScoresA = (e.SectorScoresA ?? "").Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(int.Parse).ToList(),
                SectorScoresB = (e.SectorScoresB ?? "").Split(',').Where(s => !string.IsNullOrEmpty(s)).Select(int.Parse).ToList(),
                TotalPointsA  = e.TotalPointsAx1000 / 1000.0,
                TotalPointsB  = e.TotalPointsBx1000 / 1000.0,
                OutcomeKind   = e.OutcomeKind,
                RatingABefore = e.RatingABefore,
                RatingAAfter  = e.RatingAAfter,
                RatingBBefore = e.RatingBBefore,
                RatingBAfter  = e.RatingBAfter,
                CompletedAtUnixMs = e.CompletedAtUnixMs,
            };
        }
    }
}
