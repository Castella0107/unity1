using System.Collections.Generic;

// Static layout constants shared by NoteController, HoldNoteController, and LaneVisuals.
// Layout: 6 coplanar lanes spanning world X -3.0 to +3.0 (1 unit per lane).
// Outer lanes (FxL/FxR) are independent FX lanes flanking the 4 main lanes:
//   FxL | Lane0 | Lane1 | Lane2 | Lane3 | FxR  (matches the play-screen mockup).

/// <summary>
/// NoteController・HoldNoteController・LaneVisuals が共有するレイアウト定数を提供する静的クラス。
/// 6 本のレーンが同一平面に World X = -3.0 〜 +3.0 で横並びに配置される。
/// 外側の FxL・FxR は中央 4 本(Lane0〜Lane3)を挟む独立した FX レーン。
/// 各レーンの X 座標・ノート幅・判定ライン Z・スポーン/デスポーン Z を定義する。
/// </summary>
public static class LaneLayout
{
    private static readonly Dictionary<LaneRef, float> _x = new Dictionary<LaneRef, float>
    {
        { LaneRef.FxL,   -2.6f },   // FX lane is 1.2 wide → center sits 0.6 outside Lane0's edge (-2.0)
        { LaneRef.Lane0, -1.5f },
        { LaneRef.Lane1, -0.5f },
        { LaneRef.Lane2,  0.5f },
        { LaneRef.Lane3,  1.5f },
        { LaneRef.FxR,    2.6f },
    };

    /// <summary>レーンの中心 World X 座標を返す。</summary>
    public static float GetX(LaneRef lane) => _x[lane];

    /// <summary>レーンのノート幅を返す。隣接ノート間に隙間を残すためレーン幅の 96%。</summary>
    public static float GetNoteWidth(LaneRef lane)
        => (lane == LaneRef.FxL || lane == LaneRef.FxR) ? FxNoteWidth : MainNoteWidth;

    /// <summary>レーン領域の総幅(World、-3.2〜+3.2 = メイン4×1.0 + FX2×1.2)。</summary>
    public const float TotalWidth    = 6.4f;
    /// <summary>メインレーン 1 本の幅。</summary>
    public const float MainLaneWidth = 1.0f;
    /// <summary>FX レーンの幅(メインレーンの 1.2 倍)。</summary>
    public const float FxLaneWidth   = 1.2f;
    /// <summary>メインノートの幅。</summary>
    public const float MainNoteWidth = 0.96f;
    /// <summary>FX ノートの幅(FX レーン幅の 96% = メインノートの 1.2 倍)。</summary>
    public const float FxNoteWidth   = 1.152f;

    /// <summary>判定ラインの Z 座標(カメラ前方)。</summary>
    public const float JudgmentLineZ = -0.5f;
    /// <summary>ノート出現時の Z 座標。</summary>
    public const float NoteSpawnZ    = 22f;
    /// <summary>ノート消滅時の Z 座標(判定ラインの 2 ユニット奥)。</summary>
    public const float NoteDespawnZ  = -2.5f;
}
