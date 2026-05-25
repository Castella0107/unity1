using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPMに同期したグリッドオーバーレイをゲームプレイ中に制御する永続シングルトン。
/// _Persistent.unity の BeatGridCanvas にアタッチし、GamePlayController から BindGamePlay / Unbind を呼び出す。
/// </summary>
// Persistent singleton that drives the BPM-synced grid overlay during GamePlay.
// Attach to BeatGridCanvas in _Persistent.unity.
/// <summary>
/// 拍に合わせて背景グリッドをパルスさせるシングルトン。GamePlay 以外では Canvas を無効化し、
/// GamePlayController が BindGamePlay / Unbind で制御する。
/// </summary>
public class BeatGridController : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static BeatGridController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] Canvas    _canvas;
    [SerializeField] RawImage  _gridImage;
    [SerializeField] Material  _gridMaterial;

    [Header("Pulse Settings")]
    [Tooltip("Peak brightness multiplier added on the beat (0 = no pulse)")]
    [SerializeField] float _pulseStrength  = 0.4f;
    [Tooltip("Exponential decay time constant in ms (smaller = sharper pulse)")]
    [SerializeField] float _pulseDecayMs   = 200f;
    [Tooltip("Grid scale amplitude on beat (1 + amplitude at peak)")]
    [SerializeField] float _scaleAmplitude = 0.05f;

    AudioConductor _conductor;
    BpmTimeline    _bpm;
    int            _lastBeatIndex  = -1;
    double         _lastBeatTimeMs = -1000.0;
    bool           _active;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetCanvasEnabled(false);

        // Restore saved intensity
        float saved = PlayerPrefs.GetFloat("BgEffectsIntensity", 100f) / 100f;
        SetUserIntensity(saved);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        // Reset shared material to defaults so next play starts clean
        if (_gridMaterial != null)
        {
            _gridMaterial.SetFloat("_PulseIntensity", 1.0f);
            _gridMaterial.SetFloat("_GridScale",      1.0f);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>曲開始時に GamePlayController から呼ばれ、拍パルスを開始する。</summary>
    public void BindGamePlay(AudioConductor conductor, BpmTimeline bpm)
    {
        _conductor      = conductor;
        _bpm            = bpm;
        _lastBeatIndex  = -1;
        _lastBeatTimeMs = -1000.0;
        _active         = true;
        SetCanvasEnabled(true);
    }

    /// <summary>曲終了/シーン退出時に呼ばれ、パルスを停止してマテリアルを既定値へ戻す。</summary>
    public void Unbind()
    {
        _conductor = null;
        _bpm       = null;
        _active    = false;
        SetCanvasEnabled(false);

        if (_gridMaterial != null)
        {
            _gridMaterial.SetFloat("_PulseIntensity", 1.0f);
            _gridMaterial.SetFloat("_GridScale",      1.0f);
        }
    }

    /// <summary>背景エフェクト強度(0〜1)を設定する。GameTabController のスライダーから呼ばれる。</summary>
    public void SetUserIntensity(float intensity01)
    {
        if (_gridMaterial != null)
            _gridMaterial.SetFloat("_UserIntensity", Mathf.Clamp01(intensity01));
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_active || _conductor == null || _bpm == null) return;
        if (!_conductor.IsPlaying) return;

        double songTimeMs = _conductor.SongTimeMs;
        if (songTimeMs < 0) return;

        // Beat index from current BPM
        double bpmNow         = _bpm.GetBpmAt(songTimeMs);
        double beatIntervalMs = 60000.0 / bpmNow;
        int    beatIndex      = (int)(songTimeMs / beatIntervalMs);

        if (beatIndex > _lastBeatIndex)
        {
            _lastBeatIndex  = beatIndex;
            _lastBeatTimeMs = beatIndex * beatIntervalMs;
        }

        // Exponential decay since last beat
        float sinceMs = (float)(songTimeMs - _lastBeatTimeMs);
        float decay   = Mathf.Exp(-sinceMs / _pulseDecayMs);

        if (_gridMaterial != null)
        {
            _gridMaterial.SetFloat("_PulseIntensity", 1.0f + decay * _pulseStrength);
            _gridMaterial.SetFloat("_GridScale",      1.0f + decay * _scaleAmplitude);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetCanvasEnabled(bool enabled)
    {
        if (_canvas != null) _canvas.enabled = enabled;
    }
}
