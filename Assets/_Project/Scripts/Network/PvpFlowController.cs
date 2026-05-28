using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RhythmGame.Network
{
    /// <summary>
    /// PVP マッチの 3 曲連戦を統括する常駐コーディネータ。
    /// シーンを跨いで状態を保持し、各曲完走時に蓄積、3 曲終わったら一括サーバー送信 → PvpMatchEnd へ。
    ///
    /// 設計:
    ///   - NetworkClient と同じく `[RuntimeInitializeOnLoadMethod]` で自動 spawn
    ///   - 試合中は IsActive=true、曲完了通知は GamePlayController から OnSongCompleted で受ける
    ///   - 全曲完了 → SubmitMatchAsync → PvpMatchEndParameters で SceneRouter.GoTo(PvpMatchEnd)
    /// </summary>
    public class PvpFlowController : MonoBehaviour
    {
        /// <summary>シングルトンインスタンス。</summary>
        public static PvpFlowController Instance { get; private set; }

        /// <summary>PVP マッチ進行中か。</summary>
        public bool   IsActive       { get; private set; }
        /// <summary>現在のマッチID。</summary>
        public string MatchId        { get; private set; }
        /// <summary>対戦相手のユーザーID。</summary>
        public string OpponentId     { get; private set; }
        /// <summary>自分のユーザーID。</summary>
        public string SelfUserId     { get; private set; }
        /// <summary>現在プレイ中の曲インデックス(0 始まり)。</summary>
        public int    CurrentSongIndex { get; private set; }
        /// <summary>このマッチの選曲一覧。</summary>
        public IReadOnlyList<SongPickDto> Songs => _songs;

        List<SongPickDto> _songs = new();
        List<string>      _replayPaths = new();
        bool              _submitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("PvpFlowController (auto)");
            go.AddComponent<PvpFlowController>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>マッチング後、Matchmaking シーンから呼ばれる。1 曲目の GamePlay を起動する。</summary>
        public void StartMatch(string matchId, string opponentId, List<SongPickDto> songs)
        {
            if (IsActive)
            {
                Debug.LogWarning("[PvpFlow] StartMatch called while already active — overwriting state");
            }
            MatchId    = matchId;
            OpponentId = opponentId;
            SelfUserId = LocalIdentity.UserId;
            _songs     = songs ?? new List<SongPickDto>();
            _replayPaths.Clear();
            CurrentSongIndex = 0;
            IsActive    = true;
            _submitting = false;

            Debug.Log($"[PvpFlow] StartMatch {matchId.Substring(0, 8)} vs {opponentId}, songs={_songs.Count}");
            LaunchCurrentSong();
        }

        /// <summary>GamePlayController から完走時に呼ばれる (PVP モード時のみ)。</summary>
        public void OnSongCompleted(string songId, string replayPath)
        {
            if (!IsActive) return;
            if (string.IsNullOrEmpty(replayPath))
            {
                Debug.LogWarning("[PvpFlow] OnSongCompleted with empty replayPath — aborting match");
                AbortMatch("Replay missing for song " + (CurrentSongIndex + 1));
                return;
            }
            _replayPaths.Add(replayPath);
            Debug.Log($"[PvpFlow] Song {CurrentSongIndex + 1}/{_songs.Count} completed: {songId} → {Path.GetFileName(replayPath)}");

            CurrentSongIndex++;
            if (CurrentSongIndex < _songs.Count)
            {
                LaunchCurrentSong();
            }
            else
            {
                _ = SubmitAndFinish();
            }
        }

        /// <summary>明示キャンセル (Pause メニュー等から)。途中棄権扱い。</summary>
        public void AbortMatch(string reason)
        {
            Debug.LogWarning("[PvpFlow] AbortMatch: " + reason);
            var p = new PvpMatchEndParameters
            {
                MatchId      = MatchId,
                SelfUserId   = SelfUserId,
                ErrorMessage = reason,
            };
            ResetState();
            if (SceneRouter.Instance != null)
                SceneRouter.Instance.GoTo(SceneId.PVPMatchEnd, p, TransitionStyle.FadeBlack);
        }

        // ── Internal ────────────────────────────────────────────────────────────

        void LaunchCurrentSong()
        {
            if (CurrentSongIndex < 0 || CurrentSongIndex >= _songs.Count) return;
            var s = _songs[CurrentSongIndex];

            var gp = new GamePlayParameters
            {
                SongId        = s.songId,
                Difficulty    = string.IsNullOrEmpty(s.difficulty) ? "extra" : s.difficulty,
                HiSpeed       = PlayerPrefs.GetFloat("HiSpeed", 4.5f),
                JudgeOffset   = 0,
                VisualOffset  = 0,
                Modifier      = "None",
                IsReplay      = false,
                IsPvp         = true,
                PvpMatchId    = MatchId,
                PvpSongIndex  = CurrentSongIndex,
                PvpOpponentId = OpponentId,
            };

            if (SceneRouter.Instance != null)
            {
                SceneRouter.Instance.GoTo(SceneId.GamePlay, gp, TransitionStyle.GameStart);
            }
            else
            {
                Debug.LogError("[PvpFlow] SceneRouter.Instance null — cannot launch PVP song");
            }
        }

        async Task SubmitAndFinish()
        {
            if (_submitting) return;
            _submitting = true;

            Debug.Log($"[PvpFlow] All {_songs.Count} songs played, submitting to server");

            try
            {
                var net = NetworkClient.Instance;
                if (net == null)
                {
                    AbortMatch("NetworkClient not available — cannot submit match");
                    return;
                }

                var songs = new List<SubmitMatchSongDto>(_songs.Count);
                for (int i = 0; i < _songs.Count && i < _replayPaths.Count; i++)
                {
                    if (!File.Exists(_replayPaths[i]))
                    {
                        AbortMatch("Replay file missing: " + _replayPaths[i]);
                        return;
                    }
                    byte[] bytes = File.ReadAllBytes(_replayPaths[i]);
                    songs.Add(new SubmitMatchSongDto
                    {
                        songId           = _songs[i].songId,
                        replayDataBase64 = Convert.ToBase64String(bytes),
                    });
                }

                var r = await net.SubmitMatchAsync(MatchId, SelfUserId, songs);
                if (!r.Ok)
                {
                    AbortMatch("Submit transport failed: " + r.Error);
                    return;
                }
                if (!r.Body.accepted)
                {
                    AbortMatch("Submit rejected: " + r.Body.error);
                    return;
                }

                // 自分の submit が通った。相手が submit 済みなら matchFinalized=true で結果が即返る。
                // そうでなければ相手待ち → 短時間 poll する。
                MatchResultDto finalResult = r.Body.result;
                if (!r.Body.matchFinalized)
                {
                    finalResult = await PollUntilFinalizedAsync();
                }

                if (finalResult == null)
                {
                    AbortMatch("Opponent submit timeout");
                    return;
                }

                FinishToMatchEndScene(finalResult);
            }
            catch (Exception e)
            {
                Debug.LogError("[PvpFlow] SubmitAndFinish exception: " + e);
                AbortMatch("Submit exception: " + e.Message);
            }
            finally
            {
                _submitting = false;
            }
        }

        async Task<MatchResultDto> PollUntilFinalizedAsync()
        {
            // 最大 60 秒、2 秒間隔で poll
            const int maxAttempts = 30;
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(2000);
                var f = await NetworkClient.Instance.FetchMatchAsync(MatchId);
                if (!f.Ok) continue;
                if (f.Body != null && f.Body.outcomeKind >= 0)
                {
                    Debug.Log($"[PvpFlow] Match finalized after {i + 1} polls");
                    return f.Body;
                }
            }
            return null;
        }

        void FinishToMatchEndScene(MatchResultDto r)
        {
            // ローカル履歴用の記録を ResetState の前に組み立てる(_replayPaths がまだ生きている)。
            var localRecord = BuildPvpMatchRecord(r, _replayPaths);

            var p = new PvpMatchEndParameters
            {
                MatchId       = r.matchId,
                UserIdA       = r.userIdA,
                UserIdB       = r.userIdB,
                SelfUserId    = SelfUserId,
                TotalPointsA  = r.totalPointsA,
                TotalPointsB  = r.totalPointsB,
                OutcomeKind   = r.outcomeKind,
                RatingABefore = r.ratingABefore,
                RatingAAfter  = r.ratingAAfter,
                RatingBBefore = r.ratingBBefore,
                RatingBAfter  = r.ratingBAfter,
                Songs         = BuildSongLines(r),
            };
            ResetState();

            // 直近10戦の記録+自分の3曲リプレイを保持。シーン遷移はブロックしない。
            _ = PersistPvpHistoryAsync(localRecord);

            if (SceneRouter.Instance != null)
                SceneRouter.Instance.GoTo(SceneId.PVPMatchEnd, p, TransitionStyle.FadeWhite);
        }

        // MatchResultDto(A/B 視点)を自分(Self)視点の PvpMatchRecord に正規化する。
        PvpMatchRecord BuildPvpMatchRecord(MatchResultDto r, List<string> myReplayPaths)
        {
            bool selfIsA = SelfUserId == r.userIdA;

            int n = r.songs?.Count ?? 0;
            var songIds = new string[n];
            var diffs   = new string[n];
            for (int i = 0; i < n; i++)
            {
                songIds[i] = r.songs[i].songId;
                diffs[i]   = string.IsNullOrEmpty(r.songs[i].difficulty) ? "extra" : r.songs[i].difficulty;
            }

            return new PvpMatchRecord
            {
                MatchId               = r.matchId,
                SelfUserId            = SelfUserId,
                OpponentId            = selfIsA ? r.userIdB : r.userIdA,
                ResultKind            = ResolveResultKind(r.outcomeKind, selfIsA),
                SelfPoints            = selfIsA ? r.totalPointsA : r.totalPointsB,
                OpponentPoints        = selfIsA ? r.totalPointsB : r.totalPointsA,
                SelfRatingBefore      = selfIsA ? r.ratingABefore : r.ratingBBefore,
                SelfRatingAfter       = selfIsA ? r.ratingAAfter  : r.ratingBAfter,
                OpponentRatingBefore  = selfIsA ? r.ratingBBefore : r.ratingABefore,
                OpponentRatingAfter   = selfIsA ? r.ratingBAfter  : r.ratingAAfter,
                SongIds               = songIds,
                Difficulties          = diffs,
                SelfSectorScores      = ToIntArray(selfIsA ? r.sectorScoresA : r.sectorScoresB),
                OpponentSectorScores  = ToIntArray(selfIsA ? r.sectorScoresB : r.sectorScoresA),
                SelfReplayPaths       = myReplayPaths != null ? myReplayPaths.ToArray() : new string[0],
                CompletedAtUnixMs     = r.completedAtUnixMs > 0
                    ? r.completedAtUnixMs
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
        }

        // outcomeKind: 0=Draw, 1=AWins, 2=BWins → 自分視点 0=Draw, 1=Win, 2=Loss
        static int ResolveResultKind(int outcomeKind, bool selfIsA)
        {
            if (outcomeKind == 0) return 0;
            bool selfWon = selfIsA ? (outcomeKind == 1) : (outcomeKind == 2);
            return selfWon ? 1 : 2;
        }

        static int[] ToIntArray(List<int> src)
        {
            if (src == null) return new int[0];
            var a = new int[src.Count];
            for (int i = 0; i < src.Count; i++) a[i] = src[i];
            return a;
        }

        // ローカル PVP 履歴へ保存し、直近10戦を超える古い試合(行+自分のリプレイ)を削除する。
        static async Task PersistPvpHistoryAsync(PvpMatchRecord record)
        {
            try
            {
                var svc  = RepositoryService.Instance;
                var repo = svc?.PlayRecords;
                if (repo == null || record == null) return;

                await repo.SavePvpMatchAsync(record);

                var stale = await repo.GetStalePvpMatchesAsync(keep: 10);
                foreach (var m in stale)
                {
                    if (svc.Replays != null && m.SelfReplayPaths != null)
                        foreach (var path in m.SelfReplayPaths) svc.Replays.Delete(path);
                    await repo.DeletePvpMatchAsync(m.MatchId);
                }

                Debug.Log($"[PvpFlow] Saved PVP match {record.MatchId?.Substring(0, 8)} to local history "
                          + $"(pruned {stale.Count} stale)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PvpFlow] PersistPvpHistory failed: " + e.Message);
            }
        }

        // 曲別ポイント内訳を共有 Domain (MatchScoring) で再構成する。サーバーの集計と同一ロジック
        // (難易度倍率適用)を使うので合計は totalPointsA/B と一致する。難易度倍率の効きを曲ごとに表示するため。
        static System.Collections.Generic.List<PvpSongLine> BuildSongLines(MatchResultDto r)
        {
            var lines = new System.Collections.Generic.List<PvpSongLine>();
            if (r?.songs == null) return lines;
            for (int i = 0; i < r.songs.Count; i++)
            {
                string diff = string.IsNullOrEmpty(r.songs[i].difficulty) ? "extra" : r.songs[i].difficulty;
                var pairs = new System.Collections.Generic.List<Domain.Pvp.SectorPair>(5);
                for (int sec = 0; sec < 5; sec++)
                {
                    int idx = i * 5 + sec;   // sectorScores は 3曲×5セクター=15 のフラット列
                    int a = (r.sectorScoresA != null && idx < r.sectorScoresA.Count) ? r.sectorScoresA[idx] : 0;
                    int b = (r.sectorScoresB != null && idx < r.sectorScoresB.Count) ? r.sectorScoresB[idx] : 0;
                    pairs.Add(new Domain.Pvp.SectorPair(r.songs[i].songId, sec, a, b, diff));
                }
                var outcome = Domain.Pvp.MatchScoring.Score(pairs);
                lines.Add(new PvpSongLine
                {
                    SongId     = r.songs[i].songId,
                    Difficulty = diff,
                    PointsA    = outcome.TotalPointsA,
                    PointsB    = outcome.TotalPointsB,
                });
            }
            return lines;
        }

        void ResetState()
        {
            IsActive = false;
            MatchId = OpponentId = SelfUserId = null;
            _songs.Clear();
            _replayPaths.Clear();
            CurrentSongIndex = 0;
        }
    }
}
