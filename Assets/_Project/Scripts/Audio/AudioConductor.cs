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
    /// <summary>シングルトンインスタンス。</summary>
    public static AudioConductor Instance { get; private set; }

    // ── Time properties ───────────────────────────────────────────────────────

    /// <summary>曲開始からの経過時間(ms、プリロール中は負)。PlaybackSpeed で倍率がかかる。</summary>
    public double SongTimeMs
    {
        get
        {
            if (_isPaused)  return _pausedSongTimeMs;
            if (_isPlaying) return (AudioSettings.dspTime - _dspStartTime) * 1000.0 * _playbackSpeed;
            return 0.0;
        }
    }

    /// <summary>全判定オフセットを差し引いた時刻。ヒット窓判定に使う。</summary>
    public double JudgmentTimeMs =>
        SongTimeMs - _appOffsets.JudgmentOffsetMs - (_perSongOffset?.JudgmentOffsetMs ?? 0);

    /// <summary>アプリ映像オフセットを差し引いた時刻。ノーツのスクロール位置に使う(曲別オフセットは判定専用のため適用しない)。</summary>
    public double VisualTimeMs => SongTimeMs - _appOffsets.VisualOffsetMs;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>再生中か。</summary>
    public bool IsPlaying => _isPlaying;
    /// <summary>一時停止中か。</summary>
    public bool IsPaused  => _isPaused;

    /// <summary>現在適用中のアプリ(デバイスプロファイル)オフセット。</summary>
    public AppOffsetSettings AppOffsets    => _appOffsets;
    /// <summary>現在適用中の曲別オフセット。</summary>
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

    /// <summary>現在の再生速度倍率(0.1〜4.0)。</summary>
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

    /// <summary>アプリ(デバイスプロファイル)レベルのオフセットを適用する。StartSong より前に呼ぶ。</summary>
    public void ApplyAppOffsets(AppOffsetSettings settings)
    {
        _appOffsets = (settings ?? AppOffsetSettings.Default).Clamped();
        Debug.Log(string.Format("[AudioConductor] AppOffsets J={0} V={1}",
            _appOffsets.JudgmentOffsetMs, _appOffsets.VisualOffsetMs));
    }

    /// <summary>曲別の判定オフセットを適用する。StartSong より前に呼ぶ。</summary>
    public void ApplyPerSongOffset(PerSongOffset offset)
    {
        _perSongOffset = offset?.Clamped();
        Debug.Log(string.Format("[AudioConductor] PerSongOffset {0}={1}",
            _perSongOffset?.SongId ?? "null",
            _perSongOffset?.JudgmentOffsetMs ?? 0));
    }

    // ── Playback API ──────────────────────────────────────────────────────────

    /// <summary>曲を開始する。<paramref name="prerollSec"/> の間は SongTimeMs が負となり、視覚初期化の猶予になる。</summary>
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

    /// <summary>再生を一時停止し、現在の曲時刻を保持する。</summary>
    public void Pause()
    {
        if (!_isPlaying) return;
        _pausedSongTimeMs = SongTimeMs;   // already scaled by _playbackSpeed
        _audioSource.Pause();
        _isPlaying = false;
        _isPaused  = true;
    }

    /// <summary>一時停止位置から再生を再開する(<paramref name="prerollSec"/> の猶予付き)。</summary>
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

    /// <summary>現在の曲位置を保ったまま再生速度を変更する(AudioSource.pitch を調整し基準時刻を再計算)。0.1〜4.0 にクランプ。</summary>
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

    /// <summary>再生を停止し、内部状態をリセットする。</summary>
    public void Stop()
    {
        _audioSource.Stop();
        _isPlaying        = false;
        _isPaused         = false;
        _dspStartTime     = 0.0;
        _pausedSongTimeMs = 0.0;
    }

    // ── Legacy API (Obsolete) ─────────────────────────────────────────────────

    /// <summary>[非推奨] アプリ判定オフセット(ms)。<see cref="ApplyAppOffsets"/> を使うこと。</summary>
    [System.Obsolete("Use ApplyAppOffsets(AppOffsetSettings)")]
    public int GlobalJudgmentOffsetMs
    {
        get => _appOffsets.JudgmentOffsetMs;
        set => ApplyAppOffsets(new AppOffsetSettings
            { JudgmentOffsetMs = value, VisualOffsetMs = _appOffsets.VisualOffsetMs });
    }

    /// <summary>[非推奨] アプリ映像オフセット(ms)。<see cref="ApplyAppOffsets"/> を使うこと。</summary>
    [System.Obsolete("Use ApplyAppOffsets(AppOffsetSettings)")]
    public int GlobalVisualOffsetMs
    {
        get => _appOffsets.VisualOffsetMs;
        set => ApplyAppOffsets(new AppOffsetSettings
            { JudgmentOffsetMs = _appOffsets.JudgmentOffsetMs, VisualOffsetMs = value });
    }

    /// <summary>[非推奨] 曲別判定オフセット(ms)。<see cref="ApplyPerSongOffset"/> を使うこと。</summary>
    [System.Obsolete("Use ApplyPerSongOffset(PerSongOffset)")]
    public int PerSongOffsetMs
    {
        get => _perSongOffset?.JudgmentOffsetMs ?? 0;
        set => ApplyPerSongOffset(new PerSongOffset
            { SongId = _perSongOffset?.SongId ?? "", JudgmentOffsetMs = value });
    }
}
