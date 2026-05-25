using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 入力ソース(GameInputController)の押下/離上を購読し、画面下部のキーガイドチップと
/// 対応する3Dレーンハイライトを押下中のみ点灯させる。6 本のレーンはそれぞれ独立した
/// ハイライト列を持つ(FxL/FxR も自分の列のみ点灯)。
/// チップ・3Dハイライトのいずれか未割当でも、もう片方だけで動作する(null セーフ)。
/// </summary>
public class LaneKeyGuide : MonoBehaviour
{
    [Header("Key chips — indexed by LaneId (0=Lane0/D .. 3=Lane3/K, 4=FxL, 5=FxR)")]
    [SerializeField] Image[] _keyChips = new Image[6];

    [Header("Lane highlights — indexed by LaneId (0..3=Lane0..Lane3, 4=FxL, 5=FxR)")]
    [SerializeField] Renderer[] _laneHighlights = new Renderer[6];

    [Header("Colors")]
    [SerializeField] Color _chipIdleMain  = new Color(0.10f, 0.11f, 0.14f, 0.80f);
    [SerializeField] Color _chipIdleFx    = new Color(0.14f, 0.10f, 0.16f, 0.80f);
    [SerializeField] Color _chipPressMain = new Color(0.30f, 0.85f, 1.00f, 0.95f);
    [SerializeField] Color _chipPressFx   = new Color(0.80f, 0.35f, 1.00f, 0.95f);
    [SerializeField] Color _highlightColor   = Color.white;   // lit-lane tint (RGB only; alpha from _highlightOnAlpha)
    [SerializeField] float _highlightOnAlpha = 0.22f;         // peak alpha at the near edge; fades to 0 toward the back via the gradient texture

    IInputSource _input;
    readonly int[] _columnRefs = new int[6];   // ref-count per lane column
    bool    _subscribed;

    // Each lane lights its own dedicated column (6 independent lanes).
    static readonly int[][] LaneColumns =
    {
        new[]{0}, new[]{1}, new[]{2}, new[]{3},   // Lane0..3
        new[]{4}, new[]{5},                       // FxL, FxR
    };

    void Awake()
    {
        for (int c = 0; c < _laneHighlights.Length; c++)
            SetColumnAlpha(c, 0f);   // start fully transparent
        ResetChips();
    }

    void OnEnable() => TrySubscribe();
    void Start()    => TrySubscribe();   // input source may not exist at Awake

    void TrySubscribe()
    {
        if (_subscribed) return;
        if (_input == null) _input = FindObjectOfType<GameInputController>();
        if (_input == null) return;
        _input.OnLaneDown += HandleDown;
        _input.OnLaneUp   += HandleUp;
        _subscribed = true;
    }

    void OnDisable()
    {
        if (_input != null && _subscribed)
        {
            _input.OnLaneDown -= HandleDown;
            _input.OnLaneUp   -= HandleUp;
        }
        _subscribed = false;
    }

    void HandleDown(LaneRef lane, double t) => SetLane(lane, true);
    void HandleUp(LaneRef lane, double t)   => SetLane(lane, false);

    void SetLane(LaneRef lane, bool pressed)
    {
        int li = (int)lane;

        // Key chip
        if (li >= 0 && li < _keyChips.Length && _keyChips[li] != null)
        {
            bool fx = lane == LaneRef.FxL || lane == LaneRef.FxR;
            _keyChips[li].color = pressed ? (fx ? _chipPressFx : _chipPressMain)
                                          : (fx ? _chipIdleFx  : _chipIdleMain);
        }

        // 3D columns — ref-counted so releasing an FX key doesn't kill a column
        // still held by an overlapping main key (and vice versa).
        if (li >= 0 && li < LaneColumns.Length)
        {
            foreach (int c in LaneColumns[li])
            {
                _columnRefs[c] = Mathf.Max(0, _columnRefs[c] + (pressed ? 1 : -1));
                SetColumnAlpha(c, _columnRefs[c] > 0 ? _highlightOnAlpha : 0f);
            }
        }
    }

    void SetColumnAlpha(int c, float a)
    {
        if (c < 0 || c >= _laneHighlights.Length || _laneHighlights[c] == null) return;
        var col = _highlightColor;
        col.a = a;
        _laneHighlights[c].material.color = col;
    }

    void ResetChips()
    {
        for (int i = 0; i < _keyChips.Length; i++)
        {
            if (_keyChips[i] == null) continue;
            bool fx = i == (int)LaneRef.FxL || i == (int)LaneRef.FxR;
            _keyChips[i].color = fx ? _chipIdleFx : _chipIdleMain;
        }
    }
}
