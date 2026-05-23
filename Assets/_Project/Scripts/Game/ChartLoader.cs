using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Unity-layer loader. Lives in Game/ (not Domain/) because it depends on UnityEngine.
// Uses UnityWebRequest for Android JAR-path compatibility.

/// <summary>
/// StreamingAssets からチャートデータ・メタデータ・オーディオクリップを非同期で読み込む Unity 層のローダー。
/// Android の JAR パス互換のため UnityWebRequest を使用する。
/// </summary>
public static class ChartLoader
{
    /// <summary>
    /// テストプレイ用に StreamingAssets/Songs/{songId} ではなく外部ディレクトリを参照させる上書きパス。
    /// 起動引数 --chart で指定されたディレクトリが入る。null/empty なら通常 (StreamingAssets) 解決。
    /// </summary>
    public static string OverrideBasePath { get; set; }

    static string ResolveBaseDir(string songId)
    {
        return !string.IsNullOrEmpty(OverrideBasePath)
            ? OverrideBasePath
            : Path.Combine(Application.streamingAssetsPath, "Songs", songId);
    }

    public static async Task<SongMetadata> LoadMetaAsync(string songId)
    {
        string path = Path.Combine(ResolveBaseDir(songId), "meta.json");
        string json = await ReadTextAsync(path);
        return ChartParser.ParseMeta(json);
    }

    public static async Task<ChartData> LoadChartAsync(string songId, string difficulty)
    {
        // 本体配置: <base>/charts/<diff>.json。ChartEditor 配置: <base>/<diff>.json も許容。
        string baseDir   = ResolveBaseDir(songId);
        string nested    = Path.Combine(baseDir, "charts", difficulty + ".json");
        string flat      = Path.Combine(baseDir, difficulty + ".json");
        string path      = File.Exists(nested) ? nested : (File.Exists(flat) ? flat : nested);
        string json = await ReadTextAsync(path);
        return ChartParser.ParseChart(json);
    }

    public static async Task<AudioClip> LoadAudioAsync(string songId)
    {
        // 1) audio.{ogg,mp3,wav} を優先 (PVP 本体の StreamingAssets 配置)
        // 2) baseDir 内の任意 .ogg/.mp3/.wav (ChartEditor の <曲名>.wav 等の命名互換)
        string baseDir   = ResolveBaseDir(songId);
        string[] fixedNames = { "audio.ogg", "audio.mp3", "audio.wav" };
        var attempts = new System.Collections.Generic.List<string>();

        foreach (var n in fixedNames)
            attempts.Add(Path.Combine(baseDir, n));

        // Directory.GetFiles は jar: パスでは使えないため StreamingAssets-on-Android では skip
        if (!baseDir.StartsWith("jar:") && Directory.Exists(baseDir))
        {
            foreach (var ext in new[] { "*.ogg", "*.mp3", "*.wav" })
            {
                foreach (var p in Directory.GetFiles(baseDir, ext))
                    if (!attempts.Contains(p)) attempts.Add(p);
            }
        }

        foreach (var raw0 in attempts)
        {
            string raw = raw0.Replace("\\", "/");
            bool canCheckFile = !raw.StartsWith("jar:");
            if (canCheckFile && !File.Exists(raw))
                continue;

            string url = raw.StartsWith("jar:") || raw.StartsWith("http")
                ? raw
                : "file:///" + raw.TrimStart('/');

            string fileName = Path.GetFileName(raw);
            AudioType audioType = GetAudioType(fileName);
            var clip = await TryLoadClip(url, audioType, songId, fileName);
            if (clip != null) return clip;

            if (audioType != AudioType.UNKNOWN)
            {
                clip = await TryLoadClip(url, AudioType.UNKNOWN, songId, fileName + "(UNKNOWN)");
                if (clip != null) return clip;
            }
        }

        throw new FileNotFoundException(
            string.Format("[ChartLoader] No loadable audio in '{0}' (tried audio.* + folder scan)", baseDir));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static async Task<AudioClip> TryLoadClip(string url, AudioType audioType,
                                              string songId, string tag)
    {
        try
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
                var op = req.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning(string.Format(
                        "[ChartLoader] HTTP fail [{0}] {1}: {2}", tag, url, req.error));
                    return null;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null)
                {
                    Debug.LogWarning(string.Format(
                        "[ChartLoader] GetContent returned null [{0}] {1}", tag, url));
                    return null;
                }

                Debug.Log(string.Format(
                    "[ChartLoader] Loaded [{0}] {1}  samples={2}  freq={3}",
                    tag, url, clip.samples, clip.frequency));
                return clip;
            }
        }
        catch (FormatException ex)
        {
            // Unity's OGG/audio decoder throws FormatException on bad headers.
            Debug.LogWarning(string.Format(
                "[ChartLoader] FormatException [{0}] {1}: {2}", tag, url, ex.Message));
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning(string.Format(
                "[ChartLoader] Exception [{0}] {1}: {2}", tag, url, ex.Message));
            return null;
        }
    }

    static AudioType GetAudioType(string fileName)
    {
        if (fileName.EndsWith(".ogg",  StringComparison.OrdinalIgnoreCase)) return AudioType.OGGVORBIS;
        if (fileName.EndsWith(".mp3",  StringComparison.OrdinalIgnoreCase)) return AudioType.MPEG;
        if (fileName.EndsWith(".wav",  StringComparison.OrdinalIgnoreCase)) return AudioType.WAV;
        return AudioType.UNKNOWN;
    }

    private static async Task<string> ReadTextAsync(string filePath)
    {
        string url = filePath.Replace("\\", "/");
        // file:/// needed on Windows/macOS; Android streaming assets already use jar: prefix
        if (!url.StartsWith("jar:") && !url.StartsWith("http"))
            url = "file:///" + url.TrimStart('/');

        using (var req = UnityWebRequest.Get(url))
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception("[ChartLoader] Failed: " + req.error + "  url=" + url);

            return req.downloadHandler.text;
        }
    }
}
