using UnityEngine;

/// <summary>
/// ⚠️ 廃止 (PLAYFIELD_SPEC.md §0)。FX レーンをベジェ曲線として実装したのは誤認識で、
/// 正しくは消失点 VP 中心の円弧セクター (FxSectorGeometry / FxLaneVisuals / FxArcNote)。
/// ノーツ・ビジュアルはもはやこのクラスを参照しない。シーン内の残骸は
/// Tools/Playfield/4 (PlayfieldRedesignBuilder.ApplyFxLanes) が撤去する。
///
/// (旧説明) FX レーン(FxL / FxR)のノーツが辿る曲線パスを定義するシーン内シングルトン。
/// 3 つの制御点(子 Transform: Hit / Control / Spawn)を通る 2 次ベジェ曲線で、
/// 奥(消失点付近の Spawn)から手前(判定位置の Hit)へ弧を描く経路を表す。
///
/// NoteController / HoldNoteController が <see cref="Instance"/> を参照して FX ノーツを
/// この曲線上に配置する。制御点はシーンビューで直接ドラッグして形を調整できる
/// (OnDrawGizmos で曲線をプレビュー表示する)。左右は既定でミラー生成する。
///
/// 座標系: 制御点 Transform の world 位置で評価し、world 位置・接線を返す。
/// パラメータ t は Hit=0(判定)〜 Spawn=1(出現)。判定通過後(t&lt;0)は外挿される。
/// </summary>
[ExecuteAlways]
public class FxLanePath : MonoBehaviour
{
    /// <summary>直近に有効化された FX パス。ノーツ側から参照する。未配置なら null(FX は直線にフォールバック)。</summary>
    public static FxLanePath Instance { get; private set; }

    [Header("Right side control points (world 位置で評価)")]
    [Tooltip("判定位置(t=0)。手前・画面右下あたり。")]
    [SerializeField] Transform _rightHit;
    [Tooltip("曲げの制御点(t=0.5 付近の膨らみ)。パスは必ずしもこの点を通らない。")]
    [SerializeField] Transform _rightControl;
    [Tooltip("出現位置(t=1)。奥・消失点付近。")]
    [SerializeField] Transform _rightSpawn;

    [Header("Left side")]
    [Tooltip("ON: 右側の制御点をこのオブジェクトのローカル X=0 面で反転して左側パスを生成する。")]
    [SerializeField] bool _mirrorLeftFromRight = true;
    [SerializeField] Transform _leftHit;
    [SerializeField] Transform _leftControl;
    [SerializeField] Transform _leftSpawn;

    [Header("向き")]
    [Tooltip("ON: ノーツを曲線の 3D 接線に完全整列(上下の傾きも反映)。OFF: 水平面内のヨーのみ(床に平らなまま)。")]
    [SerializeField] bool _fullOrient = false;

    [Header("Gizmo")]
    [SerializeField] bool  _drawGizmo   = true;
    [SerializeField] Color _gizmoColor  = new Color(0.1f, 0.1f, 0.1f, 1f); // 黒い線イメージ
    [SerializeField] int   _gizmoSteps  = 32;

    /// <summary>ノーツを 3D 接線へ完全整列するか(false ならヨーのみ)。</summary>
    public bool FullOrient => _fullOrient;

    void OnEnable()  { Instance = this; }
    void Awake()     { Instance = this; }
    void OnDisable() { if (Instance == this) Instance = null; }

    /// <summary>
    /// 指定 FX レーンの曲線上の位置と接線(dP/dt)を評価する。
    /// 制御点が未割り当ての場合や FX 以外のレーンでは false を返す。
    /// </summary>
    /// <param name="lane">FxL または FxR。</param>
    /// <param name="t">Hit=0 〜 Spawn=1。判定通過後は負値(外挿)。</param>
    public bool TryEvaluate(LaneRef lane, float t, out Vector3 pos, out Vector3 tangent)
    {
        pos = Vector3.zero;
        tangent = Vector3.forward;

        Transform hit, ctrl, spawn;
        bool mirror = false;

        if (lane == LaneRef.FxR)
        {
            hit = _rightHit; ctrl = _rightControl; spawn = _rightSpawn;
        }
        else if (lane == LaneRef.FxL)
        {
            if (_mirrorLeftFromRight)
            {
                hit = _rightHit; ctrl = _rightControl; spawn = _rightSpawn;
                mirror = true;
            }
            else
            {
                hit = _leftHit; ctrl = _leftControl; spawn = _leftSpawn;
            }
        }
        else
        {
            return false;
        }

        if (hit == null || ctrl == null || spawn == null) return false;

        Vector3 p0 = hit.position;   // t = 0
        Vector3 p1 = ctrl.position;  // control
        Vector3 p2 = spawn.position; // t = 1
        if (mirror) { p0 = MirrorPoint(p0); p1 = MirrorPoint(p1); p2 = MirrorPoint(p2); }

        float u = 1f - t;
        pos     = u * u * p0 + 2f * u * t * p1 + t * t * p2;
        tangent = 2f * u * (p1 - p0) + 2f * t * (p2 - p1); // dP/dt
        return true;
    }

    /// <summary>right→left ミラー: このオブジェクトのローカル X=0 面で点を反転する。</summary>
    Vector3 MirrorPoint(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.x = -local.x;
        return transform.TransformPoint(local);
    }

    void OnDrawGizmos()
    {
        if (!_drawGizmo) return;
        DrawLaneGizmo(LaneRef.FxR);
        DrawLaneGizmo(LaneRef.FxL);
    }

    void DrawLaneGizmo(LaneRef lane)
    {
        int steps = Mathf.Max(2, _gizmoSteps);
        Gizmos.color = _gizmoColor;
        Vector3 prev = Vector3.zero;
        bool have = false;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            if (!TryEvaluate(lane, t, out var p, out _)) return;
            if (have) Gizmos.DrawLine(prev, p);
            prev = p;
            have = true;
            Gizmos.DrawSphere(p, 0.05f);
        }
    }
}
