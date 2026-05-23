using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// EditorState の ChartData を本ゲームの ChartParser が読める JSON にシリアライズする。
/// 出力フォーマットは Stellanauts/PVP の {difficulty}.json と互換。
/// </summary>
public static class ChartSerializerEditor
{
    /// <summary>ChartData をフォーマット済み JSON 文字列に変換する。ChartHash は再計算される。</summary>
    public static string SerializeChart(ChartData chart)
    {
        if (chart == null) throw new ArgumentNullException(nameof(chart));

        var notes = new JArray();
        if (chart.Notes != null)
        {
            for (int i = 0; i < chart.Notes.Count; i++)
            {
                var n = chart.Notes[i];
                var jn = new JObject
                {
                    ["id"]     = n.Id,
                    ["type"]   = NoteTypeToString(n.Type),
                    ["lane"]   = LaneRefToString(n.Lane),
                    ["timeMs"] = n.TimeMs,
                };
                if (n.Type == NoteType.Hold || n.Type == NoteType.FxHold)
                    jn["durationMs"] = n.DurationMs;
                notes.Add(jn);
            }
        }

        var events = new JArray();
        if (chart.Events != null)
        {
            for (int i = 0; i < chart.Events.Count; i++)
            {
                var e = chart.Events[i];
                var je = new JObject
                {
                    ["type"]   = e.Type,
                    ["timeMs"] = e.TimeMs,
                };
                if (e.Type == "bpm")   je["bpm"]        = e.Bpm;
                if (e.Type == "speed") je["multiplier"] = e.Multiplier;
                events.Add(je);
            }
        }

        var root = new JObject
        {
            ["version"]    = chart.Version <= 0 ? 1 : chart.Version,
            ["songId"]     = chart.SongId    ?? "",
            ["difficulty"] = chart.Difficulty ?? "easy",
            ["level"]      = chart.Level,
            ["tags"]       = new JArray(chart.Tags ?? new List<string>()),
            // ChartHash placeholder - replaced below after stable serialization
            ["chartHash"]  = "",
            ["totalNotes"] = chart.Notes != null ? chart.Notes.Count : 0,
            ["events"]     = events,
            ["notes"]      = notes,
        };

        // ChartHash: SHA256 of the JSON without the hash field (so it's reproducible)
        string withoutHash = root.ToString(Formatting.None);
        root["chartHash"] = ComputeSha256Hex(withoutHash);

        return root.ToString(Formatting.Indented);
    }

    /// <summary>SongMetadata を meta.json 形式の JSON 文字列に変換する。</summary>
    public static string SerializeMeta(SongMetadata meta)
    {
        if (meta == null) throw new ArgumentNullException(nameof(meta));

        var sectors = new JArray();
        if (meta.Sectors != null)
            foreach (var s in meta.Sectors)
                sectors.Add(new JObject
                {
                    ["id"]    = s.Id,
                    ["name"]  = s.Name ?? "",
                    ["endMs"] = s.EndMs,
                });

        var root = new JObject
        {
            ["songId"]       = meta.SongId ?? "",
            ["title"]        = meta.Title  ?? "",
            ["artist"]       = meta.Artist ?? "",
            ["bpm"]          = meta.Bpm,
            ["durationMs"]   = meta.DurationMs,
            ["audioFile"]    = meta.AudioFile  ?? "audio.ogg",
            ["jacketFile"]   = meta.JacketFile ?? "jacket.png",
            ["firstOnsetMs"] = meta.FirstOnsetMs,
            ["audioOffsetMs"] = meta.AudioOffsetMs,
            ["sectors"]      = sectors,
        };
        return root.ToString(Formatting.Indented);
    }

    /// <summary>ファイルへ atomic 書き込み (一時ファイル→rename) する。</summary>
    public static void WriteFileAtomic(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else                   File.Move(tmp, path);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static string NoteTypeToString(NoteType t)
    {
        switch (t)
        {
            case NoteType.Tap:    return "tap";
            case NoteType.Hold:   return "hold";
            case NoteType.FxTap:  return "fxTap";
            case NoteType.FxHold: return "fxHold";
            default: throw new ArgumentOutOfRangeException(nameof(t), t, null);
        }
    }

    static string LaneRefToString(LaneRef l)
    {
        switch (l)
        {
            case LaneRef.Lane0: return "0";
            case LaneRef.Lane1: return "1";
            case LaneRef.Lane2: return "2";
            case LaneRef.Lane3: return "3";
            case LaneRef.FxL:   return "fxL";
            case LaneRef.FxR:   return "fxR";
            default: throw new ArgumentOutOfRangeException(nameof(l), l, null);
        }
    }

    static string ComputeSha256Hex(string content)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
