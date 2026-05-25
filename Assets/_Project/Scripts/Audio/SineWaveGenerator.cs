using UnityEngine;

// Unity-independent logic to generate AudioClips from sine waves with ADSR envelopes.
/// <summary>
/// ADSR エンベロープ付きサイン波から AudioClip を生成するユーティリティクラス（Unity 非依存ロジック）。
/// 単一周波数の Generate と、Miss 音用の不協和音 GenerateDissonant を提供する。
/// </summary>
public static class SineWaveGenerator
{
    const int SAMPLE_RATE = 44100;

    /// <summary>ADSR エンベロープ付きの単一周波数サイン波 AudioClip を生成する。</summary>
    public static AudioClip Generate(
        string name, float frequency, float durationSec,
        float volume       = 0.5f,
        float attackMs     = 2f,
        float decayMs      = 5f,
        float sustainLevel = 0.7f,
        float releaseMs    = 30f)
    {
        int total   = Mathf.CeilToInt(SAMPLE_RATE * durationSec);
        int attack  = Mathf.CeilToInt(SAMPLE_RATE * attackMs  / 1000f);
        int decay   = Mathf.CeilToInt(SAMPLE_RATE * decayMs   / 1000f);
        int release = Mathf.CeilToInt(SAMPLE_RATE * releaseMs / 1000f);
        int sustStart = attack + decay;
        int relStart  = total  - release;

        var data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float sine = Mathf.Sin(2f * Mathf.PI * frequency * i / SAMPLE_RATE);

            float env;
            if (i < attack)
                env = (float)i / attack;
            else if (i < sustStart)
                env = Mathf.Lerp(1f, sustainLevel, (float)(i - attack) / decay);
            else if (i < relStart)
                env = sustainLevel;
            else
                env = Mathf.Lerp(sustainLevel, 0f, (float)(i - relStart) / release);

            data[i] = sine * env * volume;
        }

        var clip = AudioClip.Create(name, total, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Miss 音用の二周波数不協和音 AudioClip を生成する。</summary>
    public static AudioClip GenerateDissonant(
        string name, float baseFreq, float durationSec, float volume = 0.5f)
    {
        int total   = Mathf.CeilToInt(SAMPLE_RATE * durationSec);
        int release = Mathf.CeilToInt(SAMPLE_RATE * 0.1f);
        int relStart = total - release;

        var data = new float[total];
        for (int i = 0; i < total; i++)
        {
            float s1 = Mathf.Sin(2f * Mathf.PI * baseFreq        * i / SAMPLE_RATE);
            float s2 = Mathf.Sin(2f * Mathf.PI * baseFreq * 1.06f * i / SAMPLE_RATE) * 0.5f;
            float s3 = Mathf.Sin(2f * Mathf.PI * baseFreq * 1.41f * i / SAMPLE_RATE) * 0.3f;

            float env = i < 50       ? (float)i / 50f
                      : i >= relStart ? 1f - (float)(i - relStart) / release
                      : 1f;

            data[i] = (s1 + s2 + s3) / 1.8f * env * volume;
        }

        var clip = AudioClip.Create(name, total, 1, SAMPLE_RATE, false);
        clip.SetData(data, 0);
        return clip;
    }
}
