using UnityEngine;

// Bridges JudgmentSystem.OnJudged → particle / text / combo effects.
// JudgmentSystem calls NotifyLane() just before each OnJudged so particles
// know which lane to spawn at.

/// <summary>
/// JudgmentSystem.OnJudged イベントを受けて、パーティクル・コンボ表示・ヒットサウンドの各エフェクトを制御するブリッジクラス。
/// レーン情報は NotifyLane() で事前に受け取り、パーティクルのスポーン位置に使用する。
/// ホールドティックによる連続サウンドスパムを防ぐため、サウンド再生には最小間隔スロットルを設けている。
/// </summary>
public class JudgmentEffectsController : MonoBehaviour
{
    [SerializeField] JudgmentSystem      _judgmentSystem;
    [SerializeField] JudgmentParticlePool _particlePool;
    [SerializeField] JudgmentTextPopup   _textPopup;
    [SerializeField] ComboDisplay        _comboDisplay;

    LaneRef _pendingLane;
    bool    _hasLane;
    double  _lastJudgmentSoundMs = -1000.0;
    const double MIN_SOUND_INTERVAL_MS = 32.0;  // prevents Hold-tick spam

    void OnEnable()
    {
        if (_judgmentSystem != null)
            _judgmentSystem.OnJudged += HandleJudged;
    }

    void OnDisable()
    {
        if (_judgmentSystem != null)
            _judgmentSystem.OnJudged -= HandleJudged;
    }

    void Update()
    {
        if (_judgmentSystem?.Aggregator != null && _comboDisplay != null)
            _comboDisplay.SetCombo(_judgmentSystem.Aggregator.CurrentCombo);
    }

    // Called by JudgmentSystem immediately before OnJudged fires.
    public void NotifyLane(LaneRef lane)
    {
        _pendingLane = lane;
        _hasLane     = true;
    }

    void HandleJudged(Judgment j, double deltaMs)
    {
        // JudgmentTextPopup disabled — JudgmentDisplay shows text at screen center instead.

        // Particles — only when a lane was registered
        if (_particlePool != null && _hasLane)
        {
            var style    = JudgmentEffectStyleHelper.GetSaved();
            float mul    = JudgmentEffectStyleHelper.GetParticleMultiplier(style);
            if (j == Judgment.Miss) mul *= 0.5f;

            var pos = new Vector3(LaneLayout.GetX(_pendingLane), 0.1f, LaneLayout.JudgmentLineZ);
            _particlePool.Spawn(pos, JudgmentColors.Get(j), mul);
        }
        _hasLane = false;

        // Judgment sound — throttled to suppress Hold-tick rapid-fire
        double nowMs = Time.unscaledTimeAsDouble * 1000.0;
        if (nowMs - _lastJudgmentSoundMs >= MIN_SOUND_INTERVAL_MS)
        {
            HitSoundPlayer.Instance?.PlayJudgment(j);
            _lastJudgmentSoundMs = nowMs;
        }
    }
}
