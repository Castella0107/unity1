using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 譜面自動生成用の音源解析。BeatDetector と同じ STFT 基盤 (Hann 窓, frameSize=1024, hop=512) の上で、
/// 帯域別 (低=キック/ベース、中=スネア/ボーカル/メロディ、高=ハイハット/シンバル) のオンセット検出、
/// 持続音区間 (ホールド候補) の抽出、時間方向のエネルギープロファイル (曲の盛り上がり) を返す。
/// BeatDetector が「拍格子を求める」解析なのに対し、こちらは「どこに何のノーツを置くか」の材料を出す。
/// </summary>
public static class OnsetAnalyzer
{
    /// <summary>解析帯域。バンド境界は <see cref="BandEdgesHz"/> を参照。</summary>
    public enum Band { Low = 0, Mid = 1, High = 2 }

    /// <summary>帯域境界 (Hz)。Low=[20,160), Mid=[160,2000), High=[2000,16000)。</summary>
    public static readonly double[] BandEdgesHz = { 20.0, 160.0, 2000.0, 16000.0 };

    /// <summary>1つのオンセット。Strength は帯域内正規化 [0,1]。</summary>
    public sealed class Onset
    {
        /// <summary>オンセット時刻 (ms)。</summary>
        public double TimeMs;
        /// <summary>帯域内で正規化した強度 [0,1]。</summary>
        public double Strength;
        /// <summary>検出帯域。</summary>
        public Band   SourceBand;
    }

    /// <summary>持続音区間 (ホールドノーツ候補)。エネルギー包絡のプラトーから抽出。</summary>
    public sealed class Sustain
    {
        /// <summary>区間開始時刻 (ms)。</summary>
        public double StartMs;
        /// <summary>区間終了時刻 (ms)。</summary>
        public double EndMs;
        /// <summary>区間の平均エネルギー (帯域内正規化 [0,1])。</summary>
        public double Strength;
        /// <summary>検出帯域。</summary>
        public Band   SourceBand;
        /// <summary>区間長 (ms)。</summary>
        public double DurationMs => EndMs - StartMs;
    }

    /// <summary>解析結果一式。</summary>
    public sealed class Result
    {
        /// <summary>帯域別オンセット列 (各帯域内で時刻昇順)。index は <see cref="Band"/>。</summary>
        public List<Onset>[]  BandOnsets = { new List<Onset>(), new List<Onset>(), new List<Onset>() };
        /// <summary>全帯域を統合したオンセット列 (時刻昇順)。近接 (≤30ms) は最強帯域の1件に統合。</summary>
        public List<Onset>    MergedOnsets = new List<Onset>();
        /// <summary>持続音区間 (時刻昇順)。中域から抽出。</summary>
        public List<Sustain>  Sustains = new List<Sustain>();
        /// <summary>
        /// エネルギープロファイル。<see cref="EnergyWindowMs"/> ごとの RMS を全曲最大で正規化した [0,1] 列。
        /// index i の窓は [i*EnergyWindowMs, (i+1)*EnergyWindowMs)。曲の静/動に応じた密度スケーリングに使う。
        /// </summary>
        public double[]       EnergyProfile = Array.Empty<double>();
        /// <summary>EnergyProfile の窓幅 (ms)。</summary>
        public double         EnergyWindowMs = 1000.0;

        /// <summary>時刻 t (ms) 近傍のエネルギー [0,1] を返す。範囲外は端の値。</summary>
        public double EnergyAt(double timeMs)
        {
            if (EnergyProfile.Length == 0) return 1.0;
            int i = (int)(timeMs / EnergyWindowMs);
            if (i < 0) i = 0;
            if (i >= EnergyProfile.Length) i = EnergyProfile.Length - 1;
            return EnergyProfile[i];
        }
    }

