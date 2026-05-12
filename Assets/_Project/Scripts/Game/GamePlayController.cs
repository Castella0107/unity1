using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Orchestrates a single gameplay session.
// Reads GamePlayParameters from ParameterStore (set by SongSelectController).
// On completion: saves ReplayData to file, saves PlayRecord to SQLite, navigates to Result.
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

            if (_conductor != null)
            {
                var repo = RepositoryService.Instance;
                if (repo != null && repo.IsReady)
                {
                    _conductor.ApplyAppOffsets(repo.ActiveProfile.Offsets);
                    var perSong = await repo.Offsets.GetPerSongOffsetAsync(SongId);
                    _conductor.ApplyPerSongOffset(perSong);
                }
                else
                {
                    _conductor.ApplyAppOffsets(new AppOffsetSettings
                    {
                        JudgmentOffsetMs = _params?.JudgeOffset  ?? SimpleCalibration.GetJudgmentOffset(),
                        VisualOffsetMs   = _params?.VisualOffset ?? SimpleCalibration.GetVisualOffset(),
                    });
                }
            }

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
