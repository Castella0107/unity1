using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Orchestrates a single gameplay session.
// Reads GamePlayParameters from ParameterStore (set by SongSelectController).
// On completion: saves ReplayData to file, saves PlayRecord to SQLite, navigates to Result.

/// <summary>
/// 1 回のゲームプレイセッションを統括するコントローラー。
/// ParameterStore から GamePlayParameters を取得してチャート・オーディオを非同期ロードし、
/// セッション終了後にリプレイデータの保存・プレイ記録の SQLite 保存・リザルト画面への遷移を行う。
/// </summary>
public class GamePlayController : MonoBehaviour
{
    [SerializeField] AudioConductor       _conductor;
    [SerializeField] NoteScroller         _scroller;
    [SerializeField] JudgmentSystem       _judgment;
    [SerializeField] GameInputController  _input;
    [SerializeField] GameHud              _hud;
    [SerializeField] string          _fallbackSongId     = "test_song";
    [SerializeField] string          _fallbackDifficulty = "extra";
    [SerializeField] TextMeshProUGUI _timeText;

    double       _durationMs;
    int          _totalNotes;
    bool         _resultTriggered;
    SongMetadata _meta;
    ChartData    _chart;

    GamePlayParameters _params;

    string SongId     => _params?.SongId     ?? _fallbackSongId;
    string Difficulty => _params?.Difficulty  ?? _fallbackDifficulty;

