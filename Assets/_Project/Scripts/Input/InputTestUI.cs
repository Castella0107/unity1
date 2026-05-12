using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Visual test harness for GameInputController.
//
// HOW TO TEST
//   1. Open Assets/_Project/Scenes/InputTest.unity and enter Play Mode.
//   2. Press D / F / J / K (main lanes) or Z / M (FX lanes).
//   3. The corresponding lane rectangle lights up while held and dims on release.
//   4. Each event is logged at the top with its JudgmentTimeMs.
//      If AudioConductor is not playing, time shows as 0.000 ms — that is correct.
//   5. Press [Start Song] (from AudioTestUI / separate setup) to get real timestamps.
//
// Frame-rate independence check:
//   Run at 60 fps and at 240 fps. The ms values between successive presses should
//   reflect actual elapsed time, not frame-snapped values.

public sealed class InputTestUI : MonoBehaviour
{
    [SerializeField] private GameInputController _controller;

    [Header("Lane visuals  (order: Lane0 Lane1 Lane2 Lane3 FxL FxR)")]
    [SerializeField] private Image[] _laneImages;

    [Header("Log")]
    [SerializeField] private TextMeshProUGUI _logText;

    // Dim colour for all lanes when unpressed
    private static readonly Color ColOff = new Color(0.15f, 0.16f, 0.22f, 1f);

    // Lit colours per lane (same order as LaneId enum)
    private static readonly Color[] ColOn =
    {
        new Color(0.95f, 0.35f, 0.35f, 1f),  // Lane0 – red
        new Color(0.35f, 0.95f, 0.35f, 1f),  // Lane1 – green
        new Color(0.35f, 0.55f, 0.95f, 1f),  // Lane2 – blue
        new Color(0.95f, 0.95f, 0.30f, 1f),  // Lane3 – yellow
        new Color(0.95f, 0.55f, 0.10f, 1f),  // FxL   – orange
        new Color(0.75f, 0.30f, 0.95f, 1f),  // FxR   – purple
    };

    private readonly List<string> _log = new List<string>(); // index 0 = newest
    private const int MaxLogLines = 10;

    private void OnEnable()
    {
        if (_controller == null) return;
        _controller.OnLaneDown += HandleDown;
        _controller.OnLaneUp   += HandleUp;
    }

    private void OnDisable()
    {
        if (_controller == null) return;
        _controller.OnLaneDown -= HandleDown;
        _controller.OnLaneUp   -= HandleUp;
    }

    private void HandleDown(LaneRef lane, double timeMs)
    {
        SetLane((int)lane, ColOn[(int)lane]);
        AddLog(timeMs, lane, "DOWN");
    }

    private void HandleUp(LaneRef lane, double timeMs)
    {
        SetLane((int)lane, ColOff);
        AddLog(timeMs, lane, "UP  ");
    }

    private void SetLane(int index, Color c)
    {
        if (_laneImages != null && index < _laneImages.Length && _laneImages[index] != null)
            _laneImages[index].color = c;
    }

    private void AddLog(double timeMs, LaneRef lane, string direction)
    {
        _log.Insert(0, string.Format("{0,10:F3} ms   {1,-6}   {2}", timeMs, lane, direction));
        if (_log.Count > MaxLogLines) _log.RemoveAt(MaxLogLines);
        if (_logText != null) _logText.text = string.Join("\n", _log);
    }
}
