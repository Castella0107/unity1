using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// OnsetAnalyzer / AutoChartGenerator のテスト。
/// 合成音源 (BPM120: キック500ms毎 + ハイハット250ms毎 + 10〜12s 持続音) を共有フィクスチャに、
/// 帯域別オンセット検出・持続音抽出・難易度別生成の密度/格子/ジャック制約/決定性を検証する。
/// </summary>
[TestFixture]
public class AutoChartGeneratorTests
{
    const int    SR     = 44100;
    const double DurSec = 20.0;

    static float[]              _audio;
    static OnsetAnalyzer.Result _ana;

    [OneTimeSetUp]
    public void SetUp()
    {
        _audio = Synthesize();
        _ana   = OnsetAnalyzer.Analyze(_audio, SR);
    }

    static float[] Synthesize()
    {
        int n = (int)(SR * DurSec);
        var s = new float[n];
        var rng = new Random(42);

        // キック: 60Hz 80ms バースト (指数減衰) を 500ms 毎
        for (double t = 0; t < DurSec; t += 0.5)
        {
            int start = (int)(t * SR);
            int len = (int)(0.08 * SR);
            for (int i = 0; i < len && start + i < n; i++)
            {
                double env = Math.Exp(-i / (0.02 * SR));
                s[start + i] += (float)(0.9 * env * Math.Sin(2 * Math.PI * 60.0 * i / SR));
            }
        }
        // ハイハット: 8kHz + ノイズ 30ms バーストを 250ms 毎
        for (double t = 0.125; t < DurSec; t += 0.25)
        {
            int start = (int)(t * SR);
            int len = (int)(0.03 * SR);
            for (int i = 0; i < len && start + i < n; i++)
            {
                double env = Math.Exp(-i / (0.008 * SR));
                double noise = rng.NextDouble() * 2 - 1;
                s[start + i] += (float)(0.35 * env * (0.6 * Math.Sin(2 * Math.PI * 8000.0 * i / SR) + 0.4 * noise));
            }
        }
        // 中域持続音: 440Hz を 10.0〜12.0s
        int st440 = (int)(10.0 * SR), en440 = (int)(12.0 * SR);
        for (int i = st440; i < en440 && i < n; i++)
        {
            double fade = Math.Min(1.0, Math.Min(i - st440, en440 - i) / (0.01 * SR));
            s[i] += (float)(0.4 * fade * Math.Sin(2 * Math.PI * 440.0 * i / SR));
        }
        return s;
    }

    static EditorState NewState(string diff)
    {
        var st = EditorState.NewEmpty("testsong", diff, 120.0, DurSec * 1000.0);
        st.ChartOffsetMs = 0;
        return st;
    }

    static List<NoteData> Gen(string diff, int seed = 7, double densityScale = 1.0)
    {
        return AutoChartGenerator.Generate(NewState(diff), _ana,
            new AutoChartGenerator.Options { Difficulty = diff, Seed = seed, DensityScale = densityScale });
    }

    static double MedianInterval(List<OnsetAnalyzer.Onset> list)
    {
        Assert.GreaterOrEqual(list.Count, 3);
        var iv = new List<double>();
        for (int i = 1; i < list.Count; i++) iv.Add(list[i].TimeMs - list[i - 1].TimeMs);
        iv.Sort();
        return iv[iv.Count / 2];
    }

    // ── OnsetAnalyzer ────────────────────────────────────────────────────────

    [Test]
    public void 低域オンセットがキック周期で検出される()
    {
        var low = _ana.BandOnsets[(int)OnsetAnalyzer.Band.Low];
        Assert.That(low.Count, Is.InRange(32, 48), "20秒 / 500ms ≈ 40個");
        Assert.That(MedianInterval(low), Is.EqualTo(500.0).Within(25.0));
    }

    [Test]
    public void 高域オンセットがハイハット周期で検出される()
    {
        var high = _ana.BandOnsets[(int)OnsetAnalyzer.Band.High];
        Assert.That(MedianInterval(high), Is.EqualTo(250.0).Within(25.0));
    }

    [Test]
    public void 持続音が検出される()
    {
        OnsetAnalyzer.Sustain found = null;
        foreach (var s in _ana.Sustains)
            if (s.StartMs > 9500 && s.StartMs < 10500) { found = s; break; }
        Assert.IsNotNull(found, "10s 付近の持続音");
        Assert.That(found.DurationMs, Is.InRange(1200.0, 2600.0));
    }

    // ── AutoChartGenerator ───────────────────────────────────────────────────

