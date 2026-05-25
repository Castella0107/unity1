using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 楽曲のメタデータ（タイトル・アーティスト・BPM・オーディオファイルパスなど）を保持するドメインモデル。
/// </summary>
public class SongMetadata
{
    /// <summary>楽曲の一意な識別子。</summary>
    public string       SongId;
    /// <summary>曲タイトル。</summary>
    public string       Title;
    /// <summary>アーティスト名。</summary>
    public string       Artist;
    /// <summary>基準 BPM。</summary>
    public double       Bpm;
    /// <summary>楽曲長(ms)。</summary>
    public int          DurationMs;
    /// <summary>音源ファイル名/パス。</summary>
    public string       AudioFile;
    /// <summary>ジャケット画像ファイル名/パス。</summary>
    public string       JacketFile;
    /// <summary>Beat 1 の譜面時刻(ms)。拍格子の起点。</summary>
    public int          FirstOnsetMs;
    /// <summary>
    /// 音源再生の開始遅延 (ms)。chart 時刻 t に対し、audio サンプル時刻は (t - AudioOffsetMs)。
    /// 正の値 = 音源が遅れて始まる(チャート開始から AudioOffsetMs 間は無音)
    /// 負の値 = 音源を先送りで再生開始(イントロをスキップ)
    /// </summary>
    public int          AudioOffsetMs;
    /// <summary>楽曲を区切るセクション定義のリスト。</summary>
    public List<SectorDef> Sectors;
}

/// <summary>
/// 楽曲を区切るセクター（区間）の定義。IDと名称、および終了時刻（ミリ秒）を持つ。
/// </summary>
public class SectorDef
{
    /// <summary>セクションの一意なID。</summary>
    public int    Id;
    /// <summary>セクション名。</summary>
    public string Name;
    /// <summary>セクションの終了時刻(ms)。</summary>
    public int    EndMs;
}

/// <summary>
/// 譜面データ全体を表すドメインモデル。バージョン・難易度・ノーツリスト・テンポイベントなどを保持する。
/// </summary>
public class ChartData
{
    /// <summary>譜面フォーマットのバージョン。</summary>
    public int             Version;
    /// <summary>対応する楽曲ID。</summary>
    public string          SongId;
    /// <summary>難易度 ("easy" | "normal" | "hard" | "extra")。</summary>
    public string          Difficulty;
    /// <summary>難易度レベル。</summary>
    public int             Level;
    /// <summary>任意のタグ一覧。</summary>
    public List<string>    Tags;
    /// <summary>譜面内容のハッシュ(リプレイ検証等に使用)。</summary>
    public string          ChartHash;
    /// <summary>総ノーツ数。</summary>
    public int             TotalNotes;
    /// <summary>BPM/スピードのテンポイベント列(時刻昇順)。</summary>
    public List<TempoEvent> Events;
    /// <summary>ノーツ一覧。</summary>
    public List<NoteData>  Notes;
}

/// <summary>
/// BPM変化またはスクロール速度倍率変化を表すテンポイベント。タイプは "bpm" または "speed"。
/// </summary>
public class TempoEvent
{
    /// <summary>イベント種別 ("bpm" | "speed")。</summary>
    public string Type;
    /// <summary>イベント発生時刻(ms)。</summary>
    public double TimeMs;
    /// <summary>"bpm" イベント時の BPM 値。</summary>
    public double Bpm;
    /// <summary>"speed" イベント時のスクロール速度倍率。</summary>
    public double Multiplier;
}
