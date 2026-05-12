using UnityEngine;

// Generates and caches all hit-sound AudioClips on demand.
public class HitSoundLibrary
{
    public AudioClip TapClick   { get; private set; }
    public AudioClip PerfectPlus { get; private set; }
    public AudioClip Perfect    { get; private set; }
    public AudioClip Great      { get; private set; }
    public AudioClip Good       { get; private set; }
    public AudioClip Miss       { get; private set; }

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
