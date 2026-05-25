using System;
using System.Collections.Generic;

/// <summary>
/// 譜面編集操作の Command パターン基底インターフェース。
/// すべての破壊的編集操作は EditCommand を ChartHistory.Execute() に渡して適用する。
/// </summary>
public interface IEditCommand
{
    /// <summary>この操作を状態に適用する。</summary>
    void Apply(EditorState state);
    /// <summary>この操作を取り消し、適用前の状態へ戻す。</summary>
    void Revert(EditorState state);
    /// <summary>Undo/Redo 表示用の操作説明。</summary>
    string Description { get; }
}

/// <summary>新規ノーツを Chart.Notes に追加する。</summary>
public sealed class PlaceNoteCommand : IEditCommand
{
    readonly NoteData _note;
    /// <summary>追加するノーツを指定してコマンドを生成する。</summary>
    public PlaceNoteCommand(NoteData note) { _note = note; }
    public string Description => $"Place {_note.Type} @ {_note.TimeMs:F0}ms lane={_note.Lane}";

    public void Apply(EditorState state)
    {
        state.Chart.Notes.Add(_note);
        SortNotes(state.Chart.Notes);
    }
    public void Revert(EditorState state)
    {
        for (int i = 0; i < state.Chart.Notes.Count; i++)
            if (state.Chart.Notes[i].Id == _note.Id) { state.Chart.Notes.RemoveAt(i); break; }
    }

    internal static void SortNotes(List<NoteData> notes)
    {
        notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
    }
}

/// <summary>既存ノーツを削除する。</summary>
public sealed class DeleteNoteCommand : IEditCommand
{
    readonly NoteData _snapshot;
    /// <summary>削除するノーツのスナップショットを取り、Undo で完全復元できるようにする。</summary>
    public DeleteNoteCommand(NoteData note)
    {
        _snapshot = new NoteData
        {
            Id = note.Id, Type = note.Type, Lane = note.Lane,
            TimeMs = note.TimeMs, DurationMs = note.DurationMs,
        };
    }
    public string Description => $"Delete note id={_snapshot.Id}";

    public void Apply(EditorState state)
    {
        for (int i = 0; i < state.Chart.Notes.Count; i++)
            if (state.Chart.Notes[i].Id == _snapshot.Id) { state.Chart.Notes.RemoveAt(i); break; }
    }
    public void Revert(EditorState state)
    {
        state.Chart.Notes.Add(new NoteData
        {
            Id = _snapshot.Id, Type = _snapshot.Type, Lane = _snapshot.Lane,
            TimeMs = _snapshot.TimeMs, DurationMs = _snapshot.DurationMs,
        });
        PlaceNoteCommand.SortNotes(state.Chart.Notes);
    }
}

/// <summary>ノーツの位置 (TimeMs / Lane) を変更する。</summary>
public sealed class MoveNoteCommand : IEditCommand
{
    readonly int _id;
    readonly double _oldTimeMs, _newTimeMs;
    readonly LaneRef _oldLane, _newLane;
    readonly NoteType _oldType, _newType;
    readonly bool _typeChanges;

    /// <summary>ノーツの新しい時刻とレーンを指定して移動コマンドを生成する(種別は変えない)。</summary>
    public MoveNoteCommand(NoteData note, double newTimeMs, LaneRef newLane)
    {
        _id = note.Id;
        _oldTimeMs = note.TimeMs; _newTimeMs = newTimeMs;
        _oldLane   = note.Lane;   _newLane   = newLane;
        _oldType = _newType = note.Type;
        _typeChanges = false;
    }

    /// <summary>レーン跨ぎで Type も同時に変える版(FX⇄通常 の自動補正など)。</summary>
    public MoveNoteCommand(NoteData note, double newTimeMs, LaneRef newLane, NoteType newType)
    {
        _id = note.Id;
        _oldTimeMs = note.TimeMs; _newTimeMs = newTimeMs;
        _oldLane   = note.Lane;   _newLane   = newLane;
        _oldType   = note.Type;   _newType   = newType;
        _typeChanges = note.Type != newType;
    }

