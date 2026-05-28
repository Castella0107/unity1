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

    /// <summary>保存先ルートディレクトリを準備する。</summary>
    public void Initialize()
    {
        _root = Path.Combine(Application.persistentDataPath, "replays");
        if (!Directory.Exists(_root)) Directory.CreateDirectory(_root);
    }

    /// <summary>リプレイをエンコードして YYYY/MM/{playId}.replay に書き出す。成功時は絶対パス、失敗時 null。</summary>
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

    /// <summary>リプレイファイルが存在するか。</summary>
    public bool Exists(string replayPath)
        => !string.IsNullOrEmpty(replayPath) && File.Exists(replayPath);

    /// <summary>
    /// リプレイファイルを削除する。空パス・既に存在しない場合も true(冪等)。
    /// ソロのベスト以外の刈り込みと PVP 履歴のリングバッファ削除から呼ばれる。
    /// </summary>
    public bool Delete(string replayPath)
    {
        try
        {
            if (string.IsNullOrEmpty(replayPath)) return true;
            if (File.Exists(replayPath)) File.Delete(replayPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ReplayStorage] Delete failed for " + replayPath + ": " + e.Message);
            return false;
        }
    }

    /// <summary>リプレイファイルのバイト列を非同期で読み込む(存在しなければ null)。</summary>
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

    /// <summary>保存済みリプレイファイルの総数を返す。</summary>
    public int  GetReplayCount() => Directory.Exists(_root)
        ? Directory.GetFiles(_root, "*.replay", SearchOption.AllDirectories).Length : 0;

    /// <summary>保存済みリプレイファイルの合計バイト数を返す。</summary>
    public long GetTotalSize()
    {
        if (!Directory.Exists(_root)) return 0;
        long total = 0;
        foreach (string f in Directory.GetFiles(_root, "*.replay", SearchOption.AllDirectories))
            total += new FileInfo(f).Length;
        return total;
    }
}
