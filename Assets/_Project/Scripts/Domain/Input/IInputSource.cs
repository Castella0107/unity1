using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Uses LaneRef (Domain type) so this interface can live entirely in the Domain assembly.
// GameInputController (Unity layer) implements this by casting LaneId → LaneRef.
/// <summary>
/// レーン入力イベント（押下・離上）を提供するインターフェース。
/// ドメイン層の LaneRef を使用し、Unity 層への依存を持たない。
/// </summary>
public interface IInputSource
{
    /// <summary>レーン押下時に発火(引数: レーン, 入力時刻ms)。</summary>
    event Action<LaneRef, double> OnLaneDown;
    /// <summary>レーン離上時に発火(引数: レーン, 入力時刻ms)。</summary>
    event Action<LaneRef, double> OnLaneUp;
}