    public string Description => $"Move note id={_id}";
    public void Apply(EditorState state)  => Set(state, _newTimeMs, _newLane, _newType);
    public void Revert(EditorState state) => Set(state, _oldTimeMs, _oldLane, _oldType);

    void Set(EditorState state, double t, LaneRef lane, NoteType type)
    {
        var n = FindNote(state.Chart.Notes, _id);
        if (n == null) return;
        n.TimeMs = t;
        n.Lane   = lane;
        if (_typeChanges) n.Type = type;
        PlaceNoteCommand.SortNotes(state.Chart.Notes);
    }
    static NoteData FindNote(List<NoteData> notes, int id)
    {
        for (int i = 0; i < notes.Count; i++) if (notes[i].Id == id) return notes[i];
        return null;
    }
}

/// <summary>Hold ノーツの持続時間を変更する。</summary>
public sealed class ResizeHoldCommand : IEditCommand
{
    readonly int _id;
    readonly double _oldDur, _newDur;
    /// <summary>対象ホールドと新しい持続時間を指定してリサイズコマンドを生成する。</summary>
    public ResizeHoldCommand(NoteData note, double newDurationMs)
    {
        _id = note.Id; _oldDur = note.DurationMs; _newDur = newDurationMs;
    }
    public string Description => $"Resize hold id={_id}";
    public void Apply(EditorState state)
    {
        var n = FindNote(state.Chart.Notes, _id); if (n != null) n.DurationMs = _newDur;
    }
    public void Revert(EditorState state)
    {
        var n = FindNote(state.Chart.Notes, _id); if (n != null) n.DurationMs = _oldDur;
    }
    static NoteData FindNote(List<NoteData> notes, int id)
    {
        for (int i = 0; i < notes.Count; i++) if (notes[i].Id == id) return notes[i];
        return null;
    }
}

/// <summary>複数の Command をひとまとめに実行するバッチ Command (区画コピペ等)。</summary>
public sealed class BatchCommand : IEditCommand
{
    readonly List<IEditCommand> _items;
    /// <summary>まとめて実行するコマンド列と説明を指定してバッチを生成する。</summary>
    public BatchCommand(List<IEditCommand> items, string desc) { _items = items; Description = desc; }
    /// <inheritdoc/>
    public string Description { get; }
    /// <summary>このバッチに含まれる子コマンド一覧。</summary>
    public IReadOnlyList<IEditCommand> Items => _items;
    public void Apply(EditorState state)  { for (int i = 0; i < _items.Count; i++)             _items[i].Apply(state); }
    public void Revert(EditorState state) { for (int i = _items.Count - 1; i >= 0; i--)        _items[i].Revert(state); }
}

// ─────────────────────────────────────────────────────────────────────────
// Phase 2 / 3 commands
// ─────────────────────────────────────────────────────────────────────────

/// <summary>TempoEvent (type=bpm or speed) を追加する。</summary>
public sealed class AddTempoEventCommand : IEditCommand
{
    readonly TempoEvent _ev;
    /// <summary>追加する TempoEvent を指定してコマンドを生成する。</summary>
    public AddTempoEventCommand(TempoEvent ev) { _ev = ev; }
    public string Description => $"Add {_ev.Type} {(_ev.Type == "bpm" ? _ev.Bpm : _ev.Multiplier):F2} @ {_ev.TimeMs:F0}ms";

    public void Apply(EditorState state)
    {
        state.Chart.Events ??= new List<TempoEvent>();
        state.Chart.Events.Add(_ev);
        SortEvents(state.Chart.Events);
    }
    public void Revert(EditorState state)
    {
        if (state.Chart.Events == null) return;
        for (int i = 0; i < state.Chart.Events.Count; i++)
            if (ReferenceEquals(state.Chart.Events[i], _ev))
            { state.Chart.Events.RemoveAt(i); break; }
    }
    internal static void SortEvents(List<TempoEvent> evs)
    {
        evs.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
    }
}

