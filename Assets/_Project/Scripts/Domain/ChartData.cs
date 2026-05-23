using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 楽曲のメタデータ（タイトル・アーティスト・BPM・オーディオファイルパスなど）を保持するドメインモデル。
/// </summary>
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
    /// <summary>
    /// 音源再生の開始遅延 (ms)。chart 時刻 t に対し、audio サンプル時刻は (t - AudioOffsetMs)。
    /// 正の値 = 音源が遅れて始まる(チャート開始から AudioOffsetMs 間は無音)
    /// 負の値 = 音源を先送りで再生開始(イントロをスキップ)
    /// </summary>
    public int          AudioOffsetMs;
    public List<SectorDef> Sectors;
}

/// <summary>
/// 楽曲を区切るセクター（区間）の定義。IDと名称、および終了時刻（ミリ秒）を持つ。
/// </summary>
public class SectorDef
{
    public int    Id;
    public string Name;
    public int    EndMs;
}

/// <summary>
/// 譜面データ全体を表すドメインモデル。バージョン・難易度・ノーツリスト・テンポイベントなどを保持する。
/// </summary>
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

/// <summary>
/// BPM変化またはスクロール速度倍率変化を表すテンポイベント。タイプは "bpm" または "speed"。
/// </summary>
public class TempoEvent
{
    public string Type;        // "bpm" | "speed"
    public double TimeMs;
    public double Bpm;
    public double Multiplier;
}