    void OnEnable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += HandleProfileChanged;
    }

    void OnDisable()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= HandleProfileChanged;
    }

    void HandleProfileChanged(DeviceProfile newProfile)
    {
        if (_conductor == null || newProfile == null) return;
        _conductor.ApplyAppOffsets(newProfile.Offsets);
        Debug.Log("[GamePlay] Profile changed mid-play: " + newProfile.DisplayName);
    }

    async void Start()
    {
        Application.runInBackground = true;

        if (_input == null)
        {
            Debug.LogError("[GamePlay] _input is not assigned in Inspector. " +
                           "Drag GameInputController GameObject to GamePlayController._input.");
            return;
        }

        _params = ParameterStore.GetPending<GamePlayParameters>();

        // Replay mode: let ReplayPlaybackController handle this session
        if (_params != null && _params.IsReplay)
        {
            gameObject.SetActive(false);
            return;
        }
        if (_params == null)
            Debug.LogWarning("[GamePlay] No ParameterStore entry — using fallback inspector values");

        try
        {
            _meta       = await ChartLoader.LoadMetaAsync(SongId);
            _chart      = await ChartLoader.LoadChartAsync(SongId, Difficulty);
            _durationMs = _meta.DurationMs;
            _totalNotes = _chart.TotalNotes;

            await StageInitializer.ApplyAudioOffsetsAsync(
                _conductor, SongId,
                fallbackJudgeMs:  _params?.JudgeOffset  ?? 0,
                fallbackVisualMs: _params?.VisualOffset ?? 0);

            AudioClip clip = null;
            try { clip = await ChartLoader.LoadAudioAsync(SongId); }
            catch (Exception e)
            {
                Debug.LogWarning("[GamePlay] Audio not found: " + e.Message +
                                 " → using 30-second silent clip");
                clip = AudioClip.Create("silent_fallback", 44100 * 30, 1, 44100, false);
            }

            StageInitializer.BindStageVisuals(_conductor, _chart, _meta, _scroller, _hud,
                                              _params?.HiSpeed ?? 0f);
            // PVP: 相手情報ボックス・VS スコアバー・セクター勝敗タグを有効化(BindStageVisuals は
            // 共有ゆえ isPvP:false 固定なので、ここで明示注入する)。
            if (_params != null && _params.IsPvp && _hud != null)
            {
                _hud.SetPvpContext(_params.PvpOpponentId);

                // Go 新フロー: WS opponent_progress → 相手ライブスコアを HUD へ (M5)
                var sock = RhythmGame.Network.Api.PvpMatchContext.Socket;
                if (sock != null)
                {
                    _opponentProgressHandler = (songOrder, percentX1000, score) =>
                    {
                        if (songOrder == RhythmGame.Network.Api.PvpMatchContext.CurrentSongOrder && _hud != null)
                            _hud.UpdateOpponentLive(score);
                    };
                    sock.OnOpponentProgress += _opponentProgressHandler;
                }
            }
            if (_judgment != null) _judgment.Initialize(_chart, _meta, _input, GameplayTabController.GetSavedComboBorder());
            // 実効シフト = AudioOffsetMs + FirstOnsetMs (拍起点も音源側にずらして反映)
            int audioShift = (_meta?.AudioOffsetMs ?? 0) + (_meta?.FirstOnsetMs ?? 0);
            _conductor.StartSong(clip, prerollSec: 2.0, audioOffsetMs: audioShift);

            Debug.Log(string.Format("[GamePlay] Started — song={0}  difficulty={1}  notes={2}",
                SongId, Difficulty, _chart.Notes.Count));
        }
        catch (Exception e)
        {
            Debug.LogError("[GamePlay] Start failed: " + e.Message + "\n" + e.StackTrace);
        }
    }

    void Update()
    {
        if (_timeText != null && _conductor != null)
            _timeText.text = string.Format("SongTime:  {0:F0} ms", _conductor.SongTimeMs);

        if (!_resultTriggered && _conductor != null && _conductor.IsPlaying
            && _durationMs > 0 && _conductor.SongTimeMs >= _durationMs + 1000.0)
        {
            _resultTriggered = true;
            TriggerResultAsync();
        }

        // PVP モード: 進捗 % + 現在スコアを送信
        if (_params != null && _params.IsPvp && _conductor != null && _durationMs > 0
            && _judgment != null && _judgment.Aggregator != null)
        {
            float percent = (float)Math.Max(0, Math.Min(1.0, _conductor.SongTimeMs / _durationMs));
            int   score   = _judgment.Aggregator.CurrentScore;

            // Go フロー: WebSocket progress (0.5秒間隔、docs/06 §6.7)
            var socket = RhythmGame.Network.Api.PvpMatchContext.Socket;
            if (socket != null && socket.IsConnected)
            {
                _wsProgressTimer += Time.unscaledDeltaTime;
                if (_wsProgressTimer >= 0.5f)
                {
                    _wsProgressTimer = 0f;
                    _ = socket.SendProgressAsync(
                        RhythmGame.Network.Api.PvpMatchContext.CurrentSongOrder,
                        (int)(percent * 100000f), score);
                }
            }
        }
    }

    float _wsProgressTimer;

    System.Action<int, int, long> _opponentProgressHandler;

    void OnDestroy()
    {
        StageInitializer.UnbindStageVisuals();
        if (_opponentProgressHandler != null &&
            RhythmGame.Network.Api.PvpMatchContext.Socket != null)
            RhythmGame.Network.Api.PvpMatchContext.Socket.OnOpponentProgress -= _opponentProgressHandler;
    }

    async void TriggerResultAsync()
    {
        Debug.Log("[GamePlay] TriggerResultAsync started");

        var repoSvcEarly = RepositoryService.Instance;
        Debug.Log("[GamePlay] ReplayBuffer events: "
                  + (_judgment?.ReplayBuffer?.Events?.Count.ToString() ?? "null"));
        Debug.Log("[GamePlay] repoSvc null: "      + (repoSvcEarly == null));
        Debug.Log("[GamePlay] repoSvc.IsReady: "   + (repoSvcEarly?.IsReady.ToString() ?? "N/A"));
        Debug.Log("[GamePlay] Replays null: "       + (repoSvcEarly?.Replays == null));
        Debug.Log("[GamePlay] PlayRecords null: "   + (repoSvcEarly?.PlayRecords == null));

        if (_conductor != null) _conductor.Stop();
        StageInitializer.UnbindStageVisuals();
        if (_scroller  != null) _scroller.Reset();
        if (_judgment == null || _judgment.Aggregator == null) return;

        var snap   = _judgment.SnapshotForResult();
        bool isPvpPlay = _params != null && _params.IsPvp;
        var record = PlayRecordFactory.Create(
            snap, SongId, Difficulty,
            _chart != null ? (_chart.ChartHash ?? "") : "",
            _totalNotes, ParseModifiers(_params?.Modifier), isPvpPlay, null);

        // ── Build replay data ────────────────────────────────────────────────
        var repoSvc       = RepositoryService.Instance;
        var activeProfile = repoSvc?.ActiveProfile;

        PerSongOffset perSongOffset = null;
        if (repoSvc?.Offsets != null)
            perSongOffset = await repoSvc.Offsets.GetPerSongOffsetAsync(SongId);

        var replayData = new ReplayData
        {
            Header   = new ReplayHeader(),
            Metadata = new ReplayMetadata
            {
                SongId                = record.SongId,
                Difficulty            = record.Difficulty,
                ChartHash             = HexStringToBytes(_chart?.ChartHash ?? ""),
                PlayedAtUnixMs        = record.PlayedAtUnixMs,
                DurationMs            = _meta?.DurationMs ?? 0,
                Bpm                   = _meta != null ? (float)_meta.Bpm : 0f,
                AppJudgmentOffsetMs   = (short)(activeProfile?.Offsets.JudgmentOffsetMs ?? 0),
                AppVisualOffsetMs     = (short)(activeProfile?.Offsets.VisualOffsetMs   ?? 0),
                PerSongOffsetMs       = (short)(perSongOffset?.JudgmentOffsetMs         ?? 0),
                Modifiers             = record.Modifiers,
                JudgmentEngineVersion = record.JudgmentEngineVersion,
            },
            Result = new ReplayResult
            {
                RawScore         = record.RawScore,
                EffectiveScore   = record.EffectiveScore,
                Rank             = record.Rank,
                PerfectPlusCount = record.PerfectPlusCount,
                PerfectCount     = record.PerfectCount,
                GreatCount       = record.GreatCount,
                GoodCount        = record.GoodCount,
                MissCount        = record.MissCount,
                MaxCombo         = record.MaxCombo,
                FastCount        = record.FastCount,
                LateCount        = record.LateCount,
                TotalNotes       = record.TotalNotes,
            },
            InputEvents = _judgment.ReplayBuffer != null
                ? new List<ReplayInputEvent>(_judgment.ReplayBuffer.Events)
                : new List<ReplayInputEvent>(),
        };

        // ── Save replay file ─────────────────────────────────────────────────
        if (repoSvc?.Replays != null)
        {
            record.ReplayPath = await repoSvc.Replays.SaveAsync(
                record.PlayId, replayData, record.PlayedAtUnixMs);
            Debug.Log("[GamePlay] Replay saved: " + (record.ReplayPath ?? "null"));
        }
        else
        {
            Debug.LogWarning("[GamePlay] Replay NOT saved — repoSvc.Replays is null.");
        }

        // PVP プレイはソロ PlayRecords に保存しない(Free/ソロ履歴を汚さない)。リプレイファイルは
        // 上で保存済み(Ladder の各曲再生に使う)。各曲の集積は PvpResultBridge.SubmitAndContinueAsync
        // が sector_scores 付きで行い、試合終了(GoToMatchEnd)で PvpMatchRecord に焼く。

        // ── Best score + SQLite save (ソロのみ) ──────────────────────────────
        int    bestBefore         = 0;
        bool   isNewBest          = false;
        string previousBestPlayId = null;

        var playRepo = repoSvc?.PlayRecords;
        if (!isPvpPlay && playRepo != null)
        {
            var best   = await playRepo.GetBestAsync(record.SongId, record.Difficulty);
            bestBefore = best?.BestEffectiveScore ?? 0;
            isNewBest  = record.EffectiveScore > bestBefore;
            previousBestPlayId = best?.BestPlayId;
            await playRepo.SaveAsync(record);
        }
        else if (!isPvpPlay)
        {
            string bestKey = string.Format("Best_{0}_{1}", SongId, Difficulty);
            bestBefore = PlayerPrefs.GetInt(bestKey, 0);
            isNewBest  = record.EffectiveScore > bestBefore;
            if (isNewBest) { PlayerPrefs.SetInt(bestKey, record.EffectiveScore); PlayerPrefs.Save(); }
        }

        // ── サーバー自動送信 ─────────────────────────────────────────────────
        // ソロのサーバー検証 (Go /score/validate) は Phase 7 で未実装のため停止中。
        // K の Phase 7 実装後に Go 向け送信を追加して再開する。

        // ── ソロのリプレイ刈り込み ───────────────────────────────────────────
        // 各楽曲×難易度の最高スコアのリプレイだけローカルに残す。
        // PVP リプレイは PvpResultBridge の送信 + PVP 履歴のリングバッファが管理するので触らない。
        if ((_params == null || !_params.IsPvp) && playRepo != null && repoSvc?.Replays != null)
            await PruneSoloReplaysAsync(playRepo, repoSvc.Replays, record, isNewBest, previousBestPlayId);

        var view = new PlayResultView
        {
            Record                   = record,
            SongTitle                = _meta != null ? _meta.Title : SongId,
            SongArtist               = _meta != null ? _meta.Artist : "",
            Level                    = _chart != null ? _chart.Level : 0,
            BestEffectiveScoreBefore = bestBefore,
            IsNewBest                = isNewBest,
        };

        var resultParams = new ResultParameters
        {
            View                     = view,
            SourceGamePlayParameters = _params,
        };

        JacketBackgroundController.Instance?.SetCanvasEnabled(true);
        JacketBackgroundController.Instance?.SetJacket(SongId);

        // PVP モード (Go 新フロー): submit → 曲リザルト (PVPResult) へ
        if (_params != null && _params.IsPvp &&
            !string.IsNullOrEmpty(RhythmGame.Network.Api.PvpMatchContext.MatchId))
        {
            Debug.Log($"[GamePlay] PVP song completed (Go flow) — submit へ (order={RhythmGame.Network.Api.PvpMatchContext.CurrentSongOrder})");
            await RhythmGame.Network.Api.PvpResultBridge.SubmitAndContinueAsync(record);
            return;
        }

        if (_params != null && _params.IsPvp)
            Debug.LogWarning("[GamePlay] IsPvp=true but PvpMatchContext is not active — falling back to Result");

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.Result, resultParams);
        else
        {
            ParameterStore.SetPending(resultParams);
            SceneManager.LoadScene("Result");
        }

        Debug.Log("[GamePlay] TriggerResultAsync completed — score=" + record.EffectiveScore
                  + "  replayPath=" + (record.ReplayPath ?? "not saved"));
    }

    // ソロは「各楽曲×難易度の最高スコア」のリプレイだけを残す。
    //  - 新ベスト  → 旧ベストのリプレイを削除し、その行の ReplayPath を null 化
    //  - 非ベスト  → 今保存したリプレイを破棄し、自分の行の ReplayPath を null 化
    // PVP 記録(IsPvP)のリプレイは PVP 履歴側が所有するため、ここでは絶対に削除しない。
    static async System.Threading.Tasks.Task PruneSoloReplaysAsync(
        IPlayRecordRepository repo, ReplayStorage replays,
        PlayRecord justSaved, bool isNewBest, string previousBestPlayId)
    {
        try
        {
            if (isNewBest)
            {
                if (!string.IsNullOrEmpty(previousBestPlayId) && previousBestPlayId != justSaved.PlayId)
                {
                    var old = await repo.GetByIdAsync(previousBestPlayId);
                    if (old != null && !old.IsPvP && !string.IsNullOrEmpty(old.ReplayPath))
                    {
                        replays.Delete(old.ReplayPath);
                        await repo.ClearReplayPathAsync(previousBestPlayId);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(justSaved.ReplayPath))
            {
                replays.Delete(justSaved.ReplayPath);
                await repo.ClearReplayPathAsync(justSaved.PlayId);
                justSaved.ReplayPath = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GamePlay] Solo replay prune failed: " + e.Message);
        }
    }

    static string[] ParseModifiers(string mod)
    {
        if (string.IsNullOrEmpty(mod) || mod == "None") return new string[0];
        return new[] { mod };
    }

    static byte[] HexStringToBytes(string hex)
    {
        var bytes = new byte[32];
        if (string.IsNullOrEmpty(hex)) return bytes;

        // Reject odd-length or non-hex strings rather than crashing on Convert.ToByte.
        if (hex.Length % 2 != 0 || !IsValidHex(hex))
        {
            Debug.LogWarning("[GamePlay] ChartHash '" + hex + "' is not valid hex — using zero bytes");
            return bytes;
        }

        int len = Math.Min(hex.Length / 2, 32);
        for (int i = 0; i < len; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    static bool IsValidHex(string s)
    {
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }
}
