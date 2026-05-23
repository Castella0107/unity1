using System.Collections.Generic;

/// <summary>Undo/Redo スタックを管理する。EditCommand を Execute すると Redo スタックがクリアされる。</summary>
public sealed class ChartHistory
{
    readonly Stack<IEditCommand> _undo = new Stack<IEditCommand>();
    readonly Stack<IEditCommand> _redo = new Stack<IEditCommand>();
    readonly EditorState _state;
    public ChartHistory(EditorState state) { _state = state; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;

    public void Execute(IEditCommand cmd)
    {
        cmd.Apply(_state);
        _undo.Push(cmd);
        _redo.Clear();
        _state.IsDirty = true;
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var cmd = _undo.Pop();
        cmd.Revert(_state);
        _redo.Push(cmd);
        _state.IsDirty = true;
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var cmd = _redo.Pop();
        cmd.Apply(_state);
        _undo.Push(cmd);
        _state.IsDirty = true;
        return true;
    }

    public void Clear() { _undo.Clear(); _redo.Clear(); }

    public string PeekUndoDescription() => _undo.Count > 0 ? _undo.Peek().Description : null;
    public string PeekRedoDescription() => _redo.Count > 0 ? _redo.Peek().Description : null;
}
