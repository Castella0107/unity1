using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

public class SongMetadata
{
    public string       SongId;
    public string       Title;
    public string       Artist;
    public double       Bpm;
    public int          DurationMs;
    public string       AudioFile;
    public string       JacketFile;
    public int          FirstOnsetMs;
    public List<SectorDef> Sectors;
}

public class SectorDef
{
    public int    Id;
    public string Name;
    public int    EndMs;
}

public class ChartData
{
    public int             Version;
    public string          SongId;
    public string          Difficulty;  // "easy" | "normal" | "hard" | "extra"
    public int             Level;
    public List<string>    Tags;
    public string          ChartHash;
    public int             TotalNotes;
    public List<TempoEvent> Events;
    public List<NoteData>  Notes;
}

public class TempoEvent
{
    public string Type;        // "bpm" | "speed"
    public double TimeMs;
    public double Bpm;
    public double Multiplier;
}
