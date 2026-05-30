using System;
using System.Collections.Generic;
using UnityEngine;

// Drives replay playback in the GamePlay scene.
// Active only when GamePlayParameters.IsReplay == true.
// GamePlayController deactivates itself in that case.

/// <summary>
/// GamePlay シーンでリプレイ再生を制御するクラス。
/// GamePlayParameters.IsReplay が true のときのみ有効化され、
/// 保存済みリプレイファイルを読み込んで ReplayInputSource 経由で JudgmentSystem に入力を供給する。
/// 再生完了後は履歴画面へ遷移する。
/// </summary>
public class ReplayPlaybackController : MonoBehaviour
{
    [SerializeField] AudioConductor      _conductor;
    [SerializeField] NoteScroller        _scroller;
    [SerializeField] JudgmentSystem      _judgment;
    [SerializeField] GameInputController _liveInput;
    [SerializeField] GameHud             _hud;

    ReplayInputSource _replayInput;
    ReplayData        _replay;
    ChartData         _chart;
    SongMetadata      _meta;
    bool              _isPlaying;

    /// <summary>リプレイ再生中か。</summary>
    public bool   IsPlaying     => _isPlaying;
    /// <summary>現在の再生速度倍率。</summary>
    public double PlaybackSpeed => _conductor != null ? _conductor.PlaybackSpeed : 1.0;
    /// <summary>リプレイ入力を再生し終えたか。</summary>
    public bool   IsFinished    => _replayInput?.IsFinished ?? false;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    async void Start()
    {
        var prm = ParameterStore.GetPending<GamePlayParameters>()
               ?? ParameterStore.GetCurrent<GamePlayParameters>();

        if (prm == null || !prm.IsReplay)
        {
            gameObject.SetActive(false);
            return;
        }

        Application.runInBackground = true;

        // Disable live input while replaying
        if (_liveInput != null) _liveInput.enabled = false;

        try
        {
            // ── Load replay file ─────────────────────────────────────────────
            var repoSvc = RepositoryService.Instance;
            if (repoSvc?.Replays == null || string.IsNullOrEmpty(prm.ReplayPath))
            {
                Debug.LogError("[Replay] ReplayPath is empty or ReplayStorage unavailable.");
                return;
            }

            byte[] bytes = await repoSvc.Replays.ReadAsync(prm.ReplayPath);
            if (bytes == null)
            {
                Debug.LogError("[Replay] Could not read replay file: " + prm.ReplayPath);
                return;
            }

            _replay = ReplayDecoder.Decode(bytes);

            // ── Load chart / meta / audio ────────────────────────────────────
            _meta  = await ChartLoader.LoadMetaAsync(prm.SongId);
            _chart = await ChartLoader.LoadChartAsync(prm.SongId, prm.Difficulty);

            AudioClip clip = null;
            try { clip = await ChartLoader.LoadAudioAsync(prm.SongId); }
            catch (Exception e)
            {
                Debug.LogWarning("[Replay] Audio not found: " + e.Message + " → silent fallback");
                clip = AudioClip.Create("silent", 44100 * 30, 1, 44100, false);
            }

            // ── Apply saved offsets (same as live mode) ──────────────────────
            await StageInitializer.ApplyAudioOffsetsAsync(_conductor, prm.SongId);

            // ── ChartHash verification ───────────────────────────────────────
            if (!ReplayValidator.MatchesChart(_replay, _chart))
                Debug.LogWarning("[Replay] ChartHash mismatch. Judgment may differ from original play.");

            // ── Initialize stage visuals (lane / beatgrid / jacket / hud) ────
            StageInitializer.BindStageVisuals(_conductor, _chart, _meta, _scroller, _hud, prm.HiSpeed);

            // ── Set up replay input and judgment ─────────────────────────────
            _replayInput = new ReplayInputSource(_replay);
            _judgment.Initialize(_chart, _meta, _replayInput, Judgment.Good);

            // ── Apply playback speed ─────────────────────────────────────────
            double speed = prm.InitialPlaybackSpeed > 0.0 ? prm.InitialPlaybackSpeed : 1.0;
            _conductor.SetPlaybackSpeed(speed);
            _conductor.StartSong(clip, prerollSec: 2.0, audioOffsetMs: _meta?.AudioOffsetMs ?? 0);
            _isPlaying = true;

            Debug.Log(string.Format("[Replay] Started — song={0}  diff={1}  events={2}  speed={3}x",
                prm.SongId, prm.Difficulty, _replayInput.EventCount, speed));
        }
        catch (Exception e)
        {
            Debug.LogError("[Replay] Start failed: " + e.Message + "\n" + e.StackTrace);
        }
    }

    void Update()
    {
        if (!_isPlaying || _replayInput == null || _conductor == null) return;

        // Feed replay events up to current song time.
        // JudgmentSystem.Update handles ProcessTime independently each frame.
        _replayInput.Advance(_conductor.SongTimeMs);

        // Detect replay end: all events consumed and song has passed duration
        if (_replayInput.IsFinished
            && _meta != null
            && _conductor.SongTimeMs >= _meta.DurationMs + 1000.0)
        {
            _isPlaying = false;
            OnReplayFinished();
        }
    }

    void OnDestroy()
    {
        if (_liveInput != null) _liveInput.enabled = true;
        StageInitializer.UnbindStageVisuals();
    }

    // ── Public controls ────────────────────────────────────────────────────────

    /// <summary>リプレイ再生を一時停止する。</summary>
    public void Pause()
    {
        if (!_isPlaying) return;
        _conductor?.Pause();
        _isPlaying = false;
    }

    /// <summary>一時停止したリプレイ再生を再開する。</summary>
    public void Resume()
    {
        if (_isPlaying) return;
        _conductor?.Resume(prerollSec: 0.0);
        _isPlaying = true;
    }

    /// <summary>リプレイを先頭から再生し直す。</summary>
    public void Restart()
    {
        if (_replay == null || _chart == null || _meta == null) return;
        _conductor?.Stop();
        _scroller?.Reset();

        _replayInput = new ReplayInputSource(_replay);
        _judgment.Initialize(_chart, _meta, _replayInput, Judgment.Good);

        _conductor.SetPlaybackSpeed(PlaybackSpeed);
        AudioClip clip = _conductor.GetComponent<AudioSource>()?.clip;
        // 実効シフト = AudioOffsetMs + FirstOnsetMs (拍起点も音源側にずらして反映)
        int audioShift = (_meta?.AudioOffsetMs ?? 0) + (_meta?.FirstOnsetMs ?? 0);
        if (clip != null) _conductor.StartSong(clip, prerollSec: 1.0, audioOffsetMs: audioShift);
        _isPlaying = true;
    }

    /// <summary>リプレイ再生速度を設定する。</summary>
    public void SetPlaybackSpeed(double speed)
        => _conductor?.SetPlaybackSpeed(speed);

    // ── Private ────────────────────────────────────────────────────────────────

    void OnReplayFinished()
    {
        _conductor?.Stop();
        _scroller?.Reset();
        StageInitializer.UnbindStageVisuals();
        if (_liveInput != null) _liveInput.enabled = true;

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.History);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("History");
    }
}
