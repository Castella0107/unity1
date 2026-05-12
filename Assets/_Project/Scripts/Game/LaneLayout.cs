using System.Collections.Generic;

// Static layout constants shared by NoteController, HoldNoteController, and LaneVisuals.
// Layout is CHUNITHM-style: 4 main lanes spanning world X -2.0 to +2.0 (1 unit per lane).
public static class LaneLayout
{
    private static readonly Dictionary<LaneRef, float> _x = new Dictionary<LaneRef, float>
    {
        { LaneRef.Lane0, -1.5f },
        { LaneRef.Lane1, -0.5f },
        { LaneRef.Lane2,  0.5f },
        { LaneRef.Lane3,  1.5f },
        { LaneRef.FxL,   -1.0f },
        { LaneRef.FxR,    1.0f },
    };

    public static float GetX(LaneRef lane) => _x[lane];

    // Note widths (96% of lane width to leave a thin gap between adjacent notes)
    public static float GetNoteWidth(LaneRef lane)
        => (lane == LaneRef.FxL || lane == LaneRef.FxR) ? FxNoteWidth : MainNoteWidth;

    // Overall dimensions
    public const float TotalWidth    = 4.0f;   // lane area spans -2.0 to +2.0
    public const float MainLaneWidth = 1.0f;   // one main lane
    public const float FxLaneWidth   = 2.0f;   // FX lane spans two main lanes
    public const float MainNoteWidth = 0.96f;
    public const float FxNoteWidth   = 1.96f;

    public const float JudgmentLineZ = -0.5f;   // in front of camera (z=-1.0); projects ~10-15% from screen bottom
    public const float NoteSpawnZ    = 22f;
    public const float NoteDespawnZ  = -2.5f;  // 2 units behind the new judgment line
}