    [Test]
    public void 難易度が上がるほど密度が上がる()
    {
        int easy = Gen("easy").Count, normal = Gen("normal").Count,
            hard = Gen("hard").Count, extra = Gen("extra").Count;
        Assert.Greater(easy, 5);
        Assert.Less(easy, normal);
        Assert.Less(normal, hard);
        Assert.LessOrEqual(hard, extra);
    }

    [Test]
    public void easyはFxレーンを使わずnormalは使う()
    {
        foreach (var x in Gen("easy"))
            Assert.That(x.Lane, Is.Not.EqualTo(LaneRef.FxL).And.Not.EqualTo(LaneRef.FxR));
        bool hasFx = false;
        foreach (var x in Gen("normal")) if (x.Type == NoteType.FxTap) hasFx = true;
        Assert.IsTrue(hasFx);
    }

    [Test]
    public void 持続音からホールドが生成される()
    {
        bool found = false;
        foreach (var x in Gen("normal"))
            if (x.Type == NoteType.Hold && x.TimeMs > 9000 && x.TimeMs < 11000 && x.DurationMs > 400)
                found = true;
        Assert.IsTrue(found, "10s 付近のホールド");
    }

    [Test]
    public void 全ノーツが許容音価の格子に乗る()
    {
        foreach (var diff in new[] { "easy", "normal", "hard", "extra" })
        {
            // BPM120: 8分=250ms / 16分=125ms
            double step = (diff == "hard" || diff == "extra") ? 125.0 : 250.0;
            foreach (var x in Gen(diff))
            {
                double err = Math.Abs(x.TimeMs - Math.Round(x.TimeMs / step) * step);
                Assert.Less(err, 1.0, $"{diff} t={x.TimeMs}");
            }
        }
    }

    [Test]
    public void 同一レーンのジャック間隔が守られる()
    {
        foreach (var diff in new[] { "easy", "normal", "hard", "extra" })
        {
            var preset = AutoChartGenerator.PresetFor(diff);
            var byLane = new Dictionary<LaneRef, List<NoteData>>();
            foreach (var x in Gen(diff))
            {
                if (x.Lane > LaneRef.Lane3) continue;
                if (!byLane.TryGetValue(x.Lane, out var l)) byLane[x.Lane] = l = new List<NoteData>();
                l.Add(x);
            }
            foreach (var kv in byLane)
            {
                kv.Value.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
                for (int i = 1; i < kv.Value.Count; i++)
                {
                    double gap = kv.Value[i].TimeMs - (kv.Value[i - 1].TimeMs + kv.Value[i - 1].DurationMs);
                    Assert.GreaterOrEqual(gap, preset.JackMinMs - 1.0, $"{diff} lane={kv.Key}");
                }
            }
        }
    }

    [Test]
    public void 同一シードで決定的_別シードで変化する()
    {
        var a = Gen("hard", seed: 7);
        var b = Gen("hard", seed: 7);
        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].TimeMs, b[i].TimeMs);
            Assert.AreEqual(a[i].Lane, b[i].Lane);
            Assert.AreEqual(a[i].Type, b[i].Type);
            Assert.AreEqual(a[i].DurationMs, b[i].DurationMs);
        }
        var c = Gen("hard", seed: 8);
        bool differs = a.Count != c.Count;
        for (int i = 0; !differs && i < a.Count; i++) differs = a[i].Lane != c[i].Lane;
        Assert.IsTrue(differs);
    }

    [Test]
    public void DensityScaleが密度に効く()
    {
        Assert.Less(Gen("normal", densityScale: 0.5).Count, Gen("normal").Count);
        Assert.Less(Gen("normal").Count, Gen("normal", densityScale: 1.5).Count);
    }

    [Test]
    public void 既存ノーツと衝突しない()
    {
        var st = NewState("normal");
        st.Chart.Notes.Add(new NoteData { Id = st.IssueNoteId(), Type = NoteType.Tap, Lane = LaneRef.Lane0, TimeMs = 1000, DurationMs = 0 });
        var notes = AutoChartGenerator.Generate(st, _ana,
            new AutoChartGenerator.Options { Difficulty = "normal", Seed = 7 });
        foreach (var x in notes)
            Assert.IsFalse(x.Lane == LaneRef.Lane0 && Math.Abs(x.TimeMs - 1000) < 80, $"clash t={x.TimeMs}");
    }

    [Test]
    public void ホールド終端と全ノーツが音源範囲内()
    {
        foreach (var x in Gen("extra"))
        {
            Assert.GreaterOrEqual(x.TimeMs, 0);
            Assert.LessOrEqual(x.TimeMs + x.DurationMs, DurSec * 1000.0 + 0.5);
        }
    }
}
