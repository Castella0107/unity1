using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// テスト用楽曲フォルダーにサイン波ベースの WAV ファイルを生成するエディターオンリーのヘルパークラス。
/// </summary>
public static class TestAudioGenerator
{
    static readonly string[] TestSongIds = { "test_song", "test_song_1", "test_song_2", "test_song_3" };

    [MenuItem("Tools/Generate Test Audio")]
    public static void GenerateTestAudio()
    {
        int count = 0;
        foreach (var songId in TestSongIds)
        {
            string dir     = Path.Combine(Application.streamingAssetsPath, "Songs", songId);
            string outPath = Path.Combine(dir, "audio.wav");

            if (!Directory.Exists(dir))
            {
                Debug.Log("[TestAudioGenerator] Skipping " + songId + " (folder not found)");
                continue;
            }

            if (File.Exists(outPath))
            {
                Debug.Log("[TestAudioGenerator] Already exists, skipping " + songId);
                continue;
            }

            GenerateWav(outPath, songId);
            count++;
        }

        AssetDatabase.Refresh();
        Debug.Log(string.Format("[TestAudioGenerator] Done. Generated {0} file(s).", count));
    }

    static void GenerateWav(string outPath, string songId)
    {
        const int   sampleRate   = 44100;
        const float durationSec  = 65f;   // slightly over the 60-second meta.durationMs
        int         totalSamples = (int)(sampleRate * durationSec);

        var data = new float[totalSamples];

        // Four-chord loop at 120 BPM (0.5 s/beat), with sine-wave envelope per beat
        float[] freqs = { 220f, 277f, 330f, 440f };   // A3 C#4 E4 A4

        for (int i = 0; i < totalSamples; i++)
        {
            float t    = (float)i / sampleRate;
            int   beat = (int)(t * 2) % 4;           // beat index (0-3)
            float freq = freqs[beat];

            // Beat-local phase (0-1 within each 0.5-second beat)
            float beatPhase = (t * 2f) - Mathf.Floor(t * 2f);
            float envelope  = Mathf.Sin(beatPhase * Mathf.PI) * 0.3f;   // rise-fall per beat

            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
        }

        WriteWav(outPath, data, sampleRate, channels: 1);
        Debug.Log("[TestAudioGenerator] Written: " + outPath);
    }

    static void WriteWav(string path, float[] data, int sampleRate, int channels)
    {
        int byteRate       = sampleRate * channels * 2;
        int subchunk2Size  = data.Length * channels * 2;
        int chunkSize      = 36 + subchunk2Size;

        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);                       // Subchunk1Size (PCM)
            bw.Write((short)1);                 // AudioFormat: PCM
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write((short)(channels * 2));    // BlockAlign
            bw.Write((short)16);                // BitsPerSample
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subchunk2Size);

            foreach (float sample in data)
            {
                short s = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767f);
                bw.Write(s);
            }
        }
    }
}