    const int    FrameSize        = 1024;
    const int    Hop              = 512;
    const int    MedianWin        = 21;      // 適応閾値の中央値ウィンドウ(フレーム)
    const double OnsetMinGapMs    = 50.0;    // 同一帯域内の連続オンセット最小間隔
    const double OnsetThreshold   = 0.08;    // 帯域別正規化フラックスの最小ピーク値
    const double MergeToleranceMs = 30.0;    // 帯域間オンセット統合の許容差
    const double SustainMinMs     = 350.0;   // 持続音として採用する最小長
    const double SustainEnterRatio = 0.45;   // 包絡が局所基準の何倍で持続開始とみなすか
    const double SustainExitRatio  = 0.30;   // 何倍を下回ったら持続終了か (ヒステリシス)

    /// <summary>
    /// モノラル PCM (float[-1..1]) と samplerate から解析結果一式を返す。
    /// stereo は呼び出し側で <see cref="BeatDetector.ToMono"/> でダウンミックスして渡すこと。
    /// </summary>
    public static Result Analyze(float[] samples, int sampleRate)
    {
        var result = new Result();
        if (samples == null || sampleRate <= 0)  return result;
        if (samples.Length < FrameSize * 8)      return result;
        int frames = (samples.Length - FrameSize) / Hop + 1;
        if (frames < 16) return result;

        double frameToMs = (double)Hop / sampleRate * 1000.0;

        // 1パスの STFT で帯域別フラックスと帯域別エネルギー包絡を同時に得る
        ComputeBandFluxAndEnergy(samples, sampleRate, frames,
                                 out var bandFlux, out var bandEnergy);

        // 帯域別オンセット: 中央値減算正規化 → ピークピック
        for (int b = 0; b < 3; b++)
        {
            var norm = MedianSubtractAndNormalize(bandFlux[b], MedianWin);
            var picks = PickOnsets(norm, sampleRate);
            var list  = result.BandOnsets[b];
            for (int i = 0; i < picks.Count; i++)
            {
                list.Add(new Onset
                {
                    TimeMs     = picks[i] * frameToMs,
                    Strength   = norm[picks[i]],
                    SourceBand = (Band)b,
                });
            }
        }

        result.MergedOnsets = MergeOnsets(result.BandOnsets);

        // 持続音 (ホールド候補) は中域のエネルギー包絡から抽出
        result.Sustains = ExtractSustains(bandEnergy[(int)Band.Mid], frameToMs, Band.Mid);

        // エネルギープロファイル (1秒窓 RMS, 全曲最大で正規化)
        result.EnergyProfile = ComputeEnergyProfile(samples, sampleRate, result.EnergyWindowMs);
        return result;
    }

    /// <summary>STFT 1パスで帯域別スペクトラル・フラックス (positive only) と帯域別エネルギー包絡を計算。</summary>
    static void ComputeBandFluxAndEnergy(float[] samples, int sampleRate, int frames,
                                         out double[][] flux, out double[][] energy)
    {
        int spec = FrameSize / 2 + 1;
        double binHz = (double)sampleRate / FrameSize;

        // 各 bin の所属帯域 (-1 = 対象外)
        var binBand = new int[spec];
        for (int k = 0; k < spec; k++)
        {
            double hz = k * binHz;
            binBand[k] = -1;
            for (int b = 0; b < 3; b++)
                if (hz >= BandEdgesHz[b] && hz < BandEdgesHz[b + 1]) { binBand[k] = b; break; }
        }

        var window = new double[FrameSize];
        for (int i = 0; i < FrameSize; i++)
            window[i] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (FrameSize - 1));

        var re      = new double[FrameSize];
        var im      = new double[FrameSize];
        var prevMag = new double[spec];
        var curMag  = new double[spec];
        flux   = new[] { new double[frames], new double[frames], new double[frames] };
        energy = new[] { new double[frames], new double[frames], new double[frames] };

        for (int f = 0; f < frames; f++)
        {
            int s0 = f * Hop;
            for (int i = 0; i < FrameSize; i++)
            {
                re[i] = samples[s0 + i] * window[i];
                im[i] = 0;
            }
            Fft(re, im);
            for (int k = 0; k < spec; k++)
                curMag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);

