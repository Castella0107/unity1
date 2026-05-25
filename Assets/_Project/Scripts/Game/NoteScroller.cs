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

    /// <summary>Tap/FxTap のヒットを通知し、対応するノートを非表示にする。</summary>
    public void NotifyHit(int noteId, Judgment j)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.OnHit(j);
    }

    /// <summary>ホールド頭のヒットを通知する。IsHit は立てるが尾の消滅まで表示は維持する。</summary>
    public void NotifyHitHead(int noteId)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.SetHit();
    }

    /// <summary>Tap/FxTap のオートミスを通知し、ノートを非表示にする。</summary>
    public void NotifyMiss(int noteId)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.OnMiss();
    }

    /// <summary>ノートをヒット済みにマークする(内部用の旧 API)。</summary>
    public void MarkHit(NoteController note)
    {
        if (note != null) note.SetHit();
    }

    /// <summary>現在アクティブなノート一覧(読み取り専用)。</summary>
    public IReadOnlyList<NoteController> ActiveNotes => _activeNotes;

    /// <summary>指定レーンで <paramref name="timeMs"/> に最も近い未ヒットのアクティブノートを返す(無ければ null)。</summary>
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

    /// <summary>未ヒットのアクティブノートを列挙する。</summary>
    public System.Collections.Generic.IEnumerable<NoteController> GetUnhitNotes()
    {
        for (int i = 0; i < _activeNotes.Count; i++)
        {
            var n = _activeNotes[i];
            if (n != null && !n.IsHit && n.IsActive) yield return n;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>譜面でスクローラーを初期化する(時刻昇順に並べ替えて準備)。</summary>
    public void Initialize(ChartData chart)
    {
        Reset();
        _allNotes     = chart.Notes.OrderBy(n => n.TimeMs).ToList();
        _nextSpawnIdx = 0;
    }

    /// <summary>全アクティブノートをプールに返却し、内部状態をリセットする。</summary>
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
