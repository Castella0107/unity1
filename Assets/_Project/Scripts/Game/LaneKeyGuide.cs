using TMPro;
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

    void Start()
    {
        TrySubscribe();   // input source may not exist at Awake
        PlaceFxLabels();
    }

    void OnRectTransformDimensionsChange() => PlaceFxLabels();   // 解像度変更に追従

    // ── S/L ラベルのカメラ追従 ──────────────────────────────────────────────
    // シーン遷移は加算ロードなので、Start 時点では旧シーンのカメラが Camera.main の
    // ままのことがあり、その投影で置くとラベルが画面外へ飛ぶ (K 報告 2026-08-01:
    // リトライで S/L 表示がどこかへ行く)。StageInitializer の視点適用 (ApplyCameraAngle)
    // も譜面の非同期ロード後で Start より遅い。どちらも「後からカメラが変わる」問題
    // なので、カメラの実体・姿勢・画角・画面サイズの変化を監視して置き直す。
    Camera     _placedCam;
    Vector3    _placedCamPos;
    Quaternion _placedCamRot;
    float      _placedFov;
    int        _placedW, _placedH;

    void LateUpdate()
    {
        var cam = FxSectorGeometry.Cam;
        if (cam == null) return;
        var t = cam.transform;
        if (cam == _placedCam && t.position == _placedCamPos && t.rotation == _placedCamRot
            && Mathf.Approximately(cam.fieldOfView, _placedFov)
            && Screen.width == _placedW && Screen.height == _placedH) return;

        _placedCam    = cam;
        _placedCamPos = t.position;
        _placedCamRot = t.rotation;
        _placedFov    = cam.fieldOfView;
        _placedW      = Screen.width;
        _placedH      = Screen.height;
        PlaceFxLabels();
    }

    /// <summary>
    /// S/L ラベルを FX ボタン (円環セグメント) の実際の中心へ置く。
    ///
    /// ボタンはワールド空間のメッシュ、ラベルは HUD キャンバスなので、
    /// 固定座標を焼き込むと 16:9 以外の画面比でズレる (K 報告 2026-07-31:
    /// ラベルが帯の中心でなく端に寄っていた)。
    /// ボタン中心の極座標 → ワールド → スクリーン → キャンバスへ毎回投影して合わせる。
    /// </summary>
    void PlaceFxLabels()
    {
        var cam = FxSectorGeometry.Cam;
        if (cam == null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var canvasRt = canvas.transform as RectTransform;
        if (canvasRt == null) return;

        const float thMid = (FxSectorGeometry.SectorThetaMin
                           + FxSectorGeometry.SectorThetaMax) * 0.5f;
        const float rMid  = (FxSectorGeometry.ButtonR0 + FxSectorGeometry.ButtonR1) * 0.5f;

        for (int i = 4; i < _keyChips.Length && i < 6; i++)
        {
            if (_keyChips[i] == null) continue;
            bool right = (i == 5);
            Vector3 world  = FxSectorGeometry.FloorPoint(right, thMid, rMid);
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) continue;   // カメラ後方は無視

            var cam4Canvas = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, screen, cam4Canvas, out var local))
            {
                var rt = _keyChips[i].rectTransform;
                rt.position = canvasRt.TransformPoint(local);
            }
        }
    }

    void TrySubscribe()
    {
        if (_subscribed) return;
        if (_input == null) _input = FindObjectOfType<GameInputController>();
        if (_input == null) return;
        _input.OnLaneDown += HandleDown;
        _input.OnLaneUp   += HandleUp;
        _subscribed = true;
        RefreshKeyLabels();   // Config のキーバインド変更をゲーム内チップラベルへ反映
    }

    // 各キーチップの子ラベルを、現在の実バインド (override 適用後) で更新する。
    // ラベルは HudSceneBuilder が静的文字列で焼き込んでいるため、実行時にここで上書きする。
    void RefreshKeyLabels()
    {
        var gic = _input as GameInputController;
        if (gic == null) return;
        for (int i = 0; i < _keyChips.Length; i++)
        {
            if (_keyChips[i] == null) continue;
            var lbl = _keyChips[i].GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = gic.GetLaneKeyDisplay(i);
        }
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
