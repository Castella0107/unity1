using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 楽曲音声からの簡易ビート検出。短時間エネルギー変化 (energy onset / spectral flux 風)
/// と onset 間隔の自己相関で BPM を推定する。
/// 厳密な FFT は使わず、時間領域の差分エネルギー累積で済ませる軽量実装。
/// </summary>
public static class BeatDetector
{
    /// <summary>検出結果。</summary>
    public sealed class Result
    {
        /// <summary>推定 BPM(検出失敗時は 0)。</summary>
        public double      EstimatedBpm;
        /// <summary>最初のオンセット時刻(ms)。</summary>
        public double      FirstOnsetMs;
        /// <summary>検出した全オンセット時刻(ms)。</summary>
        public List<double> OnsetTimesMs = new List<double>();
    }

    /// <summary>
    /// モノラル PCM (float[-1..1]) と samplerate からビートを推定。
    /// stereo の場合は呼び出し側でダウンミックスして渡すこと。
    /// </summary>
    /// <param name="samples">モノラル PCM</param>
    /// <param name="sampleRate">Hz</param>
    /// <param name="minBpm">推定下限 (default 60)</param>
    /// <param name="maxBpm">推定上限 (default 240)</param>
    public static Result Detect(float[] samples, int sampleRate, double minBpm = 60.0, double maxBpm = 240.0)
    {
        if (samples == null || samples.Length == 0 || sampleRate <= 0)
            return new Result { EstimatedBpm = 0, FirstOnsetMs = 0 };

        // 1. Frame-wise short-time energy (frame = ~23ms @ 44.1k → 1024 samples)
        int frameSize = NextPow2(sampleRate / 43);   // ~23ms
        if (frameSize < 256)  frameSize = 256;
        int hop = frameSize / 2;
        int frames = (samples.Length - frameSize) / hop + 1;
        if (frames < 8) return new Result { EstimatedBpm = 0, FirstOnsetMs = 0 };

        var energy = new double[frames];
        for (int f = 0; f < frames; f++)
        {
            int s0 = f * hop;
            double e = 0;
            for (int i = 0; i < frameSize; i++)
            {
                float v = samples[s0 + i];
                e += v * v;
            }
            energy[f] = e / frameSize;
        }

        // 2. Flux = positive difference of energy across frames
        var flux = new double[frames];
        for (int f = 1; f < frames; f++)
        {
            double d = energy[f] - energy[f - 1];
            flux[f] = d > 0 ? d : 0;
        }

        // 3. Adaptive thresholding: local mean + std over 21-frame window
        var onsetFrames = new List<int>();
        int win = 21, half = win / 2;
        for (int f = half; f < frames - half; f++)
        {
            double sum = 0, sumSq = 0;
            for (int k = -half; k <= half; k++)
            {
                double v = flux[f + k];
                sum += v; sumSq += v * v;
            }
            double mean = sum / win;
            double var  = sumSq / win - mean * mean;
            double std  = var > 0 ? Math.Sqrt(var) : 0;
            double thr  = mean + std * 1.5;
            if (flux[f] > thr && flux[f] > 1e-7)
            {
                bool isPeak = (f == 0 || flux[f] >= flux[f - 1]) && (f == frames - 1 || flux[f] >= flux[f + 1]);
                if (isPeak)
                {
                    // Suppress double-trigger within ~50ms
                    double tMs = (double)(f * hop) / sampleRate * 1000.0;
                    if (onsetFrames.Count == 0 || tMs - (onsetFrames[onsetFrames.Count - 1] * hop * 1000.0 / sampleRate) > 50.0)
                        onsetFrames.Add(f);
                }
            }
        }

        var result = new Result();
        for (int i = 0; i < onsetFrames.Count; i++)
            result.OnsetTimesMs.Add((double)(onsetFrames[i] * hop) / sampleRate * 1000.0);

        if (result.OnsetTimesMs.Count > 0)
            result.FirstOnsetMs = result.OnsetTimesMs[0];

        // 4. BPM via histogram of inter-onset intervals (IOI), folded into [minBpm, maxBpm]
        result.EstimatedBpm = EstimateBpmFromIois(result.OnsetTimesMs, minBpm, maxBpm);
        return result;
    }

    /// <summary>IOI ヒストグラム + オクターブ折り畳みで BPM 推定。</summary>
    public static double EstimateBpmFromIois(List<double> onsetTimesMs, double minBpm, double maxBpm)
    {
        if (onsetTimesMs == null || onsetTimesMs.Count < 4) return 0;

        // Collect short IOIs (< 2 seconds), fold candidate BPMs into [minBpm, maxBpm]
        var bpmVotes = new Dictionary<int, double>();
        for (int i = 1; i < onsetTimesMs.Count; i++)
        {
            double iMs = onsetTimesMs[i] - onsetTimesMs[i - 1];
            if (iMs < 50 || iMs > 2000) continue;
            double bpm = 60000.0 / iMs;
            while (bpm > maxBpm) bpm *= 0.5;
            while (bpm < minBpm) bpm *= 2.0;
            int key = (int)Math.Round(bpm);
            // bucket within ±2 BPM
            for (int k = -2; k <= 2; k++)
            {
                int b = key + k;
                if (b < (int)minBpm || b > (int)maxBpm) continue;
                bpmVotes.TryGetValue(b, out double w);
                bpmVotes[b] = w + Math.Exp(-(k * k) / 2.0);
            }
        }
        if (bpmVotes.Count == 0) return 0;

        double bestW = 0; int bestB = 0;
        foreach (var kv in bpmVotes) if (kv.Value > bestW) { bestW = kv.Value; bestB = kv.Key; }
        return bestB;
    }

    static int NextPow2(int n)
    {
        int p = 1; while (p < n) p <<= 1; return p;
    }

    /// <summary>2ch stereo float をモノラル平均にダウンミックス。</summary>
    public static float[] ToMono(float[] interleaved, int channels)
    {
        if (channels <= 1) return interleaved;
        int frames = interleaved.Length / channels;
        var mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float s = 0;
            for (int c = 0; c < channels; c++) s += interleaved[i * channels + c];
            mono[i] = s / channels;
        }
        return mono;
    }
}
