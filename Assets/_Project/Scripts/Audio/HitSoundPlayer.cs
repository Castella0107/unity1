using UnityEngine;
using UnityEngine.Audio;

// DontDestroyOnLoad singleton for tap-click and judgment sounds.
// Assign _sfxGroup in the Inspector after creating MainAudioMixer.
// Null-safe: works without a mixer (volume defaults to AudioSource.volume).
/// <summary>
/// タップクリック音と判定効果音を再生する DontDestroyOnLoad シングルトン。
/// HitSoundLibrary で生成したクリップを AudioSource.PlayOneShot で再生し、
/// AudioMixer 未割り当て時は AudioSource のボリュームで直接制御する。
/// </summary>
public class HitSoundPlayer : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static HitSoundPlayer Instance { get; private set; }

    [Header("Mixer (optional — set after creating MainAudioMixer)")]
    [SerializeField] AudioMixerGroup _sfxGroup;

    [Header("Behaviour")]
    [SerializeField] bool  _enableTapClick       = true;
    [SerializeField] bool  _enableJudgmentSounds = true;
    [SerializeField] float _tapClickVolume       = 1.0f;
    [SerializeField] float _judgmentVolume       = 0.85f;

    // PlayerPrefs キー (簡易コンフィグ PLAY OPTIONS から切替・永続化)
    const string PrefTapClick  = "HitSoundTap";
    const string PrefJudgment  = "HitSoundJudgment";

    AudioSource     _source;
    HitSoundLibrary _library;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 保存済み設定を復元 (未保存ならインスペクタ既定値)
        _enableTapClick       = PlayerPrefs.GetInt(PrefTapClick, _enableTapClick       ? 1 : 0) == 1;
        _enableJudgmentSounds = PlayerPrefs.GetInt(PrefJudgment, _enableJudgmentSounds ? 1 : 0) == 1;

        _source               = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake   = false;
        _source.spatialBlend  = 0f;
        if (_sfxGroup != null) _source.outputAudioMixerGroup = _sfxGroup;

        _library = new HitSoundLibrary();
        _library.GenerateAll();

        Debug.Log("[HitSoundPlayer] Ready");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>キー押下時に(判定前に)即座にタップクリック音を鳴らす。</summary>
    public void PlayTapClick()
    {
        if (!_enableTapClick || _library?.TapClick == null) return;
        _source.PlayOneShot(_library.TapClick, _tapClickVolume);
    }

    /// <summary>判定確定時に対応する効果音を鳴らす。</summary>
    public void PlayJudgment(Judgment j)
    {
        if (!_enableJudgmentSounds) return;
        var clip = _library?.GetForJudgment(j);
        if (clip == null) return;
        _source.PlayOneShot(clip, _judgmentVolume);
    }

    /// <summary>効果音 AudioSource のボリューム(0〜1)を設定する。AudioVolumeBinder から呼ばれる。</summary>
    public void SetSourceVolume(float linear01)
    {
        if (_source != null) _source.volume = linear01;
    }

    /// <summary>タップクリック音の有効/無効を切り替える(PlayerPrefs に永続化)。</summary>
    public void SetTapClickEnabled(bool enabled)
    {
        _enableTapClick = enabled;
        PlayerPrefs.SetInt(PrefTapClick, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>判定効果音の有効/無効を切り替える(PlayerPrefs に永続化)。</summary>
    public void SetJudgmentSoundsEnabled(bool enabled)
    {
        _enableJudgmentSounds = enabled;
        PlayerPrefs.SetInt(PrefJudgment, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
