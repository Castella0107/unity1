using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

// Unity-layer loader. Lives in Game/ (not Domain/) because it depends on UnityEngine.
// Uses UnityWebRequest for Android JAR-path compatibility.

public static class ChartLoader
{
    public static async Task<SongMetadata> LoadMetaAsync(string songId)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Songs", songId, "meta.json");
        string json = await ReadTextAsync(path);
        return ChartParser.ParseMeta(json);
    }

    public static async Task<ChartData> LoadChartAsync(string songId, string difficulty)
    {
        string path = Path.Combine(Application.streamingAssetsPath,
                                   "Songs", songId, "charts", difficulty + ".json");
        string json = await ReadTextAsync(path);
        return ChartParser.ParseChart(json);
    }

    public static async Task<AudioClip> LoadAudioAsync(string songId)
    {
        // Try common audio extensions in priority order
        string[] candidates = { "audio.ogg", "audio.mp3", "audio.wav" };

        foreach (var fileName in candidates)
        {
            string raw = Path.Combine(Application.streamingAssetsPath, "Songs", songId, fileName)
                             .Replace("\\", "/");

            // File-existence check (StreamingAssets on Android is inside a JAR, so skip there)
            bool canCheckFile = !raw.StartsWith("jar:");
            if (canCheckFile && !File.Exists(raw))
                continue;

            string url = raw.StartsWith("jar:") || raw.StartsWith("http")
                ? raw
                : "file:///" + raw.TrimStart('/');

            // 1st attempt: explicit format
            AudioType audioType = GetAudioType(fileName);
            var clip = await TryLoadClip(url, audioType, songId, fileName);
            if (clip != null) return clip;

            // 2nd attempt: UNKNOWN (let Unity auto-detect), skips if same as explicit
            if (audioType != AudioType.UNKNOWN)
            {
                clip = await TryLoadClip(url, AudioType.UNKNOWN, songId, fileName + "(UNKNOWN)");
                if (clip != null) return clip;
            }
        }

        throw new FileNotFoundException(
            string.Format("[ChartLoader] No loadable audio found for songId='{0}'. " +
                          "Tried {1} in Songs/{0}/", songId, string.Join(", ", candidates)));
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
