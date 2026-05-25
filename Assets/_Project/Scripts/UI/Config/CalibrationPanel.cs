using System.Collections.Generic;
using Domain.Calibration;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// タップ計測による自動オフセット推定パネル。
/// BPM 120 で 16 ビートのクリック音を再生し、ユーザーは Space キーで音にあわせてタップする。
/// </summary>
/// <remarks>
/// 最初の <see cref="PrerollBeats"/> ビートは準備カウントとして計測対象外。
/// 残り <see cref="SampleBeats"/> ビートのタップを <see cref="OffsetEstimator"/> に渡して
/// 推奨判定オフセットを算出する。
/// </remarks>
public class CalibrationPanel : MonoBehaviour
{
    /// <summary>キャリブレーション用ビート周期(秒)。BPM 120 相当。</summary>
    public const double BpmPeriodSec = 0.5;
    /// <summary>計測前の準備ビート数(この間は計測しない)。</summary>
    public const int    PrerollBeats = 4;
    /// <summary>計測対象のビート数。</summary>
    public const int    SampleBeats  = 12;
    /// <summary>総ビート数(準備 + 計測)。</summary>
    public const int    TotalBeats   = PrerollBeats + SampleBeats;

    [Header("Panel")]
    [SerializeField] GameObject _root;
    [SerializeField] Button     _closeButton;

    [Header("Idle State")]
    [SerializeField] GameObject _idleGroup;
    [SerializeField] Button     _startButton;
    [SerializeField] TextMeshProUGUI _instructionText;

    [Header("Running State")]
    [SerializeField] GameObject _runningGroup;
    [SerializeField] TextMeshProUGUI _beatCounterText;
    [SerializeField] Slider     _progressBar;

    [Header("Result State")]
    [SerializeField] GameObject _resultGroup;
    [SerializeField] TextMeshProUGUI _resultText;
    [SerializeField] Button     _applyButton;
    [SerializeField] Button     _retryButton;
    [SerializeField] Button     _cancelButton;

    // ── State ─────────────────────────────────────────────────────────────────

    enum Phase { Idle, Running, Completed }
    Phase _phase = Phase.Idle;

    AudioSource[] _beatSources;
    AudioClip     _clickClip;
    double        _firstBeatDsp;          // 最初のビートが鳴る dspTime
    readonly List<double> _samples = new List<double>();
    int            _nextExpectedBeat;     // 次にタップを期待する beat index
    OffsetEstimator.Result _lastResult;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Start()
    {
        _clickClip = GenerateClickClip();

        if (_startButton  != null) _startButton.onClick.AddListener(BeginSession);
        if (_closeButton  != null) _closeButton.onClick.AddListener(Close);
        if (_applyButton  != null) _applyButton.onClick.AddListener(ApplyAndClose);
        if (_retryButton  != null) _retryButton.onClick.AddListener(BeginSession);
        if (_cancelButton != null) _cancelButton.onClick.AddListener(Close);

        if (_instructionText != null)
            _instructionText.text =
                "Space キーをクリック音に合わせて押してください。\n" +
                "最初の " + PrerollBeats + " ビートは準備カウントです。\n" +
                "合計 " + TotalBeats + " ビート(約 " + (TotalBeats * BpmPeriodSec) + " 秒)";

        ShowIdle();
    }

    void OnDisable()
    {
        CleanupBeatSources();
        _phase = Phase.Idle;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>キャリブレーションパネルを開いてアイドル状態を表示する。</summary>
    public void Open()
    {
        if (_root != null) _root.SetActive(true);
        ShowIdle();
    }

    /// <summary>パネルを閉じ、計測用ビート音源を破棄してアイドルに戻す。</summary>
    public void Close()
    {
        CleanupBeatSources();
        _phase = Phase.Idle;
        if (_root != null) _root.SetActive(false);
    }

    // ── Session control ───────────────────────────────────────────────────────

    void BeginSession()
    {
        CleanupBeatSources();
        _samples.Clear();
        _nextExpectedBeat = 0;

        // 1.0 秒先から最初のビートを鳴らす(プレロード余裕)
        _firstBeatDsp = AudioSettings.dspTime + 1.0;

        // 各ビートに専用 AudioSource を割り当てて PlayScheduled で予約
        _beatSources = new AudioSource[TotalBeats];
        for (int i = 0; i < TotalBeats; i++)
        {
            var go = new GameObject("CalibBeat_" + i);
            go.transform.SetParent(transform, worldPositionStays: false);
            var src = go.AddComponent<AudioSource>();
            src.clip          = _clickClip;
            src.playOnAwake   = false;
            src.spatialBlend  = 0f;
            src.PlayScheduled(_firstBeatDsp + i * BpmPeriodSec);
            _beatSources[i] = src;
        }

        _phase = Phase.Running;
        ShowRunning();
    }

    void Update()
    {
        if (_phase != Phase.Running) return;

        double now = AudioSettings.dspTime;
        double elapsed = now - _firstBeatDsp;  // 最初のビート前は負

        // Beat カウント表示更新
        int currentBeat = Mathf.Clamp(
            Mathf.FloorToInt((float)(elapsed / BpmPeriodSec)) + 1,
            0, TotalBeats);
        if (_beatCounterText != null)
            _beatCounterText.text = "Beat " + currentBeat + " / " + TotalBeats;
        if (_progressBar != null)
            _progressBar.value = Mathf.Clamp01((float)(elapsed / (TotalBeats * BpmPeriodSec)));

        // タップ検知(Space キー)
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            RecordTap(now);
        }

        // 全ビート終了判定(最後のビートから 1.0 秒の余韻を取って終了)
        if (elapsed >= TotalBeats * BpmPeriodSec + 1.0)
        {
            CompleteSession();
        }
    }

