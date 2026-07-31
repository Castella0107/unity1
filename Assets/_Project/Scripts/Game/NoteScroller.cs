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
    [SerializeField] float          _scrollSpeed = 4.5f;

    // note Z = JudgmentLineZ + dt/1000 * scrollSpeed.
    // Spawn/despawn windows are derived from _scrollSpeed so notes always appear at
    // NoteSpawnZ and disappear at NoteDespawnZ for any HiSpeed (no pop-in at low speed).

    /// <summary>
    /// HiSpeed 設定値 1.0 あたりのワールド速度係数。従来は設定値 = ワールド速度そのままだったが
    /// 体感が弱く「設定 20 が他ゲームの 12 相当」(K 実感) だったため 20/12 倍で補正した。
    /// それでもまだ効きが弱いとの指摘 (K 2026-07-31) で倍率を追加した。
    /// 1.8 倍 → 2.0 倍と調整し、最終的に 3.0 倍 (20/12 × 3.0 = 5.0) にしている。
    /// ノーツ・小節線 (BeatLineScroller) は共に ScrollSpeed を参照するので同期は保たれる。
    /// スポーン/デスポーン範囲も _scrollSpeed から逆算しているため、速度を上げても
    /// ノーツの湧き出し位置は変わらない (NoteSpawnZ で一定)。
    /// </summary>
    public const float HiSpeedScale = 20f / 12f * 3.0f;

    /// <summary>現在のスクロール速度(World Z units / 秒)。HiSpeed 設定値 × HiSpeedScale。</summary>
    public float ScrollSpeed => _scrollSpeed;

    /// <summary>スクロール速度(HiSpeed 設定値 0.5〜20)を設定する。プレイ開始前に呼ぶ。</summary>
    public void SetScrollSpeed(float speed) => _scrollSpeed = Mathf.Clamp(speed, 0.5f, 20f) * HiSpeedScale;

    private List<NoteData>                  _allNotes;
    private ScrollSpeedTimeline             _speed;        // "speed" イベントによるスクロール演出(視覚専用)
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

    /// <summary>ホールドの「取れていない」視覚状態 (若干グレー) を切り替える。</summary>
    public void NotifyHoldDropped(int noteId, bool dropped)
    {
        if (_noteById.TryGetValue(noteId, out var ctrl))
            ctrl.SetHoldDropped(dropped);
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

    /// <summary>現在のスクロール速度演出(speed イベント)。BeatLineScroller 等が共有する。</summary>
    public ScrollSpeedTimeline Speed => _speed;

    /// <summary>譜面でスクローラーを初期化する(時刻昇順に並べ替えて準備)。</summary>
    public void Initialize(ChartData chart)
    {
        Reset();
        _allNotes     = chart.Notes.OrderBy(n => n.TimeMs).ToList();
        _speed        = new ScrollSpeedTimeline(chart.Events);
        _nextSpawnIdx = 0;

        // 同時押し検出 (K 指示 2026-07-30): 同一時刻 (ms 丸め) に 2 ノーツ以上あるものは
        // 同時押し色 (GameColorSettings.ChordColor、既定 黄) で表示する。表示のみで判定に影響しない。
        _chordIds.Clear();
        var byTime = new Dictionary<long, List<int>>();
        foreach (var n in _allNotes)
        {
            long key = (long)System.Math.Round(n.TimeMs);
            if (!byTime.TryGetValue(key, out var list)) byTime[key] = list = new List<int>();
            list.Add(n.Id);
        }
        foreach (var kv in byTime)
            if (kv.Value.Count >= 2)
                foreach (int id in kv.Value) _chordIds.Add(id);
    }

    readonly HashSet<int> _chordIds = new HashSet<int>();   // 同時押しノーツの Id

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

        // Speed-relative windows: spawn exactly at NoteSpawnZ, despawn at NoteDespawnZ.
        float  speed            = Mathf.Max(0.1f, _scrollSpeed);
        double spawnLookaheadMs = (LaneLayout.NoteSpawnZ   - LaneLayout.JudgmentLineZ) / speed * 1000.0;
        double despawnAfterMs   = (LaneLayout.JudgmentLineZ - LaneLayout.NoteDespawnZ) / speed * 1000.0;

        // Spawn notes entering the lookahead window. Distances are measured in
        // speed-weighted "visual ms" so spawn/despawn Z stay correct under speed events.
        while (_nextSpawnIdx < _allNotes.Count)
        {
            var noteData = _allNotes[_nextSpawnIdx];
            double dt    = _speed != null ? _speed.VisualDistanceMs(visualMs, noteData.TimeMs)
                                          : noteData.TimeMs - visualMs;

            if (dt > spawnLookaheadMs) break;

            if (dt < -despawnAfterMs) { _nextSpawnIdx++; continue; }

            var ctrl = _pool.Acquire(noteData.Type);
            ctrl.Initialize(noteData);
            ctrl.SetChord(_chordIds.Contains(noteData.Id));   // 同時押しは黄 (設定可)
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
            double dtEnd = _speed != null ? _speed.VisualDistanceMs(visualMs, endMs)
                                          : endMs - visualMs;

            if (dtEnd < -despawnAfterMs)
            {
                _noteById.Remove(note.Data.Id);
                _pool.Release(note);
                _activeNotes.RemoveAt(i);
            }
            else
            {
                note.UpdatePosition(visualMs, _scrollSpeed, _speed);
            }
        }
    }
}
