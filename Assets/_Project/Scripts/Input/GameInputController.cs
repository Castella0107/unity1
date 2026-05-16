using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Translates raw Input System callbacks into typed lane events with exact timing.
//
// Design notes
//   - OnLaneDown / OnLaneUp fire on the Input System's update, NOT in Update().
//     JudgmentTimeMs is captured in the callback itself to avoid frame-quantization.
//   - Press(behavior=2) fires performed on both press (value=1) and release (value=0).
//     We discriminate via ReadValue<float>() and route to Down or Up accordingly.
//     canceled fires right after the release performed; we use it for the Up path
//     and ignore the release performed to keep the logic symmetric.
//
// Usage
//   Subscribe to OnLaneDown / OnLaneUp before this component is enabled.
//   Unsubscribe when done (standard C# event hygiene).

/// <summary>
/// Unity Input System のコールバックを受け取り、レーン押下・離上イベントを正確なタイミングで
/// <see cref="OnLaneDown"/> / <see cref="OnLaneUp"/> として発行する MonoBehaviour。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class GameInputController : MonoBehaviour, IInputSource
{
    [SerializeField] private InputActionAsset _inputActions;

    public event Action<LaneRef, double> OnLaneDown;
    public event Action<LaneRef, double> OnLaneUp;

    private InputActionMap _gameplay;
    private InputAction[]  _laneActions; // indexed by (int)LaneId
    private bool           _mapEnabled;

    private void Awake()
    {
        if (_inputActions == null)
        {
            Debug.LogError("[GameInputController] InputActionAsset is not assigned.");
            return;
        }

        _gameplay    = _inputActions.FindActionMap("Gameplay", throwIfNotFound: true);
        _laneActions = new InputAction[6];

        _laneActions[(int)LaneId.Lane0] = _gameplay.FindAction("Lane0", throwIfNotFound: true);
        _laneActions[(int)LaneId.Lane1] = _gameplay.FindAction("Lane1", throwIfNotFound: true);
        _laneActions[(int)LaneId.Lane2] = _gameplay.FindAction("Lane2", throwIfNotFound: true);
        _laneActions[(int)LaneId.Lane3] = _gameplay.FindAction("Lane3", throwIfNotFound: true);
        _laneActions[(int)LaneId.FxL]   = _gameplay.FindAction("FxL",   throwIfNotFound: true);
        _laneActions[(int)LaneId.FxR]   = _gameplay.FindAction("FxR",   throwIfNotFound: true);

        BindLane(LaneId.Lane0);
        BindLane(LaneId.Lane1);
        BindLane(LaneId.Lane2);
        BindLane(LaneId.Lane3);
        BindLane(LaneId.FxL);
        BindLane(LaneId.FxR);
    }

    // Press(behavior=2): performed fires on both press (value=1) and release (value=0).
    // Use value > 0.5 for Down, canceled for Up (avoids double-fire on release).
    private void BindLane(LaneId lane)
    {
        var action = _laneActions[(int)lane];
        action.performed += ctx =>
        {
            if (ctx.ReadValue<float>() > 0.5f)
                RaiseLaneEvent(lane, isDown: true);
        };
        action.canceled += ctx => RaiseLaneEvent(lane, isDown: false);
    }

    private void RaiseLaneEvent(LaneId lane, bool isDown)
    {
        double t = AudioConductor.Instance != null
            ? AudioConductor.Instance.JudgmentTimeMs
            : 0.0;

        if (isDown)
        {
            HitSoundPlayer.Instance?.PlayTapClick();
            OnLaneDown?.Invoke((LaneRef)(int)lane, t);
        }
        else
        {
            OnLaneUp?.Invoke((LaneRef)(int)lane, t);
        }
    }

    private void OnEnable()
    {
        if (_gameplay != null && !_mapEnabled)
        {
            _gameplay.Enable();
            _mapEnabled = true;
        }
    }

    private void OnDisable()
    {
        if (_gameplay != null && _mapEnabled)
        {
            _gameplay.Disable();
            _mapEnabled = false;
        }
    }
}