/// <summary>TempoEvent を削除する (Undoで完全復元)。</summary>
public sealed class RemoveTempoEventCommand : IEditCommand
{
    readonly TempoEvent _snapshot;
    readonly int        _originalIndex;
    /// <summary>削除する TempoEvent と元インデックスをスナップショットし、Undo で完全復元できるようにする。</summary>
    public RemoveTempoEventCommand(TempoEvent ev, int originalIndex)
    {
        _snapshot = new TempoEvent
        {
            Type = ev.Type, TimeMs = ev.TimeMs, Bpm = ev.Bpm, Multiplier = ev.Multiplier
        };
        _originalIndex = originalIndex;
    }
    public string Description => $"Remove {_snapshot.Type} @ {_snapshot.TimeMs:F0}ms";

    public void Apply(EditorState state)
    {
        if (state.Chart.Events == null) return;
        if (_originalIndex >= 0 && _originalIndex < state.Chart.Events.Count
            && SameEvent(state.Chart.Events[_originalIndex], _snapshot))
        {
            state.Chart.Events.RemoveAt(_originalIndex);
            return;
        }
        // Fallback: find by content
        for (int i = 0; i < state.Chart.Events.Count; i++)
            if (SameEvent(state.Chart.Events[i], _snapshot))
            { state.Chart.Events.RemoveAt(i); break; }
    }
    public void Revert(EditorState state)
    {
        state.Chart.Events ??= new List<TempoEvent>();
        state.Chart.Events.Add(new TempoEvent
        {
            Type = _snapshot.Type, TimeMs = _snapshot.TimeMs,
            Bpm = _snapshot.Bpm, Multiplier = _snapshot.Multiplier
        });
        AddTempoEventCommand.SortEvents(state.Chart.Events);
    }
    static bool SameEvent(TempoEvent a, TempoEvent b)
        => a.Type == b.Type && a.TimeMs == b.TimeMs
           && a.Bpm == b.Bpm && a.Multiplier == b.Multiplier;
}

/// <summary>SectorDef を SongMetadata.Sectors に追加する。</summary>
public sealed class AddSectorCommand : IEditCommand
{
    readonly SectorDef _sec;
    /// <summary>追加するセクション定義を指定してコマンドを生成する。</summary>
    public AddSectorCommand(SectorDef sec) { _sec = sec; }
    public string Description => $"Add sector '{_sec.Name}' end={_sec.EndMs}ms";

    public void Apply(EditorState state)
    {
        if (state.Metadata == null) return;
        state.Metadata.Sectors ??= new List<SectorDef>();
        state.Metadata.Sectors.Add(_sec);
        SortSectors(state.Metadata.Sectors);
    }
    public void Revert(EditorState state)
    {
        if (state.Metadata?.Sectors == null) return;
        for (int i = 0; i < state.Metadata.Sectors.Count; i++)
            if (ReferenceEquals(state.Metadata.Sectors[i], _sec))
            { state.Metadata.Sectors.RemoveAt(i); break; }
    }
    internal static void SortSectors(List<SectorDef> list)
    {
        list.Sort((a, b) => a.EndMs.CompareTo(b.EndMs));
    }
}

/// <summary>SectorDef を削除 (Undoで完全復元)。</summary>
public sealed class RemoveSectorCommand : IEditCommand
{
    readonly SectorDef _snapshot;
    /// <summary>削除するセクションをスナップショットし、Undo で完全復元できるようにする。</summary>
    public RemoveSectorCommand(SectorDef sec)
    {
        _snapshot = new SectorDef { Id = sec.Id, Name = sec.Name, EndMs = sec.EndMs };
    }
    public string Description => $"Remove sector '{_snapshot.Name}'";

