using System.Collections.Generic;

/// <summary>Undo/Redo スタックを管理する。EditCommand を Execute すると Redo スタックがクリアされる。</summary>
public sealed class ChartHistory
{
    readonly Stack<IEditCommand> _undo = new Stack<IEditCommand>();
    readonly Stack<IEditCommand> _redo = new Stack<IEditCommand>();
    readonly EditorState _state;
    /// <summary>操作対象の編集状態を指定して履歴を生成する。</summary>
    public ChartHistory(EditorState state) { _state = state; }

    /// <summary>Undo 可能か。</summary>
    public bool CanUndo => _undo.Count > 0;
    /// <summary>Redo 可能か。</summary>
    public bool CanRedo => _redo.Count > 0;
    /// <summary>Undo スタックの件数。</summary>
    public int UndoCount => _undo.Count;
    /// <summary>Redo スタックの件数。</summary>
    public int RedoCount => _redo.Count;

    /// <summary>コマンドを適用して Undo スタックに積む。Redo スタックはクリアされ、IsDirty が立つ。</summary>
    public void Execute(IEditCommand cmd)
    {
        cmd.Apply(_state);
        _undo.Push(cmd);
        _redo.Clear();
        _state.IsDirty = true;
    }

    /// <summary>直近のコマンドを取り消す。取り消すものが無ければ false。</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var cmd = _undo.Pop();
        cmd.Revert(_state);
        _redo.Push(cmd);
        _state.IsDirty = true;
        return true;
    }

    /// <summary>取り消したコマンドを再適用する。再適用するものが無ければ false。</summary>
    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var cmd = _redo.Pop();
        cmd.Apply(_state);
        _undo.Push(cmd);
        _state.IsDirty = true;
        return true;
    }

    /// <summary>Undo/Redo 両スタックをクリアする。</summary>
    public void Clear() { _undo.Clear(); _redo.Clear(); }

    /// <summary>次に Undo されるコマンドの説明(無ければ null)。</summary>
    public string PeekUndoDescription() => _undo.Count > 0 ? _undo.Peek().Description : null;
    /// <summary>次に Redo されるコマンドの説明(無ければ null)。</summary>
    public string PeekRedoDescription() => _redo.Count > 0 ? _redo.Peek().Description : null;
}
