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
    public static HitSoundPlayer Instance { get; private set; }

    [Header("Mixer (optional — set after creating MainAudioMixer)")]
    [SerializeField] AudioMixerGroup _sfxGroup;

    [Header("Behaviour")]
    [SerializeField] bool  _enableTapClick       = true;
    [SerializeField] bool  _enableJudgmentSounds = true;
    [SerializeField] float _tapClickVolume       = 1.0f;
    [SerializeField] float _judgmentVolume       = 0.85f;

    AudioSource     _source;
    HitSoundLibrary _library;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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

    /// Fire immediately when a key is pressed (before judgment).
    public void PlayTapClick()
    {
        if (!_enableTapClick || _library?.TapClick == null) return;
        _source.PlayOneShot(_library.TapClick, _tapClickVolume);
    }

    /// Fire when judgment resolves.
    public void PlayJudgment(Judgment j)
    {
        if (!_enableJudgmentSounds) return;
        var clip = _library?.GetForJudgment(j);
        if (clip == null) return;
        _source.PlayOneShot(clip, _judgmentVolume);
    }

    // Called by AudioVolumeBinder when SFX volume changes.
    public void SetSourceVolume(float linear01)
    {
        if (_source != null) _source.volume = linear01;
    }

    public void SetTapClickEnabled(bool enabled)       => _enableTapClick       = enabled;
    public void SetJudgmentSoundsEnabled(bool enabled) => _enableJudgmentSounds = enabled;
}
