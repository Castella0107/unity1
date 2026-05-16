using UnityEngine;

// Audio timing core for the rhythm game.
// All time values are derived from AudioSettings.dspTime — never accumulated.
// Frame rate (60/240 fps) has zero effect on precision.
//
// HOW TO USE
//   1. Place on a GameObject in _Persistent scene (survives scene loads).
//   2. Call StartSong(clip) before the chart starts. prerollSec gives time to
//      initialise visuals before audio starts (SongTimeMs < 0 during preroll).
//   3. Each frame, read SongTimeMs for game logic, JudgmentTimeMs for hit windows,
//      VisualTimeMs for note scroll position.
//   4. Tune offsets via ApplyAppOffsets() and ApplyPerSongOffset().

/// <summary>
/// リズムゲームのオーディオタイミングコア。AudioSettings.dspTime を基準に SongTimeMs / JudgmentTimeMs / VisualTimeMs を提供するシングルトン。
/// フレームレートに依存しない精度を持ち、オフセット適用・再生速度変更・ポーズ/リジューム機能を備える。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class AudioConductor : MonoBehaviour
{
    public static AudioConductor Instance { get; private set; }

    // ── Time properties ───────────────────────────────────────────────────────

    /// Milliseconds since the song origin (negative during preroll).
    /// Scaled by PlaybackSpeed — at 2x speed, time advances twice as fast.
    public double SongTimeMs
    {
        get
        {
            if (_isPaused)  return _pausedSongTimeMs;
            if (_isPlaying) return (AudioSettings.dspTime - _dspStartTime) * 1000.0 * _playbackSpeed;
            return 0.0;
        }
    }

    /// SongTimeMs minus all judgment offsets. Use for hit-window checks.
    public double JudgmentTimeMs =>
        SongTimeMs - _appOffsets.JudgmentOffsetMs - (_perSongOffset?.JudgmentOffsetMs ?? 0);

    /// SongTimeMs minus app visual offset. Use for note scroll position.
    /// Per-song offset is intentionally NOT applied here (it is a judgment-only correction).
    public double VisualTimeMs => SongTimeMs - _appOffsets.VisualOffsetMs;

    // ── State ─────────────────────────────────────────────────────────────────

    public bool IsPlaying => _isPlaying;
    public bool IsPaused  => _isPaused;

    public AppOffsetSettings AppOffsets    => _appOffsets;
    public PerSongOffset     PerSongOffset => _perSongOffset;

    // ── Private ───────────────────────────────────────────────────────────────

    AudioSource       _audioSource;
    double            _dspStartTime;
    double            _pausedSongTimeMs;
    bool              _isPlaying;
    bool              _isPaused;
    double            _playbackSpeed = 1.0;
    AppOffsetSettings _appOffsets    = AppOffsetSettings.Default;
    PerSongOffset     _perSongOffset;

    public double PlaybackSpeed => _playbackSpeed;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(Instance.gameObject);
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _audioSource = GetComponent<AudioSource>();
    }

    // ── Offset API (new) ──────────────────────────────────────────────────────

    /// Apply app-level (device profile) offsets. Call before StartSong.
    public void ApplyAppOffsets(AppOffsetSettings settings)
    {
        _appOffsets = (settings ?? AppOffsetSettings.Default).Clamped();
        Debug.Log(string.Format("[AudioConductor] AppOffsets J={0} V={1}",
            _appOffsets.JudgmentOffsetMs, _appOffsets.VisualOffsetMs));
    }

    /// Apply per-song judgment offset. Call before StartSong.
    public void ApplyPerSongOffset(PerSongOffset offset)
    {
        _perSongOffset = offset?.Clamped();
        Debug.Log(string.Format("[AudioConductor] PerSongOffset {0}={1}",
            _perSongOffset?.SongId ?? "null",
            _perSongOffset?.JudgmentOffsetMs ?? 0));
    }

    // ── Playback API ──────────────────────────────────────────────────────────

    public void StartSong(AudioClip clip, double prerollSec = 1.0)
    {
        if (clip == null) { Debug.LogWarning("[AudioConductor] StartSong: clip is null"); return; }
        Stop();
        double scheduled    = AudioSettings.dspTime + prerollSec;
        _audioSource.clip   = clip;
        _audioSource.PlayScheduled(scheduled);
        _dspStartTime       = scheduled;
        _isPlaying          = true;
        _isPaused           = false;
        _pausedSongTimeMs   = 0.0;
    }

    public void Pause()
    {
        if (!_isPlaying) return;
        _pausedSongTimeMs = SongTimeMs;   // already scaled by _playbackSpeed
        _audioSource.Pause();
        _isPlaying = false;
        _isPaused  = true;
    }

    public void Resume(double prerollSec = 0.5)
    {
        if (_isPlaying || !_isPaused || _audioSource.clip == null) return;
        // Convert scaled game-time back to actual audio position in seconds
        double audioPosSec = _pausedSongTimeMs / 1000.0 / _playbackSpeed;
        double scheduled   = AudioSettings.dspTime + prerollSec;
        if (audioPosSec > 0.0)
        {
            _audioSource.timeSamples = Mathf.Clamp(
                (int)(audioPosSec * _audioSource.clip.frequency),
                0, _audioSource.clip.samples - 1);
        }
        _audioSource.PlayScheduled(scheduled);
        _dspStartTime = scheduled - audioPosSec;
        _isPlaying    = true;
        _isPaused     = false;
    }

    /// Change playback speed while preserving the current song position.
    /// Adjusts AudioSource.pitch and recalculates _dspStartTime.
    public void SetPlaybackSpeed(double speed)
    {
        speed = speed < 0.1 ? 0.1 : speed > 4.0 ? 4.0 : speed;
        if (_isPlaying)
        {
            double currentMs = SongTimeMs;  // snapshot before changing speed
            _playbackSpeed         = speed;
            _audioSource.pitch     = (float)speed;
            _dspStartTime          = AudioSettings.dspTime - currentMs / 1000.0 / _playbackSpeed;
        }
        else
        {
            _playbackSpeed     = speed;
            _audioSource.pitch = (float)speed;
        }
    }

    public void Stop()
    {
        _audioSource.Stop();
        _isPlaying        = false;
        _isPaused         = false;
        _dspStartTime     = 0.0;
        _pausedSongTimeMs = 0.0;
    }

    // ── Legacy API (Obsolete) ─────────────────────────────────────────────────

    [System.Obsolete("Use ApplyAppOffsets(AppOffsetSettings)")]
    public int GlobalJudgmentOffsetMs
    {
        get => _appOffsets.JudgmentOffsetMs;
        set => ApplyAppOffsets(new AppOffsetSettings
            { JudgmentOffsetMs = value, VisualOffsetMs = _appOffsets.VisualOffsetMs });
    }

    [System.Obsolete("Use ApplyAppOffsets(AppOffsetSettings)")]
    public int GlobalVisualOffsetMs
    {
        get => _appOffsets.VisualOffsetMs;
        set => ApplyAppOffsets(new AppOffsetSettings
            { JudgmentOffsetMs = _appOffsets.JudgmentOffsetMs, VisualOffsetMs = value });
    }

    [System.Obsolete("Use ApplyPerSongOffset(PerSongOffset)")]
    public int PerSongOffsetMs
    {
        get => _perSongOffset?.JudgmentOffsetMs ?? 0;
        set => ApplyPerSongOffset(new PerSongOffset
            { SongId = _perSongOffset?.SongId ?? "", JudgmentOffsetMs = value });
    }
}
