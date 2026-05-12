using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// F3 toggles the debug overlay.  Off by default.
// Shows SongTime, JudgmentTime, recent hits, FPS.
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] AudioConductor  _conductor;
    [SerializeField] JudgmentSystem  _judgment;

    private bool          _visible;
    private readonly Queue<string> _hitLog = new Queue<string>();
    private readonly StringBuilder _sb     = new StringBuilder(512);

    private void OnEnable()
    {
        if (_judgment != null) _judgment.OnJudged += LogHit;
    }
    private void OnDisable()
    {
        if (_judgment != null) _judgment.OnJudged -= LogHit;
    }

    private void Start()
    {
        _visible = false;
        if (_text != null) _text.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
        {
            _visible = !_visible;
            if (_text != null) _text.gameObject.SetActive(_visible);
        }
        if (!_visible || _text == null) return;

        float fps = 1f / Time.unscaledDeltaTime;
        double songMs   = _conductor != null ? _conductor.SongTimeMs      : 0;
        double judgeMs  = _conductor != null ? _conductor.JudgmentTimeMs  : 0;
        int    active   = _judgment?.Aggregator?.CurrentCombo ?? 0;
        int    score    = _judgment?.Aggregator?.CurrentScore ?? 0;

        _sb.Length = 0;
        _sb.AppendFormat("FPS {0:F0}  |  F3: toggle\n", fps);
        _sb.AppendFormat("SongTime     {0,9:F1} ms\n", songMs);
        _sb.AppendFormat("JudgmentTime {0,9:F1} ms\n", judgeMs);
        _sb.AppendFormat("Combo {0,5}  Score {1,7}\n", active, score);
        _sb.AppendLine("-- Last Hits --");
        foreach (var e in _hitLog) _sb.AppendLine(e);

        _text.text = _sb.ToString();
    }

    private void LogHit(Judgment j, double deltaMs)
    {
        string dir = deltaMs < -1 ? "FAST" : deltaMs > 1 ? "LATE" : "    ";
        _hitLog.Enqueue(string.Format("{0,7:F1}ms {1,5} {2}", deltaMs, dir, j));
        while (_hitLog.Count > 10) _hitLog.Dequeue();
    }
}
