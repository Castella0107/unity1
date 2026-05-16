using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// リプレイファイルを Application.persistentDataPath/replays/YYYY/MM/{playId}.replay の形式で管理するクラス。
/// </summary>
// Manages replay files under Application.persistentDataPath/replays/YYYY/MM/{playId}.replay
public class ReplayStorage
{
    string _root;

    public void Initialize()
    {
        _root = Path.Combine(Application.persistentDataPath, "replays");
        if (!Directory.Exists(_root)) Directory.CreateDirectory(_root);
    }

    /// Encode and write a replay file. Returns the absolute path, or null on failure.
    public async Task<string> SaveAsync(string playId, ReplayData data, long playedAtUnixMs)
    {
        if (string.IsNullOrEmpty(_root)) Initialize();
        try
        {
            var dt  = DateTimeOffset.FromUnixTimeMilliseconds(playedAtUnixMs);
            var dir = Path.Combine(_root, dt.ToString("yyyy"), dt.ToString("MM"));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path  = Path.Combine(dir, playId + ".replay");
            byte[] bytes = ReplayEncoder.Encode(data);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write,
                                            FileShare.None, 4096, useAsync: true))
                await fs.WriteAsync(bytes, 0, bytes.Length);

            Debug.Log(string.Format("[ReplayStorage] Saved {0} ({1} bytes)", path, bytes.Length));
            return path;
        }
        catch (Exception e)
        {
            Debug.LogError("[ReplayStorage] SaveAsync failed: " + e.Message);
            return null;
        }
    }

    public bool Exists(string replayPath)
        => !string.IsNullOrEmpty(replayPath) && File.Exists(replayPath);

    public async Task<byte[]> ReadAsync(string replayPath)
    {
        if (!Exists(replayPath)) return null;
        byte[] buf = null;
        using (var fs = new FileStream(replayPath, FileMode.Open, FileAccess.Read,
                                        FileShare.Read, 4096, useAsync: true))
        {
            buf = new byte[fs.Length];
            await fs.ReadAsync(buf, 0, buf.Length);
        }
        return buf;
    }

    public int  GetReplayCount() => Directory.Exists(_root)
        ? Directory.GetFiles(_root, "*.replay", SearchOption.AllDirectories).Length : 0;

    public long GetTotalSize()
    {
        if (!Directory.Exists(_root)) return 0;
        long total = 0;
        foreach (string f in Directory.GetFiles(_root, "*.replay", SearchOption.AllDirectories))
            total += new FileInfo(f).Length;
        return total;
    }
}
