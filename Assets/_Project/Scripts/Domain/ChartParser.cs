using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面またはメタデータのJSONパース中に発生した例外を表す。
/// </summary>
public class ChartParseException : Exception
{
    public ChartParseException(string message) : base(message) { }
    public ChartParseException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// JSONテキストから <see cref="SongMetadata"/> および <see cref="ChartData"/> をパースする静的クラス。
/// UTF-8 BOMの除去、ChartHashの検証・フォールバック生成も行う。
/// </summary>
public static class ChartParser
{
    // Newtonsoft default: case-insensitive matching, ignores missing members.
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    // ── Internal DTOs ──────────────────────────────────────────────────────

    private class MetaDto
    {
        public string SongId       { get; set; }
        public string Title        { get; set; }
        public string Artist       { get; set; }
        public double Bpm          { get; set; }
        public int    DurationMs   { get; set; }
        public string AudioFile    { get; set; }
        public string JacketFile   { get; set; }
        public int    FirstOnsetMs { get; set; }
        public int    AudioOffsetMs { get; set; }
        public List<SectorDefDto> Sectors { get; set; }
    }

    private class SectorDefDto
    {
        public int    Id    { get; set; }
        public string Name  { get; set; }
        public int    EndMs { get; set; }
    }

    private class TempoEventDto
    {
        public string Type       { get; set; }
        public double TimeMs     { get; set; }
        public double Bpm        { get; set; }
        public double Multiplier { get; set; }
    }

    private class NoteDto
    {
        public int    Id          { get; set; }
        public string Type        { get; set; }  // "tap"|"hold"|"fxTap"|"fxHold"
        public string Lane        { get; set; }  // "0"-"3"|"fxL"|"fxR"
        public double TimeMs      { get; set; }
        public double DurationMs  { get; set; }
    }

    private class ChartDto
    {
        public int    Version    { get; set; }
        public string SongId     { get; set; }
        public string Difficulty { get; set; }
        public int    Level      { get; set; }
        public List<string> Tags { get; set; }
        public string ChartHash  { get; set; }
        public int    TotalNotes { get; set; }
        public List<TempoEventDto> Events { get; set; }
        public List<NoteDto>       Notes  { get; set; }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public static SongMetadata ParseMeta(string json)
    {
        if (json != null && json.Length > 0 && (int)json[0] == 0xFEFF)
            json = json.Substring(1);

        try
        {
            var dto = JsonConvert.DeserializeObject<MetaDto>(json, Settings)
                ?? throw new ChartParseException("meta JSON parsed to null");

            var sectors = new List<SectorDef>();
            if (dto.Sectors != null)
                foreach (var s in dto.Sectors)
                    sectors.Add(new SectorDef { Id = s.Id, Name = s.Name, EndMs = s.EndMs });

            return new SongMetadata
            {
                SongId       = dto.SongId,
                Title        = dto.Title,
                Artist       = dto.Artist,
                Bpm          = dto.Bpm,
                DurationMs   = dto.DurationMs,
                AudioFile    = dto.AudioFile,
                JacketFile   = dto.JacketFile,
                FirstOnsetMs = dto.FirstOnsetMs,
                AudioOffsetMs = dto.AudioOffsetMs,
                Sectors      = sectors
            };
        }
        catch (JsonException ex)
        {
            throw new ChartParseException("Failed to parse meta JSON", ex);
        }
    }

    public static ChartData ParseChart(string json)
    {
        // Strip UTF-8 BOM (U+FEFF) if present — PS5.1 -Encoding utf8 adds it.
        if (json != null && json.Length > 0 && (int)json[0] == 0xFEFF)
            json = json.Substring(1);

        try
        {
            var dto = JsonConvert.DeserializeObject<ChartDto>(json, Settings)
                ?? throw new ChartParseException("chart JSON parsed to null");

            var notes = new List<NoteData>();
            if (dto.Notes != null)
                foreach (var n in dto.Notes)
                    notes.Add(NoteFromDto(n));

            var events = new List<TempoEvent>();
            if (dto.Events != null)
                foreach (var e in dto.Events)
                    events.Add(new TempoEvent
                    {
                        Type       = e.Type,
                        TimeMs     = e.TimeMs,
                        Bpm        = e.Bpm,
                        Multiplier = e.Multiplier
                    });

            var bpm = new BpmTimeline(events);
            int totalScoringEvents = ScoringEventCounter.Count(notes, bpm);
            if (totalScoringEvents == 0) totalScoringEvents = System.Math.Max(dto.TotalNotes, 1);

            // Ensure ChartHash is a valid lowercase hex string.
            // If the JSON field is missing, empty, or contains non-hex chars (e.g. "abc123test"),
            // fall back to a SHA-256 of the raw JSON so ReplayEncoder never receives garbage.
            string chartHash = dto.ChartHash;
            if (!IsValidHexHash(chartHash))
                chartHash = ComputeSha256Hex(json);

            return new ChartData
            {
                Version    = dto.Version,
                SongId     = dto.SongId,
                Difficulty = dto.Difficulty,
                Level      = dto.Level,
                Tags       = dto.Tags ?? new List<string>(),
                ChartHash  = chartHash,
                TotalNotes = totalScoringEvents,
                Events     = events,
                Notes      = notes
            };
        }
        catch (JsonException ex)
        {
            string preview = json != null && json.Length > 0
                ? json.Substring(0, System.Math.Min(200, json.Length))
                : "(null)";
            throw new ChartParseException(
                "Failed to parse chart JSON: " + ex.Message + " | first 200 chars: " + preview, ex);
        }
    }

    // ── Private conversion helpers ─────────────────────────────────────────

    // ── Hash helpers ───────────────────────────────────────────────────────────

    private static bool IsValidHexHash(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length % 2 != 0) return false;
        foreach (char c in s)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }

    private static string ComputeSha256Hex(string content)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    // ── Note conversion ────────────────────────────────────────────────────────

    private static NoteData NoteFromDto(NoteDto dto)
    {
        return new NoteData
        {
            Id         = dto.Id,
            Type       = ParseNoteType(dto.Type),
            Lane       = ParseLaneRef(dto.Lane),
            TimeMs     = dto.TimeMs,
            DurationMs = dto.DurationMs
        };
    }

    private static LaneRef ParseLaneRef(string s)
    {
        switch (s)
        {
            case "0": return LaneRef.Lane0;
            case "1": return LaneRef.Lane1;
            case "2": return LaneRef.Lane2;
            case "3": return LaneRef.Lane3;
            case "fxL": return LaneRef.FxL;
            case "fxR": return LaneRef.FxR;
            default:    throw new ChartParseException($"Unknown lane value: '{s}'");
        }
    }

    private static NoteType ParseNoteType(string s)
    {
        switch (s)
        {
            case "tap":    return NoteType.Tap;
            case "hold":   return NoteType.Hold;
            case "fxTap":  return NoteType.FxTap;
            case "fxHold": return NoteType.FxHold;
            default:       throw new ChartParseException($"Unknown note type: '{s}'");
        }
    }
}