            for (int k = 1; k < spec; k++)
            {
                int b = binBand[k];
                if (b < 0) continue;
                energy[b][f] += curMag[k];
                if (f > 0)
                {
                    double d = curMag[k] - prevMag[k];
                    if (d > 0) flux[b][f] += d;
                }
            }
            var swap = prevMag; prevMag = curMag; curMag = swap;
        }
    }

    /// <summary>各点で局所中央値を引いて正の残差にし、最大値 1 に正規化する (BeatDetector と同一手法)。</summary>
    static double[] MedianSubtractAndNormalize(double[] flux, int win)
    {
        int n = flux.Length;
        var norm = new double[n];
        var buf  = new double[win];
        int half = win / 2;
        for (int f = 0; f < n; f++)
        {
            int s0 = Math.Max(0, f - half);
            int s1 = Math.Min(n - 1, f + half);
            int k  = s1 - s0 + 1;
            Array.Copy(flux, s0, buf, 0, k);
            Array.Sort(buf, 0, k);
            double med = buf[k / 2];
            norm[f] = Math.Max(0, flux[f] - med);
        }
        double max = 0;
        for (int i = 0; i < n; i++) if (norm[i] > max) max = norm[i];
        if (max > 0) { for (int i = 0; i < n; i++) norm[i] /= max; }
        return norm;
    }

    /// <summary>正規化フラックスの局所最大を取得 + 最小ギャップ拘束。</summary>
    static List<int> PickOnsets(double[] norm, int sampleRate)
    {
        int n = norm.Length;
        var result = new List<int>();
        double frameRate = (double)sampleRate / Hop;
        int minGapFrames = Math.Max(1, (int)Math.Round(OnsetMinGapMs * frameRate / 1000.0));
        for (int f = 1; f < n - 1; f++)
        {
            if (norm[f] > norm[f - 1] && norm[f] >= norm[f + 1] && norm[f] > OnsetThreshold)
            {
                if (result.Count == 0 || f - result[result.Count - 1] >= minGapFrames)
                    result.Add(f);
            }
        }
        return result;
    }

    /// <summary>帯域別オンセットを統合。近接 (≤ MergeToleranceMs) は最強の1件に代表させる。</summary>
    static List<Onset> MergeOnsets(List<Onset>[] bandOnsets)
    {
        var all = new List<Onset>();
        for (int b = 0; b < bandOnsets.Length; b++) all.AddRange(bandOnsets[b]);
        all.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        var merged = new List<Onset>();
        for (int i = 0; i < all.Count; i++)
        {
            var cur = all[i];
            if (merged.Count > 0 && cur.TimeMs - merged[merged.Count - 1].TimeMs <= MergeToleranceMs)
            {
                if (cur.Strength > merged[merged.Count - 1].Strength)
                    merged[merged.Count - 1] = cur;
                continue;
            }
            merged.Add(new Onset { TimeMs = cur.TimeMs, Strength = cur.Strength, SourceBand = cur.SourceBand });
        }
        return merged;
    }

    /// <summary>
    /// 帯域エネルギー包絡のプラトーから持続音区間を抽出する。
    /// 局所基準 (移動窓最大) に対する比でヒステリシス判定し、SustainMinMs 未満の区間は捨てる。
    /// </summary>
    static List<Sustain> ExtractSustains(double[] rawEnergy, double frameToMs, Band band)
    {
        int n = rawEnergy.Length;
        var result = new List<Sustain>();
        if (n < 8) return result;

        // 平滑化 (移動平均 ±2 フレーム ≒ ±12ms)
        var env = new double[n];
        for (int f = 0; f < n; f++)
        {
            int s0 = Math.Max(0, f - 2), s1 = Math.Min(n - 1, f + 2);
            double s = 0;
            for (int i = s0; i <= s1; i++) s += rawEnergy[i];
            env[f] = s / (s1 - s0 + 1);
        }

        // 局所基準: ±3秒窓の最大値。静かな曲でも相対判定できるようにする。
        int halfWin = Math.Max(1, (int)(3000.0 / frameToMs));
        var localRef = new double[n];
        {
            // スライディング最大 (単調 deque)
            var deque = new int[n];
            int head = 0, tail = 0;
            int right = -1;
            for (int f = 0; f < n; f++)
            {
                int targetRight = Math.Min(n - 1, f + halfWin);
                while (right < targetRight)
                {
                    right++;
                    while (tail > head && env[deque[tail - 1]] <= env[right]) tail--;
                    deque[tail++] = right;
                }
                while (head < tail && deque[head] < f - halfWin) head++;
                localRef[f] = (head < tail) ? env[deque[head]] : env[f];
            }
        }

        bool inSeg = false;
        int segStart = 0;
        double segSum = 0; int segCount = 0;
        for (int f = 0; f < n; f++)
        {
            double reference = Math.Max(localRef[f], 1e-9);
            double ratio = env[f] / reference;
            if (!inSeg)
            {
                if (ratio >= SustainEnterRatio)
                {
                    inSeg = true; segStart = f; segSum = 0; segCount = 0;
                }
            }
            if (inSeg)
            {
                segSum += ratio; segCount++;
                bool ended = (ratio < SustainExitRatio) || (f == n - 1);
                if (ended)
                {
                    double startMs = segStart * frameToMs;
                    double endMs   = f * frameToMs;
                    if (endMs - startMs >= SustainMinMs)
                    {
                        result.Add(new Sustain
                        {
                            StartMs    = startMs,
                            EndMs      = endMs,
                            Strength   = Math.Min(1.0, segSum / segCount),
                            SourceBand = band,
                        });
                    }
                    inSeg = false;
                }
            }
        }
        return result;
    }

    /// <summary>windowMs ごとの RMS を全曲最大で正規化した [0,1] 列を返す。</summary>
    static double[] ComputeEnergyProfile(float[] samples, int sampleRate, double windowMs)
    {
        int winSamples = Math.Max(1, (int)(windowMs / 1000.0 * sampleRate));
        int windows = (samples.Length + winSamples - 1) / winSamples;
        var profile = new double[windows];
        for (int w = 0; w < windows; w++)
        {
            int s0 = w * winSamples;
            int s1 = Math.Min(samples.Length, s0 + winSamples);
            double sum = 0;
            for (int i = s0; i < s1; i++) sum += (double)samples[i] * samples[i];
            profile[w] = Math.Sqrt(sum / Math.Max(1, s1 - s0));
        }
        double max = 0;
        for (int i = 0; i < windows; i++) if (profile[i] > max) max = profile[i];
        if (max > 0) { for (int i = 0; i < windows; i++) profile[i] /= max; }
        return profile;
    }

    /// <summary>In-place Cooley-Tukey radix-2 forward FFT (BeatDetector と同一実装)。入力長は 2 の冪。</summary>
    static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        int j = 0;
        for (int i = 1; i < n; i++)
        {
            int bit = n >> 1;
            while ((j & bit) != 0) { j ^= bit; bit >>= 1; }
            j |= bit;
            if (i < j)
            {
                double tr = re[i]; re[i] = re[j]; re[j] = tr;
                double ti = im[i]; im[i] = im[j]; im[j] = ti;
            }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle  = -2.0 * Math.PI / len;
            double wlenRe = Math.Cos(angle);
            double wlenIm = Math.Sin(angle);
            int half = len >> 1;
            for (int i = 0; i < n; i += len)
            {
                double wRe = 1, wIm = 0;
                for (int k = 0; k < half; k++)
                {
                    int idx1 = i + k;
                    int idx2 = idx1 + half;
                    double r2 = re[idx2], i2 = im[idx2];
                    double vRe = r2 * wRe - i2 * wIm;
                    double vIm = r2 * wIm + i2 * wRe;
                    re[idx2] = re[idx1] - vRe;
                    im[idx2] = im[idx1] - vIm;
                    re[idx1] += vRe;
                    im[idx1] += vIm;
                    double nwRe = wRe * wlenRe - wIm * wlenIm;
                    double nwIm = wRe * wlenIm + wIm * wlenRe;
                    wRe = nwRe; wIm = nwIm;
                }
            }
        }
    }
}
