// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// ノーツの種別を表す列挙型。通常タップ・ホールド・FXタップ・FXホールドの4種類。
/// </summary>
public enum NoteType { Tap, Hold, FxTap, FxHold }

// LaneRef is the Domain-layer lane identifier (mirrors LaneId in the Input layer).
// Kept separate so the Domain assembly has no dependency on the Input assembly.
/// <summary>
/// ドメイン層のレーン識別子。通常レーン4つ（Lane0〜Lane3）とFXレーン2つ（FxL・FxR）を表す列挙型。
/// </summary>
public enum LaneRef  { Lane0, Lane1, Lane2, Lane3, FxL, FxR }

/// <summary>
/// 1つのノーツの情報を保持するドメインモデル。種別・レーン・タイミング（ミリ秒）・ホールド持続時間を含む。
/// </summary>
public class NoteData
{
    public int      Id;
    public NoteType Type;
    public LaneRef  Lane;
    public double   TimeMs;
    public double   DurationMs; // Hold / FxHold only
}
