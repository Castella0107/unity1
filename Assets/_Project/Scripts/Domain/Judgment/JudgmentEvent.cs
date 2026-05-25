// Unity-independent. No UnityEngine references allowed in this assembly.

/// <summary>
/// ノーツのイベント種別を表す列挙型。タップ・ホールド頭・ホールドティック・ホールド尾の4種類。
/// </summary>
public enum NoteKind { Tap, HoldHead, HoldTick, HoldTail }

/// <summary>
/// 判定エンジンが発行する判定イベントを表す読み取り専用構造体。
/// ノーツID・種別・レーン・判定結果・タイミング差分・発生時刻・コンボ数・自動ミスフラグを含む。
/// </summary>
public readonly struct JudgmentEvent
{
    /// <summary>対象ノーツのID。</summary>
    public readonly int      NoteId;
    /// <summary>イベント種別(タップ/ホールド頭/ティック/尾)。</summary>
    public readonly NoteKind Kind;
    /// <summary>対象レーン。</summary>
    public readonly LaneRef  Lane;
    /// <summary>判定結果。</summary>
    public readonly Judgment Judgment;
    /// <summary>タイミング差(入力時刻 - ノーツ時刻)。Miss/Tick/オートTail では 0。</summary>
    public readonly double   DeltaMs;
    /// <summary>イベント発生時刻(ms)。</summary>
    public readonly double   TimeMs;
    /// <summary>このイベント後のコンボ数。</summary>
    public readonly int      Combo;
    /// <summary>オートミス(掃引による未入力ミス)か。</summary>
    public readonly bool     IsAutoMiss;

    /// <summary>全フィールドを指定して判定イベントを生成する。</summary>
    public JudgmentEvent(
        int noteId, NoteKind kind, LaneRef lane, Judgment judgment,
        double deltaMs, double timeMs, int combo, bool isAutoMiss = false)
    {
        NoteId     = noteId;
        Kind       = kind;
        Lane       = lane;
        Judgment   = judgment;
        DeltaMs    = deltaMs;
        TimeMs     = timeMs;
        Combo      = combo;
        IsAutoMiss = isAutoMiss;
    }
}
