// Unity-independent. No UnityEngine references allowed in this assembly.
/// <summary>
/// CRC32チェックサムの計算ユーティリティクラス。リプレイデータの整合性検証に使用する。
/// </summary>
public static class Crc32
{
    static readonly uint[] Table = MakeTable();

    static uint[] MakeTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
