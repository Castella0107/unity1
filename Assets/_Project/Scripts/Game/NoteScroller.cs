using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Reads a sorted NoteData list and manages note lifecycle:
//   1. Spawns notes from the pool when they enter the lookahead window.
//   2. Updates their scroll position every frame using AudioConductor.VisualTimeMs.
//   3. Returns notes to the pool after they pass the despawn threshold.
//
// All timing is based on AudioConductor.VisualTimeMs — no Time.deltaTime accumulation.

/// <summary>
/// ソート済み NoteData リストを元にノートのライフサイクルを管理するスクローラー。
/// ルックアヘッドウィンドウに入ったノートをプールからスポーンし、フレームごとに
/// AudioConductor.VisualTimeMs を基準にスクロール位置を更新、デスポーン閾値を超えたノートをプールへ返却する。
/// </summary>
public class NoteScroller : MonoBehaviour
{
    [SerializeField] AudioConductor _conductor;
    [SerializeField] NotePool       _pool;
    [SerializeField] float          _scrollSpeed = 10f;

    // At scrollSpeed=10, SPAWN_LOOKAHEAD_MS=2200 → notes appear at Z≈22 (NoteSpawnZ).
    private const double SPAWN_LOOKAHEAD_MS = 2200.0;
    private const double DESPAWN_AFTER_MS   =  500.0;

    private List<NoteData>                  _allNotes;
    private int                             _nextSpawnIdx;
    private List<NoteController>            _activeNotes = new List<NoteController>();
    private Dictionary<int, NoteController> _noteById    = new Dictionary<int, NoteController>();

    // ── Judgment notifications (NoteId-based, replaces direct NoteController access) ──

    // Tap/FxTap hit: deactivate the note.
    public void NotifyHit(int noteId, Judgment j)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.OnHit(j);
    }

    // Hold head hit: mark IsHit but keep the note visible until tail despawns.
    public void NotifyHitHead(int noteId)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.SetHit();
    }

    // Tap/FxTap auto-miss: deactivate the note.
    public void NotifyMiss(int noteId)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.OnMiss();
    }

    // ── Legacy API (kept for internal use; JudgmentSystem no longer calls these) ──

    public void MarkHit(NoteController note)
    {
        if (note != null) note.SetHit();
    }

    // ── Public read-only access ────────────────────────────────────────────────
    public IReadOnlyList<NoteController> ActiveNotes => _activeNotes;

    public NoteController FindNearestUnhitNote(LaneRef lane, double timeMs)
    {
        NoteController best     = null;
        double         bestDist = double.MaxValue;
        for (int i = 0; i < _activeNotes.Count; i++)
        {
            var n = _activeNotes[i];
            if (n == null || n.IsHit || !n.IsActive) continue;
            if (n.Data.Lane != lane) continue;
            double d = System.Math.Abs(timeMs - n.Data.TimeMs);
            if (d < bestDist) { bestDist = d; best = n; }
        }
        return best;
    }

    public System.Collections.Generic.IEnumerable<NoteController> GetUnhitNotes()
    {
        for (int i = 0; i < _activeNotes.Count; i++)
        {
            var n = _activeNotes[i];
            if (n != null && !n.IsHit && n.IsActive) yield return n;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void Initialize(ChartData chart)
    {
        Reset();
        _allNotes     = chart.Notes.OrderBy(n => n.TimeMs).ToList();
        _nextSpawnIdx = 0;
    }

    public void Reset()
    {
        foreach (var n in _activeNotes)
            if (n != null) _pool.Release(n);
        _activeNotes.Clear();
        _noteById.Clear();
        _nextSpawnIdx = 0;
    }

    // ── Frame update ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (_allNotes == null || _conductor == null) return;
        if (!_conductor.IsPlaying && !_conductor.IsPaused) return;

        double visualMs = _conductor.VisualTimeMs;

        // Spawn notes entering the lookahead window.
        while (_nextSpawnIdx < _allNotes.Count)
        {
            var noteData = _allNotes[_nextSpawnIdx];
            double dt    = noteData.TimeMs - visualMs;

            if (dt > SPAWN_LOOKAHEAD_MS) break;

            if (dt < -DESPAWN_AFTER_MS) { _nextSpawnIdx++; continue; }

            var ctrl = _pool.Acquire(noteData.Type);
            ctrl.Initialize(noteData);
            _activeNotes.Add(ctrl);
            _noteById[noteData.Id] = ctrl;
            _nextSpawnIdx++;
        }

        // Scroll active notes and despawn when they exit the visible range.
        for (int i = _activeNotes.Count - 1; i >= 0; i--)
        {
            var note = _activeNotes[i];
            if (note == null) { _activeNotes.RemoveAt(i); continue; }
            if (!note.IsActive)
            {
                _noteById.Remove(note.Data?.Id ?? -1);
                _pool.Release(note);
                _activeNotes.RemoveAt(i);
                continue;
            }

            bool isHold  = note.Data.Type == NoteType.Hold || note.Data.Type == NoteType.FxHold;
            double endMs = isHold ? note.Data.TimeMs + note.Data.DurationMs : note.Data.TimeMs;
            double dtEnd = endMs - visualMs;

            if (dtEnd < -DESPAWN_AFTER_MS)
            {
                _noteById.Remove(note.Data.Id);
                _pool.Release(note);
                _activeNotes.RemoveAt(i);
            }
            else
            {
                note.UpdatePosition(visualMs, _scrollSpeed);
            }
        }
    }
}
