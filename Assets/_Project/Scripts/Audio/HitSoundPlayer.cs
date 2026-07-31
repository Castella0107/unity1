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
    const string PrefNoteSound = "NoteSoundIdx";   // 0=スタンダード (air-chart と同じサンプル)、1=クリック (シンセ)

    /// <summary>ノーツ音の選択 (0=スタンダード/air-chart サンプル、1=クリック/シンセ)。コンフィグから変更。</summary>
    public static int NoteSoundIdx
    {
        get => PlayerPrefs.GetInt(PrefNoteSound, 0);
        set { PlayerPrefs.SetInt(PrefNoteSound, value); PlayerPrefs.Save(); }
    }

    AudioClip _airchartClip;   // Resources/SFX/note_hit_airchart (譜面エディタと同一の標準ノーツ音)

    const int     TapVoices = 8;   // タップ音の最大同時発音 (ラウンドロビン)
    AudioSource[] _tapPool;
    int           _tapPoolIdx;

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
        _source.volume        = AudioVolumeBinder.CurrentSfx01;   // 生成順に依存せず設定を反映
        if (_sfxGroup != null) _source.outputAudioMixerGroup = _sfxGroup;

        // タップ音専用のラウンドロビンプール (K 報告 2026-07-30: 16分連打でタップ音が消える対策)。
        // PlayOneShot の積み重ねはボイス上限超過時に新しい発音ごと間引かれることがあるため、
        // 固定 8 ボイスを順番に使い回し「最新の打鍵は必ず鳴る」ことを保証する
        // (最も古いボイスを止めて使うので同時発音も 8 に上限され、音割れも防ぐ)。
        _tapPool = new AudioSource[TapVoices];
        for (int i = 0; i < TapVoices; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f;
            src.priority     = 64;   // 既定 128 より高優先 (打鍵フィードバックは間引かれたくない)
            src.volume       = _source.volume;
            if (_sfxGroup != null) src.outputAudioMixerGroup = _sfxGroup;
            _tapPool[i] = src;
        }

        _library = new HitSoundLibrary();
        _library.GenerateAll();

        // 標準ノーツ音: air-chart (譜面エディタ) に埋め込まれているものと同一のサンプル。
        // 読み込めない場合は従来のシンセクリックへフォールバック。
        _airchartClip = Resources.Load<AudioClip>("SFX/note_hit_airchart");
        if (_airchartClip == null)
            Debug.LogWarning("[HitSoundPlayer] Resources/SFX/note_hit_airchart が見つかりません — シンセクリックを使用");

        Debug.Log("[HitSoundPlayer] Ready");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>キー押下時に(判定前に)即座にノーツ音を鳴らす。
    /// 標準 (idx=0) は air-chart と同一サンプル、idx=1 は従来のシンセクリック。</summary>
    public void PlayTapClick()
    {
        if (!_enableTapClick) return;
        AudioClip clip = (NoteSoundIdx == 0 && _airchartClip != null) ? _airchartClip : _library?.TapClick;
        if (clip == null) return;

        // ラウンドロビン: 最も古いボイスを止めて再生 (連打でも最新の打鍵が必ず鳴る)
        var src = _tapPool[_tapPoolIdx];
        _tapPoolIdx = (_tapPoolIdx + 1) % TapVoices;
        src.Stop();
        src.clip   = clip;
        src.volume = _source.volume * _tapClickVolume;
        src.Play();
    }

    /// <summary>ノーツ音の試聴 (コンフィグの選択変更時に 1 回鳴らす)。ON/OFF 設定は無視して鳴らす。</summary>
    public void PreviewNoteSound(int idx)
    {
        if (idx == 0 && _airchartClip != null) { _source.PlayOneShot(_airchartClip, _tapClickVolume); return; }
        if (_library?.TapClick != null) _source.PlayOneShot(_library.TapClick, _tapClickVolume);
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
        if (_tapPool != null)
            foreach (var src in _tapPool)
                if (src != null) src.volume = linear01 * _tapClickVolume;
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