    public void Apply(EditorState state)
    {
        if (state.Metadata?.Sectors == null) return;
        for (int i = 0; i < state.Metadata.Sectors.Count; i++)
        {
            var s = state.Metadata.Sectors[i];
            if (s.Id == _snapshot.Id && s.Name == _snapshot.Name && s.EndMs == _snapshot.EndMs)
            { state.Metadata.Sectors.RemoveAt(i); break; }
        }
    }
    public void Revert(EditorState state)
    {
        if (state.Metadata == null) return;
        state.Metadata.Sectors ??= new List<SectorDef>();
        state.Metadata.Sectors.Add(new SectorDef
        {
            Id = _snapshot.Id, Name = _snapshot.Name, EndMs = _snapshot.EndMs
        });
        AddSectorCommand.SortSectors(state.Metadata.Sectors);
    }
}

/// <summary>選択ノーツ群をミラー反転 (Lane0↔Lane3, Lane1↔Lane2, FxL↔FxR)。</summary>
public sealed class MirrorNotesCommand : IEditCommand
{
    readonly List<(int id, LaneRef oldLane, LaneRef newLane)> _ops;
    /// <summary>反転対象ノーツ群から、各ノーツの新旧レーン対応を記録してコマンドを生成する。</summary>
    public MirrorNotesCommand(List<NoteData> notes)
    {
        _ops = new List<(int, LaneRef, LaneRef)>(notes.Count);
        for (int i = 0; i < notes.Count; i++)
        {
            var n = notes[i];
            _ops.Add((n.Id, n.Lane, Mirror(n.Lane)));
        }
    }
    public string Description => $"Mirror {_ops.Count} notes";
    public void Apply(EditorState state)  => SetAll(state, useNew: true);
    public void Revert(EditorState state) => SetAll(state, useNew: false);
    void SetAll(EditorState state, bool useNew)
    {
        if (state.Chart?.Notes == null) return;
        var dict = new Dictionary<int, LaneRef>();
        for (int i = 0; i < _ops.Count; i++) dict[_ops[i].id] = useNew ? _ops[i].newLane : _ops[i].oldLane;
        for (int i = 0; i < state.Chart.Notes.Count; i++)
        {
            var n = state.Chart.Notes[i];
            if (dict.TryGetValue(n.Id, out var lane)) n.Lane = lane;
        }
    }
    /// <summary>レーンを左右反転する (Lane0↔Lane3, Lane1↔Lane2, FxL↔FxR)。</summary>
    public static LaneRef Mirror(LaneRef lane)
    {
        switch (lane)
        {
            case LaneRef.Lane0: return LaneRef.Lane3;
            case LaneRef.Lane1: return LaneRef.Lane2;
            case LaneRef.Lane2: return LaneRef.Lane1;
            case LaneRef.Lane3: return LaneRef.Lane0;
            case LaneRef.FxL:   return LaneRef.FxR;
            case LaneRef.FxR:   return LaneRef.FxL;
            default: return lane;
        }
    }
}

/// <summary>クリップボードから相対時刻でノーツを貼り付ける。</summary>
public sealed class PasteNotesCommand : IEditCommand
{
    readonly List<NoteData> _pasted; // newly issued ids
    /// <summary>貼り付ける(新規ID採番済みの)ノーツ群を指定してコマンドを生成する。</summary>
    public PasteNotesCommand(List<NoteData> pasted) { _pasted = pasted; }
    public string Description => $"Paste {_pasted.Count} notes";

    public void Apply(EditorState state)
    {
        state.Chart.Notes ??= new List<NoteData>();
        for (int i = 0; i < _pasted.Count; i++) state.Chart.Notes.Add(_pasted[i]);
        PlaceNoteCommand.SortNotes(state.Chart.Notes);
    }
    public void Revert(EditorState state)
    {
        if (state.Chart?.Notes == null) return;
        var ids = new HashSet<int>();
        for (int i = 0; i < _pasted.Count; i++) ids.Add(_pasted[i].Id);
        state.Chart.Notes.RemoveAll(n => ids.Contains(n.Id));
    }
}