    void RecordTap(double tapDsp)
    {
        // 最近接ビートを探す
        double elapsed = tapDsp - _firstBeatDsp;
        int nearestBeat = Mathf.RoundToInt((float)(elapsed / BpmPeriodSec));
        if (nearestBeat < 0 || nearestBeat >= TotalBeats) return;
        if (nearestBeat < PrerollBeats) return;  // 準備カウント中は記録しない

        double beatTime = _firstBeatDsp + nearestBeat * BpmPeriodSec;
        double deltaMs  = (tapDsp - beatTime) * 1000.0;
        _samples.Add(deltaMs);
    }

    void CompleteSession()
    {
        CleanupBeatSources();
        _phase = Phase.Completed;

        _lastResult = OffsetEstimator.Estimate(_samples);
        ShowResult(_lastResult);
    }

    async void ApplyAndClose()
    {
        if (!_lastResult.Success) return;

        var repo = RepositoryService.Instance?.Offsets;
        if (repo == null) { Close(); return; }

        var profile = RepositoryService.Instance.ActiveProfile;
        if (profile == null) { Close(); return; }

        var updated = new DeviceProfile
        {
            ProfileId           = profile.ProfileId,
            DisplayName         = profile.DisplayName,
            OsDeviceName        = profile.OsDeviceName,
            IsAutoSwitchEnabled = profile.IsAutoSwitchEnabled,
            Offsets = new AppOffsetSettings
            {
                JudgmentOffsetMs = _lastResult.RecommendedOffsetMs,
                VisualOffsetMs   = profile.Offsets.VisualOffsetMs,
            },
            CreatedAtUnixMs = profile.CreatedAtUnixMs,
            UpdatedAtUnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        bool ok = await repo.SaveProfileAsync(updated);
        if (ok) await RepositoryService.Instance.SetActiveProfileAsync(updated.ProfileId);

        Close();
    }

    // ── UI state transitions ─────────────────────────────────────────────────

    void ShowIdle()
    {
        if (_idleGroup    != null) _idleGroup.SetActive(true);
        if (_runningGroup != null) _runningGroup.SetActive(false);
        if (_resultGroup  != null) _resultGroup.SetActive(false);
    }

    void ShowRunning()
    {
        if (_idleGroup    != null) _idleGroup.SetActive(false);
        if (_runningGroup != null) _runningGroup.SetActive(true);
        if (_resultGroup  != null) _resultGroup.SetActive(false);
        if (_progressBar  != null) _progressBar.value = 0f;
        if (_beatCounterText != null) _beatCounterText.text = "Beat 0 / " + TotalBeats;
    }

    void ShowResult(OffsetEstimator.Result r)
    {
        if (_idleGroup    != null) _idleGroup.SetActive(false);
        if (_runningGroup != null) _runningGroup.SetActive(false);
        if (_resultGroup  != null) _resultGroup.SetActive(true);

        if (_resultText != null)
        {
            if (r.Success)
            {
                _resultText.text = string.Format(
                    "推奨判定オフセット: {0:+0;-0;0} ms\n\n" +
                    "計測サンプル: {1} 件 (除外: {2} 件)\n" +
                    "ばらつき: ±{3:F1} ms",
                    r.RecommendedOffsetMs, r.AcceptedCount, r.RejectedCount, r.StdDevMs);
            }
            else
            {
                _resultText.text = "計測失敗: " + r.FailureReason +
                                   "\n\n「再測定」を押して、音にあわせて Space キーを正確に押してください。";
            }
        }

        if (_applyButton != null) _applyButton.interactable = r.Success;
    }

    // ── Click clip generator ─────────────────────────────────────────────────

    static AudioClip GenerateClickClip()
    {
        const int sampleRate = 44100;
        const float durationSec = 0.04f;
        int sampleCount = (int)(sampleRate * durationSec);
        var samples = new float[sampleCount];

        // 800Hz トーン + 指数減衰エンベロープ
        const float freq = 800f;
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 60f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope * 0.6f;
        }

        var clip = AudioClip.Create("CalibClick", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void CleanupBeatSources()
    {
        if (_beatSources == null) return;
        foreach (var src in _beatSources)
        {
            if (src == null) continue;
            src.Stop();
            Destroy(src.gameObject);
        }
        _beatSources = null;
    }
}
