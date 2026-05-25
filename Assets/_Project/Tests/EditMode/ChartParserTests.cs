using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

// Inline JSON strings avoid file I/O in the first four tests.
// The fifth/sixth tests read the actual StreamingAssets files.

/// <summary><see cref="ChartParser"/> のユニットテスト。</summary>
[TestFixture]
public class ChartParserTests
{
    // ── Shared inline JSON ─────────────────────────────────────────────────

    private const string MetaJson = @"{
        ""songId"": ""test_song"",
        ""title"": ""Test Song"",
        ""artist"": ""Test Artist"",
        ""bpm"": 120.0,
        ""durationMs"": 20000,
        ""audioFile"": ""audio.ogg"",
        ""jacketFile"": ""jacket.png"",
        ""firstOnsetMs"": 0,
        ""sectors"": [
            { ""id"": 1, ""name"": ""S1"", ""endMs"": 4000 },
            { ""id"": 2, ""name"": ""S2"", ""endMs"": 8000 }
        ]
    }";

    private const string ChartJson = @"{
        ""version"": 1,
        ""songId"": ""test_song"",
        ""difficulty"": ""extra"",
        ""level"": 10,
        ""tags"": [],
        ""chartHash"": ""abc123"",
        ""totalNotes"": 3,
        ""events"": [{ ""type"": ""bpm"", ""timeMs"": 0, ""bpm"": 120.0, ""multiplier"": 1.0 }],
        ""notes"": [
            { ""id"": 1, ""type"": ""tap"",   ""lane"": ""0"",   ""timeMs"": 1000, ""durationMs"": 0 },
            { ""id"": 2, ""type"": ""fxTap"", ""lane"": ""fxL"", ""timeMs"": 2000, ""durationMs"": 0 },
            { ""id"": 3, ""type"": ""fxTap"", ""lane"": ""fxR"", ""timeMs"": 3000, ""durationMs"": 0 }
        ]
    }";

    // ── 1. ParseMeta ───────────────────────────────────────────────────────

    [Test]
    public void ParseMeta_ValidJson_ReturnsMetadata()
    {
        var meta = ChartParser.ParseMeta(MetaJson);

        Assert.AreEqual("test_song",   meta.SongId);
        Assert.AreEqual("Test Song",   meta.Title);
        Assert.AreEqual("Test Artist", meta.Artist);
        Assert.AreEqual(120.0,         meta.Bpm,    0.001);
        Assert.AreEqual(20000,         meta.DurationMs);
        Assert.AreEqual("audio.ogg",   meta.AudioFile);
        Assert.IsNotNull(meta.Sectors);
        Assert.AreEqual(2, meta.Sectors.Count);
        Assert.AreEqual(4000, meta.Sectors[0].EndMs);
        Assert.AreEqual("S2", meta.Sectors[1].Name);
    }

    // ── 2. ParseChart ──────────────────────────────────────────────────────

    [Test]
    public void ParseChart_ValidJson_ReturnsChartData()
    {
        var chart = ChartParser.ParseChart(ChartJson);

        Assert.AreEqual("test_song", chart.SongId);
        Assert.AreEqual("extra",     chart.Difficulty);
        Assert.AreEqual(10,          chart.Level);
        Assert.AreEqual(3,           chart.TotalNotes);
        Assert.IsNotNull(chart.Notes);
        Assert.AreEqual(3,             chart.Notes.Count);
        Assert.AreEqual(1000,          chart.Notes[0].TimeMs, 0.001);
        Assert.AreEqual(NoteType.Tap,  chart.Notes[0].Type);
        Assert.AreEqual(LaneRef.Lane0, chart.Notes[0].Lane);
        Assert.IsNotNull(chart.Events);
        Assert.AreEqual(1, chart.Events.Count);
        Assert.AreEqual(120.0, chart.Events[0].Bpm, 0.001);
    }

    // ── 3. Lane string → enum ─────────────────────────────────────────────

    [Test]
    public void ParseChart_LaneStringsToEnum_Correctly()
    {
        var chart = ChartParser.ParseChart(ChartJson);
        // notes[0] = lane "0", notes[1] = "fxL", notes[2] = "fxR"
        Assert.AreEqual(LaneRef.Lane0, chart.Notes[0].Lane, "\"0\" → Lane0");
        Assert.AreEqual(LaneRef.FxL,   chart.Notes[1].Lane, "\"fxL\" → FxL");
        Assert.AreEqual(LaneRef.FxR,   chart.Notes[2].Lane, "\"fxR\" → FxR");
    }

    // ── 4. Validate duplicate → Critical ─────────────────────────────────

    [Test]
    public void Validate_DuplicateNotes_ReturnsCritical()
    {
        var chart = new ChartData
        {
            Notes = new List<NoteData>
            {
                new NoteData { Id = 1, Type = NoteType.Tap, Lane = LaneRef.Lane0, TimeMs = 1000 },
                new NoteData { Id = 2, Type = NoteType.Tap, Lane = LaneRef.Lane0, TimeMs = 1000 }, // duplicate
            },
            Events = new List<TempoEvent>(),
            Tags   = new List<string>()
        };
        var song = new SongMetadata { SongId = "t", DurationMs = 5000, Sectors = new List<SectorDef>() };

        var issues = ChartValidator.Validate(chart, song);

        Assert.IsTrue(
            issues.Exists(i => i.Severity == ValidationIssue.SeverityLevel.Critical),
            "Expected a Critical issue for duplicate lane+time notes");
    }

    // ── 5. Validate hold DurationMs ≤ 0 → Critical ───────────────────────

    [Test]
    public void Validate_NegativeHoldDuration_ReturnsCritical()
    {
        var chart = new ChartData
        {
            Notes = new List<NoteData>
            {
                new NoteData { Id = 1, Type = NoteType.Hold, Lane = LaneRef.Lane0,
                               TimeMs = 1000, DurationMs = -100 },
            },
            Events = new List<TempoEvent>(),
            Tags   = new List<string>()
        };
        var song = new SongMetadata { SongId = "t", DurationMs = 5000, Sectors = new List<SectorDef>() };

        var issues = ChartValidator.Validate(chart, song);

        Assert.IsTrue(
            issues.Exists(i => i.Severity == ValidationIssue.SeverityLevel.Critical),
            "Expected a Critical issue for hold DurationMs <= 0");
    }

    // ── 6. Full file: 20 notes, 0 validation issues ───────────────────────

    [Test]
    public void TestSongChart_TwentyNotesAndZeroValidationIssues()
    {
        // GetCurrentDirectory() returns project root in Unity Edit Mode
        string root      = Directory.GetCurrentDirectory();
        string metaPath  = Path.Combine(root, "Assets", "StreamingAssets", "Songs", "test_song", "meta.json");
        string chartPath = Path.Combine(root, "Assets", "StreamingAssets", "Songs", "test_song", "charts", "extra.json");

        Assert.IsTrue(File.Exists(metaPath),  $"meta.json not found: {metaPath}");
        Assert.IsTrue(File.Exists(chartPath), $"extra.json not found: {chartPath}");

        var meta  = ChartParser.ParseMeta (File.ReadAllText(metaPath));
        var chart = ChartParser.ParseChart(File.ReadAllText(chartPath));

        Assert.AreEqual(20, chart.Notes.Count, "should have exactly 20 notes");

        var issues = ChartValidator.Validate(chart, meta);
        Assert.AreEqual(0, issues.Count,
            $"expected 0 issues, got {issues.Count}: " +
            string.Join(", ", issues.ConvertAll(i => i.Message)));
    }
}
