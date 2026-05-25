using System.Collections.Generic;

/// <summary>
/// 判定エンジンテスト用に小さな <see cref="ChartData"/> を組み立てるフルーエントなテストヘルパー。
/// TotalNotes はスコアリングイベント総数で算出され、オールパーフェクトが常に 1,000,000 になる。
/// </summary>
public class ChartBuilder
{
    int _nextId = 1;
    readonly List<NoteData>   _notes  = new List<NoteData>();
    readonly List<TempoEvent> _tempos = new List<TempoEvent>();

    /// <summary>BPM イベントを追加する。</summary>
    public ChartBuilder WithBpm(double bpm, double timeMs = 0)
    {
        _tempos.Add(new TempoEvent { Type = "bpm", TimeMs = timeMs, Bpm = bpm });
        return this;
    }

    /// <summary>タップ(FX レーンなら FxTap)を追加する。</summary>
    public ChartBuilder AddTap(LaneRef lane, double timeMs)
    {
        _notes.Add(new NoteData
        {
            Id         = _nextId++,
            Type       = (lane == LaneRef.FxL || lane == LaneRef.FxR)
                         ? NoteType.FxTap : NoteType.Tap,
            Lane       = lane,
            TimeMs     = timeMs,
            DurationMs = 0,
        });
        return this;
    }

    /// <summary>ホールド(FX レーンなら FxHold)を追加する。</summary>
    public ChartBuilder AddHold(LaneRef lane, double startMs, double durationMs)
    {
        _notes.Add(new NoteData
        {
            Id         = _nextId++,
            Type       = (lane == LaneRef.FxL || lane == LaneRef.FxR)
                         ? NoteType.FxHold : NoteType.Hold,
            Lane       = lane,
            TimeMs     = startMs,
            DurationMs = durationMs,
        });
        return this;
    }

    /// <summary>蓄積したノーツとテンポから <see cref="ChartData"/> を生成する(BPM 未指定なら 120)。</summary>
    public ChartData Build()
    {
        if (_tempos.Count == 0) WithBpm(120);
        return new ChartData
        {
            Notes      = _notes,
            Events     = _tempos,
            TotalNotes = ComputeTotalScoringEvents(),
            ChartHash  = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Level      = 18,
        };
    }

    int ComputeTotalScoringEvents()
    {
        var bpmTl = new BpmTimeline(_tempos);
        int total = ScoringEventCounter.Count(_notes, bpmTl);
        return total > 0 ? total : 1;
    }
}
