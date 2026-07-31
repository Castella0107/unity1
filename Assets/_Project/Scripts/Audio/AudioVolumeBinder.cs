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

    /// <summary>音量設定の変更通知。AudioConductor が購読して再生中の楽曲音量へ即時反映する。</summary>
    public static event System.Action VolumeChanged;

    // ミキサー未割当時の直接制御用の実効音量 (PlayerPrefs 基準なので生成順に依存しない)。
    // AudioTabController は PlayerPrefs へ書いてから binder を呼ぶため、常に最新値が取れる。
    /// <summary>楽曲の実効音量 (0〜1)。マスター% × 楽曲%。</summary>
    public static float CurrentMusic01 =>
        Pct(PlayerPrefs.GetFloat("Vol_Master", 80f)) * Pct(PlayerPrefs.GetFloat("Vol_Music", 90f));
    /// <summary>効果音の実効音量 (0〜1)。マスター% × 効果音%。</summary>
    public static float CurrentSfx01 =>
        Pct(PlayerPrefs.GetFloat("Vol_Master", 80f)) * Pct(PlayerPrefs.GetFloat("Vol_Sfx", 70f));
    static float Pct(float percent) => Mathf.Clamp01(percent / 100f);

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
        NotifyDirect();   // マスターは楽曲・効果音の両方に効く
    }

    /// <summary>音楽音量(0〜100%)を設定する。</summary>
    public void SetMusicVolume(float percent)
    {
        SetParam(MUSIC_PARAM, percent);
        NotifyDirect();
    }

    /// <summary>効果音音量(0〜100%)を設定する。</summary>
    public void SetSfxVolume(float percent)
    {
        SetParam(SFX_PARAM, percent);
        NotifyDirect();
    }

    // ミキサー未割当時の直接制御: 効果音 = HitSoundPlayer へ即時反映、
    // 楽曲 = VolumeChanged 経由で AudioConductor が反映。
    // (現行 _Persistent.unity は _mainMixer 未割当 + NewAudioMixer は公開パラメータ空のため、
    //  実運用はこの直接制御パスで動く。ミキサーを配線した場合は SetParam が本線になる)
    void NotifyDirect()
    {
        if (_mainMixer != null) return;
        HitSoundPlayer.Instance?.SetSourceVolume(CurrentSfx01);
        VolumeChanged?.Invoke();
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
