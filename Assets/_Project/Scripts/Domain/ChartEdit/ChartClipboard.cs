using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面エディタ用のシンプルなクリップボード。
/// コピー時に相対時刻でスナップショットを保存し、ペースト時に現在のplayhead時刻+IssueNoteId()で再注入する。
/// </summary>
public sealed class ChartClipboard
{
    /// <summary>クリップボード内ノーツ。TimeMs はコピー時の基準時刻からの相対 ms。</summary>
    readonly List<NoteData> _items = new List<NoteData>();
    /// <summary>クリップボードに格納されているノーツ数。</summary>
    public int Count => _items.Count;
    /// <summary>クリップボードが空か。</summary>
    public bool IsEmpty => _items.Count == 0;

    /// <summary>クリップボードを空にする。</summary>
    public void Clear() => _items.Clear();

    /// <summary>選択ノーツをクリップボードに格納。基準時刻 = 選択ノーツの最早時刻。</summary>
    public void Copy(IEnumerable<NoteData> notes)
    {
        _items.Clear();
        double minT = double.MaxValue;
        foreach (var n in notes) if (n.TimeMs < minT) minT = n.TimeMs;
        if (minT == double.MaxValue) return;
        foreach (var n in notes)
        {
            _items.Add(new NoteData
            {
                Id = -1,                           // re-issued at paste time
                Type = n.Type, Lane = n.Lane,
                TimeMs = n.TimeMs - minT,         // relative
                DurationMs = n.DurationMs,
            });
        }
    }

    /// <summary>クリップボードのコピーを返す (id 未発行・絶対時刻にオフセット)。</summary>
    public List<NoteData> Materialize(EditorState state, double atTimeMs)
    {
        var result = new List<NoteData>(_items.Count);
        for (int i = 0; i < _items.Count; i++)
        {
            var src = _items[i];
            result.Add(new NoteData
            {
                Id = state.IssueNoteId(),
                Type = src.Type, Lane = src.Lane,
                TimeMs = atTimeMs + src.TimeMs,
                DurationMs = src.DurationMs,
            });
        }
        return result;
    }
}
