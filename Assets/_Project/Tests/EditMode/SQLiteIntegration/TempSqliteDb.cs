using System;
using System.IO;

// テスト毎にテンポラリDBファイルを作り、終了時に削除するヘルパー。
public class TempSqliteDb : IDisposable
{
    public string FilePath { get; }

    public TempSqliteDb()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid().ToString("N") + ".db");
    }

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
