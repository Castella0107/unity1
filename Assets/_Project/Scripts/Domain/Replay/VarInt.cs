using System.IO;

// Unity-independent. No UnityEngine references allowed in this assembly.
// ZigZag + VLQ variable-length integer encoding.
// Positive values: small numbers fit in 1 byte; large in up to 5 bytes.
// Negative values: ZigZag maps them to small positive numbers when magnitude is small.
public static class VarInt
{
    static uint ZigZagEncode(int value) => (uint)((value << 1) ^ (value >> 31));
    static int  ZigZagDecode(uint value) => (int)((value >> 1) ^ -(int)(value & 1));

    public static void WriteSignedVarInt(BinaryWriter writer, int value)
    {
        uint v = ZigZagEncode(value);
        while ((v & ~0x7Fu) != 0)
        {
            writer.Write((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }
        writer.Write((byte)v);
    }

    public static int ReadSignedVarInt(BinaryReader reader)
    {
        uint result = 0;
        int  shift  = 0;
        while (true)
        {
            byte b = reader.ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift >= 35) throw new InvalidDataException("VarInt overflow");
        }
        return ZigZagDecode(result);
    }
}
