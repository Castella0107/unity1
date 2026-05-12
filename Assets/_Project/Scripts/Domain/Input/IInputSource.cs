using System;

// Unity-independent. No UnityEngine references allowed in this assembly.
// Uses LaneRef (Domain type) so this interface can live entirely in the Domain assembly.
// GameInputController (Unity layer) implements this by casting LaneId → LaneRef.
public interface IInputSource
{
    event Action<LaneRef, double> OnLaneDown;
    event Action<LaneRef, double> OnLaneUp;
}
