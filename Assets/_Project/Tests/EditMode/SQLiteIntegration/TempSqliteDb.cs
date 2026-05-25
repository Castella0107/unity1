using System;
using System.IO;

/// <summary>テスト毎に一時 DB ファイルを作成し、Dispose 時に関連ファイル(-wal/-shm 含む)を削除するヘルパー。</summary>
public class TempSqliteDb : IDisposable
{
    /// <summary>一時 DB ファイルの絶対パス。</summary>
    public string FilePath { get; }

    /// <summary>一時 DB ファイルパスを採番してインスタンスを生成する。</summary>
    public TempSqliteDb()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N") + ".db");
    }

    /// <summary>一時 DB ファイルと関連ファイルを削除する。</summary>
    public void Dispose()
    {
        try
        {
            if (File.Exists(FilePath))           File.Delete(FilePath);
            if (File.Exists(FilePath + "-wal"))  File.Delete(FilePath + "-wal");
            if (File.Exists(FilePath + "-shm"))  File.Delete(FilePath + "-shm");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[TempSqliteDb] Cleanup failed: " + e.Message);
        }
    }
}
