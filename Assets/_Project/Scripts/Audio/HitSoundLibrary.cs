using UnityEngine;

// Generates and caches all hit-sound AudioClips on demand.
/// <summary>
/// ヒット効果音の AudioClip をオンデマンドで生成・キャッシュするライブラリ。
/// TapClick および各判定（PerfectPlus / Perfect / Great / Good / Miss）に対応するクリップを提供する。
/// </summary>
public class HitSoundLibrary
{
    /// <summary>タップ時のクリック音。</summary>
    public AudioClip TapClick   { get; private set; }
    /// <summary>PerfectPlus 判定音。</summary>
    public AudioClip PerfectPlus { get; private set; }
    /// <summary>Perfect 判定音。</summary>
    public AudioClip Perfect    { get; private set; }
    /// <summary>Great 判定音。</summary>
    public AudioClip Great      { get; private set; }
    /// <summary>Good 判定音。</summary>
    public AudioClip Good       { get; private set; }
    /// <summary>Miss 判定音(不協和音)。</summary>
    public AudioClip Miss       { get; private set; }

    /// <summary>全ヒット音クリップを生成してプロパティにキャッシュする。</summary>
    public void GenerateAll()
    {
        TapClick = SineWaveGenerator.Generate(
            "TapClick",   frequency: 1200, durationSec: 0.020f, volume: 0.4f,
            attackMs: 1,  decayMs: 5,  sustainLevel: 0.3f, releaseMs: 13f);

        PerfectPlus = SineWaveGenerator.Generate(
            "PerfectPlus", frequency: 1500, durationSec: 0.080f, volume: 0.5f,
            attackMs: 2,  decayMs: 10, sustainLevel: 0.6f, releaseMs: 60f);

        Perfect = SineWaveGenerator.Generate(
            "Perfect",    frequency: 1000, durationSec: 0.080f, volume: 0.45f,
            attackMs: 2,  decayMs: 10, sustainLevel: 0.6f, releaseMs: 60f);

        Great = SineWaveGenerator.Generate(
            "Great",      frequency: 700,  durationSec: 0.080f, volume: 0.4f,
            attackMs: 2,  decayMs: 10, sustainLevel: 0.6f, releaseMs: 60f);

        Good = SineWaveGenerator.Generate(
            "Good",       frequency: 500,  durationSec: 0.120f, volume: 0.4f,
            attackMs: 3,  decayMs: 15, sustainLevel: 0.5f, releaseMs: 90f);

        Miss = SineWaveGenerator.GenerateDissonant(
            "Miss",       baseFreq: 200,   durationSec: 0.150f, volume: 0.45f);
    }

    /// <summary>判定に対応するヒット音クリップを返す。</summary>
    public AudioClip GetForJudgment(Judgment j)
    {
        switch (j)
        {
            case Judgment.PerfectPlus: return PerfectPlus;
            case Judgment.Perfect:     return Perfect;
            case Judgment.Great:       return Great;
            case Judgment.Good:        return Good;
            case Judgment.Miss:        return Miss;
            default:                   return null;
        }
    }
}
