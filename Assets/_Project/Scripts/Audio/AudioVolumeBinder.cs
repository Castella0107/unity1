using UnityEngine;
using UnityEngine.Audio;

// DontDestroyOnLoad singleton that maps PlayerPrefs volume sliders
// (0–100 %) → AudioMixer dB params + direct AudioSource fallback.
// Works with or without a mixer assigned in the Inspector.
/// <summary>
/// PlayerPrefs のボリューム値（0〜100 %）を AudioMixer の dB パラメータへマッピングする DontDestroyOnLoad シングルトン。
/// AudioMixer が未割り当ての場合は AudioSource に直接フォールバックする。
/// </summary>
public class AudioVolumeBinder : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static AudioVolumeBinder Instance { get; private set; }

    [Header("Mixer (optional — assign after creating MainAudioMixer)")]
    [SerializeField] AudioMixer _mainMixer;

    // Exposed parameter names (must match names registered in AudioMixer Inspector).
    const string MASTER_PARAM = "MasterVolumeDb";
    const string MUSIC_PARAM  = "MusicVolumeDb";
    const string SFX_PARAM    = "SfxVolumeDb";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ApplyAllFromPrefs();
        Debug.Log("[AudioVolumeBinder] Ready");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>PlayerPrefs に保存された Master/Music/Sfx の各音量を読み込んで適用する。</summary>
    public void ApplyAllFromPrefs()
    {
        SetMasterVolume(PlayerPrefs.GetFloat("Vol_Master", 80f));
        SetMusicVolume(PlayerPrefs.GetFloat("Vol_Music",  90f));
        SetSfxVolume(PlayerPrefs.GetFloat("Vol_Sfx",    70f));
    }

    /// <summary>マスター音量(0〜100%)を設定する。</summary>
    public void SetMasterVolume(float percent)
    {
        SetParam(MASTER_PARAM, percent);
        // Master dB already scales all groups in the mixer.
        // No direct source override needed.
    }

    /// <summary>音楽音量(0〜100%)を設定する。</summary>
    public void SetMusicVolume(float percent)
    {
        SetParam(MUSIC_PARAM, percent);
    }

    /// <summary>効果音音量(0〜100%)を設定する。Mixer 未割り当て時は HitSoundPlayer に直接フォールバック。</summary>
    public void SetSfxVolume(float percent)
    {
        SetParam(SFX_PARAM, percent);
        // Direct fallback for HitSoundPlayer (when no mixer assigned).
        if (_mainMixer == null)
            HitSoundPlayer.Instance?.SetSourceVolume(Mathf.Clamp01(percent / 100f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // 0 % → −80 dB (silence), 100 % → 0 dB (unity gain).
    void SetParam(string param, float percent)
    {
        if (_mainMixer == null) return;
        float linear = Mathf.Clamp01(percent / 100f);
        float db     = linear > 0.0001f ? 20f * Mathf.Log10(linear) : -80f;
        _mainMixer.SetFloat(param, db);
    }
}
