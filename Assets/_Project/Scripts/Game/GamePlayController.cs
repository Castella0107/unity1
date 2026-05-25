using System;
using System.Collections.Generic;
using System.IO;
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

            StageInitializer.BindStageVisuals(_conductor, _chart, _meta, _scroller, _hud);
            if (_judgment != null) _judgment.Initialize(_chart, _meta, _input, GameTabController.GetSavedComboBorder());
            _conductor.StartSong(clip, prerollSec: 2.0);

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

        // PVP モード: 進捗 % + 現在スコアをオーバーレイに渡す (実 POST は overlay 側で 0.5秒間隔)
        if (_params != null && _params.IsPvp && _conductor != null && _durationMs > 0
            && _judgment != null && _judgment.Aggregator != null)
        {
            var overlay = RhythmGame.Network.PvpProgressOverlay.Instance;
            if (overlay != null)
            {
                float percent = (float)Math.Max(0, Math.Min(1.0, _conductor.SongTimeMs / _durationMs));
                int   score   = _judgment.Aggregator.CurrentScore;
                overlay.UpdateLocalProgress(_params.PvpSongIndex, percent, score);
            }
        }
    }

    void OnDestroy()
    {
        StageInitializer.UnbindStageVisuals();
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
        var record = PlayRecordFactory.Create(
            snap, SongId, Difficulty,
            _chart != null ? (_chart.ChartHash ?? "") : "",
            _totalNotes, ParseModifiers(_params?.Modifier), false, null);

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

        // ── Best score + SQLite save ─────────────────────────────────────────
        int  bestBefore = 0;
        bool isNewBest  = false;

        var playRepo = repoSvc?.PlayRecords;
        if (playRepo != null)
        {
            var best   = await playRepo.GetBestAsync(record.SongId, record.Difficulty);
            bestBefore = best?.BestEffectiveScore ?? 0;
            isNewBest  = record.EffectiveScore > bestBefore;
            await playRepo.SaveAsync(record);
        }
        else
        {
            string bestKey = string.Format("Best_{0}_{1}", SongId, Difficulty);
            bestBefore = PlayerPrefs.GetInt(bestKey, 0);
            isNewBest  = record.EffectiveScore > bestBefore;
            if (isNewBest) { PlayerPrefs.SetInt(bestKey, record.EffectiveScore); PlayerPrefs.Save(); }
        }

        // ── サーバー自動送信 (fire-and-forget) ───────────────────────────────
        // ローカル保存とリザルト遷移を絶対にブロックしない設計。
        // 失敗・タイムアウト・サーバー停止すべて Debug.LogWarning に落として続行。
        // PVP モードでは matchId バンドルで submit するので個別送信はスキップ。
        if (_params == null || !_params.IsPvp)
            SubmitToServerFireAndForget(record);

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

        // PVP モード: Result シーンへ寄らず PvpFlowController に通知し、次曲または送信フェーズへ
        if (_params != null && _params.IsPvp)
        {
            var pvp = RhythmGame.Network.PvpFlowController.Instance;
            if (pvp != null && pvp.IsActive)
            {
                Debug.Log($"[GamePlay] PVP song completed, notifying PvpFlowController (idx={_params.PvpSongIndex})");
                pvp.OnSongCompleted(record.SongId, record.ReplayPath);
                return;   // SceneRouter は PvpFlowController が次曲または PvpMatchEnd へ
            }
            Debug.LogWarning("[GamePlay] IsPvp=true but PvpFlowController is not active — falling back to Result");
        }

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

    // ── サーバー自動送信 ──────────────────────────────────────────────────
    // ローカル DB に保存済みの PlayRecord をサーバーで検証 + leaderboard 登録する。
    // Result 遷移を絶対にブロックしないため async void で発射する。
    static async void SubmitToServerFireAndForget(PlayRecord record)
    {
        try
        {
            if (record == null || string.IsNullOrEmpty(record.ReplayPath) || !File.Exists(record.ReplayPath))
            {
                Debug.Log("[GamePlay] Server submit skipped — no replay path");
                return;
            }
            var net = RhythmGame.Network.NetworkClient.Instance;
            if (net == null)
            {
                Debug.Log("[GamePlay] Server submit skipped — NetworkClient not bootstrapped");
                return;
            }

            byte[] replayBytes = File.ReadAllBytes(record.ReplayPath);
            var claim = new RhythmGame.Network.ResultClaimDto
            {
                score       = record.RawScore,
                maxCombo    = record.MaxCombo,
                perfectPlus = record.PerfectPlusCount,
                perfect     = record.PerfectCount,
                great       = record.GreatCount,
                good        = record.GoodCount,
                miss        = record.MissCount,
                rank        = record.Rank ?? "",
            };
            var meta = new RhythmGame.Network.ValidateRequestDto
            {
                playId           = record.PlayId,
                songId           = record.SongId,
                difficulty       = record.Difficulty,
                userId           = RhythmGame.Network.LocalIdentity.UserId,
                playedAtUnixMs   = record.PlayedAtUnixMs,
                totalNotes       = record.TotalNotes,
                isFullCombo      = record.IsFullCombo,
                isAllPerfect     = record.IsAllPerfect,
                isAllPerfectPlus = record.IsAllPerfectPlus,
            };

            // Result 画面が同じ送信結果 (VALID/INVALID) を表示できるよう、進行中の Task を共有スロットに登録。
            var validateTask = net.ValidateReplayAsync(record.ChartHash, replayBytes, claim, meta);
            RhythmGame.Network.ServerSubmissionTracker.Register(record.PlayId, validateTask);
            var r = await validateTask;
            if (!r.Ok)
            {
                Debug.LogWarning("[GamePlay] Server submit transport failed: " + r.TransportError + " — enqueuing");
                RhythmGame.Network.SubmissionQueue.Enqueue(new RhythmGame.Network.SubmissionQueue.QueuedEntry
                {
                    ChartHash  = record.ChartHash,
                    ReplayPath = record.ReplayPath,
                    Claim      = claim,
                    Meta       = meta,
                });
                return;
            }
            if (r.Body.isValid)
            {
                Debug.Log($"[GamePlay] Server VALID — score={r.Body.serverResult?.score} (rt={r.RoundtripMs}ms)");
            }
            else
            {
                Debug.LogWarning($"[GamePlay] Server INVALID — {r.Body.mismatchReason} (rt={r.RoundtripMs}ms)");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GamePlay] Server submit exception: " + e.Message);
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
