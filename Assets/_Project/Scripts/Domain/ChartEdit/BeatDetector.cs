using System;
using System.Collections.Generic;

// Unity-independent. Part of Domain assembly.

/// <summary>
/// 楽曲音声からの高精度ビート/BPM 検出。
/// パイプライン:
///   1. STFT (Hann 窓, frameSize=1024, hop=512)
///   2. HFC (High-Frequency Content) 重み付きスペクトラル・フラックス
///   3. 中央値減算による適応閾値正規化(楽曲ダイナミクスに頑健)
///   4. オンセット関数の自己相関 + オクターブ調和スコア
///   5. 放物線補間で小数 BPM (粗推定: ここでは seed)
///   6. 位相最適化で FirstOnsetMs をサブフレーム精度に
///   7. 全曲オンセットに対する段階的ウィンドウ拡張つき反復最小二乗で BPM/位相を高精度化
///      (自己相関の frame 解像度・丸め由来の終盤累積ズレをほぼ解消)
/// FFT は自前実装 (Cooley-Tukey radix-2)。
/// </summary>
public static class BeatDetector
{
    /// <summary>検出結果。</summary>
    public sealed class Result
    {
        /// <summary>推定 BPM(検出失敗時は 0)。最小二乗精緻化により小数 3 桁精度。</summary>
        public double      EstimatedBpm;
        /// <summary>最初のビート時刻 (ms)。BPM 推定後の位相最適化で精密化される。</summary>
        public double      FirstOnsetMs;
        /// <summary>検出した全オンセット時刻(ms)。</summary>
        public List<double> OnsetTimesMs = new List<double>();
    }

    const int    FrameSize        = 1024;     // STFT window size
    const int    Hop              = 512;      // 50% overlap → 約 86 Hz frame rate @44.1kHz
    const int    MedianWin        = 21;       // 適応閾値の中央値ウィンドウ(フレーム)
    const double OnsetMinGapMs    = 50.0;     // 連続オンセットの最小間隔
    const double OnsetThreshold   = 0.05;     // 正規化フラックスの最小ピーク値

    /// <summary>
    /// モノラル PCM (float[-1..1]) と samplerate からビートを推定。
    /// stereo の場合は呼び出し側で <see cref="ToMono"/> でダウンミックスして渡すこと。
    /// </summary>
    public static Result Detect(float[] samples, int sampleRate, double minBpm = 60.0, double maxBpm = 240.0)
    {
        var empty = new Result { EstimatedBpm = 0, FirstOnsetMs = 0 };
        if (samples == null || sampleRate <= 0) return empty;
        if (samples.Length < FrameSize * 8)     return empty;

        int frames = (samples.Length - FrameSize) / Hop + 1;
        if (frames < 16) return empty;

        // 1〜2. STFT + HFC スペクトラル・フラックス
        var flux = ComputeFluxHfc(samples, frames);

        // 3. 中央値除去で適応的にベースラインを引く + [0,1] 正規化
        var norm = MedianSubtractAndNormalize(flux, MedianWin);

        // 4. オンセット位置 (display 用、BPM 推定にも下流で使う norm を返す)
        double frameToMs = (double)Hop / sampleRate * 1000.0;
        var onsetFrames = PickOnsets(norm, sampleRate);
        var result = new Result();
        foreach (var f in onsetFrames) result.OnsetTimesMs.Add(f * frameToMs);

        // 5. 自己相関で BPM 推定 (放物線補間で小数精度)
        double frameRate = (double)sampleRate / Hop;
        int minLag = Math.Max(2,         (int)Math.Floor(60.0 / maxBpm * frameRate));
        int maxLag = Math.Min(frames - 2, (int)Math.Ceiling(60.0 / minBpm * frameRate));
        if (maxLag <= minLag) return result;

        double bpm = EstimateBpmAutocorr(norm, minLag, maxLag, frameRate, minBpm, maxBpm);
        result.EstimatedBpm = bpm;

        // 6. 位相最適化で FirstOnsetMs を精密化
        if (bpm > 0)
        {
            double periodFrames = 60.0 / bpm * frameRate;
            double phaseFrames  = OptimizePhase(norm, periodFrames);
            result.FirstOnsetMs = phaseFrames * frameToMs;

            // 7. 全曲オンセットへの最小二乗で BPM/位相を精緻化。
            //    自己相関は frame 解像度(約 11.6ms)が上限で、わずかな BPM 誤差でも曲後半に向けて
            //    累積し格子が音とズレる。全曲にわたる長い時間基線で直線回帰すると BPM 精度が桁違いに
            //    上がり、終盤ズレをほぼ解消できる。result.EstimatedBpm / FirstOnsetMs を上書きする。
            RefineTempoLeastSquares(result, bpm, result.FirstOnsetMs, minBpm, maxBpm);
        }
        else if (result.OnsetTimesMs.Count > 0)
        {
            result.FirstOnsetMs = result.OnsetTimesMs[0];
        }
        return result;
    }

    /// <summary>
    /// 既知/概算 BPM を seed に、検出済みオンセット列から BPM と最初の拍位置を高精度化する。
    ///
    /// 用途: 自動推定(<see cref="Detect"/>)が拍レベル(オクターブ/分割)を取り違えたとき、人が概算 BPM
    /// (例: ジャケット表記の 180)を与えて精密値(例: 180.003)+ ダウンビートへ追い込む。テンポの
    /// 「どの拍レベルか」の判定は自動では原理的に不安定なため人に委ね、精度は最小二乗で機械が出す分担。
    ///
    /// seed 付近のビート格子へオンセットを当て、<see cref="RefineTempoLeastSquares"/> で精緻化する。
    /// 前段の FFT/オンセット検出は <paramref name="onsetTimesMs"/> を再利用するため再実行不要(軽量)。
    /// </summary>
    public static Result RefineFromOnsets(List<double> onsetTimesMs, double seedBpm,
                                          double minBpm = 60.0, double maxBpm = 240.0)
    {
        var result = new Result { EstimatedBpm = seedBpm, FirstOnsetMs = 0 };
        if (onsetTimesMs == null || onsetTimesMs.Count < 8 || seedBpm <= 0) return result;
        result.OnsetTimesMs.AddRange(onsetTimesMs);

        // 入力 BPM の周辺 ±6% だけを F1(オンセット網羅率×格子占有率)で探索し、最寄りの実拍格子へ
        // スナップしてから精緻化する。局所探索なので 1/2・2・4/3 等の別拍レベルへ飛ばず(=人が与えた
        // 拍レベルを尊重)、概算入力(例 178/182)でも真の格子(180)に吸着できる。
        double snapped   = SnapToLocalGrid(result.OnsetTimesMs, seedBpm, minBpm, maxBpm);
        double seedPhase = BestPhaseMs(result.OnsetTimesMs, snapped);
        result.FirstOnsetMs = seedPhase;
        RefineTempoLeastSquares(result, snapped, seedPhase, minBpm, maxBpm);
        return result;
    }

    /// <summary>
    /// オンセット列から「もっともらしい拍 BPM 候補」を F1(網羅率×占有率)上位順に返す。
    /// 各候補は <see cref="RefineFromOnsets"/> 相当で精密化済み(小数3桁 BPM + FirstOnsetMs)。
    ///
    /// テンポの拍レベル(オクターブ/分割)は自動では一意に決められない(例: 180 の曲が 200 や 90 とも
    /// 部分的に整合する)ため、上位候補を UI に並べて人に選ばせる用途。F1 のピーク(局所最大)を集め、
    /// 近い(±2%)ものを統合して上位 <paramref name="maxCount"/> 件を返す。真の拍が 1 位でなくても
    /// 候補に含まれるよう、オクターブ/分割の関係にある複数レベルを残す。
    /// </summary>
    public static List<Result> RankTempoCandidates(List<double> onsetTimesMs, int maxCount = 4,
                                                   double minBpm = 60.0, double maxBpm = 240.0)
    {
        var outList = new List<Result>();
        if (onsetTimesMs == null || onsetTimesMs.Count < 8) return outList;
        var onsets = new List<double>(onsetTimesMs);

        int steps = (int)((maxBpm - minBpm) / 0.5) + 1;
        if (steps < 3) return outList;
        var bpms = new double[steps];
        var f1s  = new double[steps];
        for (int i = 0; i < steps; i++) { bpms[i] = minBpm + i * 0.5; f1s[i] = GridF1(onsets, bpms[i]); }

        var peaks = new List<(double bpm, double f1)>();
        for (int i = 1; i < steps - 1; i++)
            if (f1s[i] >= f1s[i - 1] && f1s[i] >= f1s[i + 1] && f1s[i] > 0)
                peaks.Add((bpms[i], f1s[i]));
        peaks.Sort((a, b) => b.f1.CompareTo(a.f1));

        foreach (var (bpm, _) in peaks)
        {
            bool dup = false;
            for (int j = 0; j < outList.Count; j++)
                if (Math.Abs(outList[j].EstimatedBpm - bpm) < bpm * 0.02) { dup = true; break; }
            if (dup) continue;

            var r = RefineFromOnsets(onsets, bpm, minBpm, maxBpm);
            if (r.EstimatedBpm <= 0) continue;

            bool dup2 = false;
            for (int j = 0; j < outList.Count; j++)
                if (Math.Abs(outList[j].EstimatedBpm - r.EstimatedBpm) < r.EstimatedBpm * 0.02) { dup2 = true; break; }
            if (dup2) continue;

            outList.Add(r);
            if (outList.Count >= maxCount) break;
        }
        return outList;
    }

    /// <summary>seedBpm の ±6% を 0.25 BPM 刻みで掃引し、F1(網羅率×占有率)最大の BPM を返す。</summary>
    static double SnapToLocalGrid(List<double> onsets, double seedBpm, double minBpm, double maxBpm)
    {
        double lo = Math.Max(minBpm, seedBpm * 0.94);
        double hi = Math.Min(maxBpm, seedBpm * 1.06);
        double best = -1, snap = seedBpm;
        for (double b = lo; b <= hi + 1e-9; b += 0.25)
        {
            double f1 = GridF1(onsets, b);
            if (f1 > best) { best = f1; snap = b; }
        }
        return snap;
    }

    /// <summary>
    /// 指定 BPM のビート格子に対するオンセットの当てはまり度を F1 で返す。
    /// coverage = 格子近傍にあるオンセットの割合、occupancy = オンセットを持つ拍の割合。
    /// 細かすぎる格子(高 BPM)は occupancy が下がり、粗すぎる/ズレた格子は coverage が下がるため、
    /// 真の拍レベル付近で最大化する。
    /// </summary>
    static double GridF1(List<double> onsets, double bpm)
    {
        double period = 60_000.0 / bpm;
        if (period <= 0 || onsets.Count == 0) return 0;
        double phase = BestPhaseMs(onsets, bpm);
        double tol = 0.10 * period;
        int aligned = 0;
        var occupied = new HashSet<long>();
        for (int i = 0; i < onsets.Count; i++)
        {
            long k = (long)Math.Round((onsets[i] - phase) / period);
            if (Math.Abs(onsets[i] - (phase + k * period)) <= tol) { aligned++; occupied.Add(k); }
        }
        long kFirst = (long)Math.Round((onsets[0] - phase) / period);
        long kLast  = (long)Math.Round((onsets[onsets.Count - 1] - phase) / period);
        long gridBeats = Math.Max(1, kLast - kFirst + 1);
        double coverage  = (double)aligned / onsets.Count;
        double occupancy = (double)occupied.Count / gridBeats;
        return (coverage + occupancy > 0) ? 2.0 * coverage * occupancy / (coverage + occupancy) : 0;
    }

    /// <summary>
    /// 指定 BPM のビート周期に対し、オンセットを 1 周期へ畳み込んだヒストグラムの最頻位相(ms)を返す。
    /// 最小二乗精緻化の初期位相(ダウンビート候補)に使う。
    /// </summary>
    static double BestPhaseMs(List<double> onsets, double bpm)
    {
        double period = 60_000.0 / bpm;
        if (period <= 0) return 0;
        const int B = 120;
        var hist = new int[B];
        for (int i = 0; i < onsets.Count; i++)
        {
            double m = onsets[i] - Math.Floor(onsets[i] / period) * period;
            int bin = (int)(m / period * B);
            if (bin < 0) bin = 0; else if (bin >= B) bin = B - 1;
            hist[bin]++;
        }
        int pk = 0;
        for (int i = 1; i < B; i++) if (hist[i] > hist[pk]) pk = i;
        return (pk + 0.5) / B * period;
    }

    /// <summary>Hann 窓 + STFT で HFC 重み付きスペクトラル・フラックス (positive only) を計算する。</summary>
    static double[] ComputeFluxHfc(float[] samples, int frames)
    {
        int spec = FrameSize / 2 + 1;

        // Hann window (FrameSize は定数なので静的化できるが Domain 制約に合わせ毎回作る)
        var window = new double[FrameSize];
        for (int i = 0; i < FrameSize; i++)
            window[i] = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (FrameSize - 1));

        var re      = new double[FrameSize];
        var im      = new double[FrameSize];
        var prevMag = new double[spec];
        var curMag  = new double[spec];
        var flux    = new double[frames];

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

            double f_flux = 0;
            if (f > 0)
            {
                // HFC 重み: 高周波 bin ほど大きく寄与 → トランジェント (キック立ち上がり、
                // スネア、ハイハット) のオンセットを強調し、低周波の持続音を抑える。
                for (int k = 1; k < spec; k++)
                {
                    double d = curMag[k] - prevMag[k];
                    if (d > 0) f_flux += d * k;
                }
            }
            flux[f] = f_flux;

            var swap = prevMag; prevMag = curMag; curMag = swap;
        }
        return flux;
    }

    /// <summary>各点で局所中央値を引いて正の残差にし、最大値 1 に正規化する。</summary>
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

    /// <summary>
    /// オンセット関数の自己相関で BPM を推定。各 lag のスコアに 1/2 倍/2 倍 lag の調和を加算して
    /// オクターブの曖昧性を減らし、ピーク周辺の放物線補間でサブフレーム精度を得る。
    /// </summary>
    static double EstimateBpmAutocorr(double[] norm, int minLag, int maxLag, double frameRate,
                                       double minBpm, double maxBpm)
    {
        int n = norm.Length;
        int range = maxLag - minLag + 1;
        var acf = new double[range];
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double s = 0;
            int end = n - lag;
            for (int f = 0; f < end; f++) s += norm[f] * norm[f + lag];
            acf[lag - minLag] = s;
        }

        // オクターブ調和スコア: lag のスコアに 2*lag (半分の BPM) と lag/2 (倍 BPM) のサポートを加算。
        // これにより周期性が本物の lag が浮上する (倍/半オクターブの偽ピークを抑制)。
        int bestIdx   = 0;
        double bestScore = -1;
        for (int i = 0; i < range; i++)
        {
            int lag = i + minLag;
            double score = acf[i];
            int lag2 = lag * 2;
            if (lag2 - minLag < range) score += 0.5 * acf[lag2 - minLag];
            int lagH = lag / 2;
            if (lagH >= minLag) score += 0.5 * acf[lagH - minLag];
            if (score > bestScore) { bestScore = score; bestIdx = i; }
        }
        int bestLag = bestIdx + minLag;

        // 放物線補間 (ピーク y1 周辺の y0, y2 で頂点をサブフレームに精緻化)
        double frac = 0;
        if (bestIdx > 0 && bestIdx < range - 1)
        {
            double y0 = acf[bestIdx - 1], y1 = acf[bestIdx], y2 = acf[bestIdx + 1];
            double denom = y0 - 2.0 * y1 + y2;
            if (Math.Abs(denom) > 1e-12)
                frac = 0.5 * (y0 - y2) / denom;
            if (frac < -0.5) frac = -0.5; else if (frac > 0.5) frac = 0.5;
        }
        double preciseLag = bestLag + frac;
        if (preciseLag <= 0) return 0;

        double bpm = 60.0 * frameRate / preciseLag;
        while (bpm < minBpm) bpm *= 2.0;
        while (bpm > maxBpm) bpm *= 0.5;
        return Math.Round(bpm * 100.0) / 100.0;
    }

    /// <summary>
    /// BPM 推定後、ベスト位相 (最初のビート時刻) を周期内の整数位相全探索 + 放物線補間で求める。
    /// </summary>
    static double OptimizePhase(double[] norm, double periodFrames)
    {
        int n = norm.Length;
        if (periodFrames < 1.0) return 0;
        int p = (int)Math.Round(periodFrames);
        if (p < 1) p = 1;

        var sums = new double[p];
        double bestSum = -1;
        int bestPhase = 0;
        for (int phase = 0; phase < p; phase++)
        {
            double s = 0;
            for (double t = phase; t < n; t += periodFrames)
            {
                int idx = (int)t;
                if (idx >= 0 && idx < n) s += norm[idx];
            }
            sums[phase] = s;
            if (s > bestSum) { bestSum = s; bestPhase = phase; }
        }

        double frac = 0;
        if (bestPhase > 0 && bestPhase < p - 1)
        {
            double y0 = sums[bestPhase - 1], y1 = sums[bestPhase], y2 = sums[bestPhase + 1];
            double denom = y0 - 2.0 * y1 + y2;
            if (Math.Abs(denom) > 1e-12)
                frac = 0.5 * (y0 - y2) / denom;
            if (frac < -0.5) frac = -0.5; else if (frac > 0.5) frac = 0.5;
        }
        return bestPhase + frac;
    }

    /// <summary>
    /// 段階的ウィンドウ拡張つき反復最小二乗で BPM(=拍周期 period)と位相を精緻化する。
    ///
    /// 動機: 自己相関 BPM は frame 解像度(約 11.6ms)が上限で、わずかな誤差(例 0.3 BPM)でも
    /// 1 拍ごとに蓄積し、3〜4 分の曲後半では格子が音と数百 ms ズレる。これが「後半でずれる」主因。
    ///
    /// 手法: 各オンセットを最寄りのビート格子に割当て(k = round((t-phase)/period))、拍頭近傍
    /// (|残差| ≤ tol) の inlier だけで「t ≈ phase + k·period」を直線回帰する。全曲にわたる長い
    /// 時間基線が傾き(period)を高精度化し、終盤ズレをほぼ解消する。tol = 0.18·period とすることで
    /// 裏拍(0.5·period)や十六分(0.25·period)は自動的に除外される。
    ///
    /// 粗 period に誤差があると曲末で k 割当てがズレるため、まず曲頭の短い窓(k が確実に正しい範囲)で
    /// 精緻化し、窓を倍々に広げながら全曲へ拡張する。各拡張で period 精度が上がるので広い窓でも
    /// k 割当てが崩れない。inlier 不足や暴走時は粗推定のまま据え置く(安全側)。
    /// </summary>
    static void RefineTempoLeastSquares(Result result, double coarseBpm, double coarsePhaseMs,
                                        double minBpm, double maxBpm)
    {
        var onsets = result.OnsetTimesMs;
        if (coarseBpm <= 0 || onsets == null || onsets.Count < 8) return;

        double period = 60_000.0 / coarseBpm;   // ms / beat
        double phase  = coarsePhaseMs;
        double lastT  = onsets[onsets.Count - 1];

        double window = Math.Max(12_000.0, 32.0 * period);   // 初期窓 ≧ 約12秒
        bool full = false;
        for (int pass = 0; pass < 24 && !full; pass++)
        {
            if (window >= lastT) { window = lastT + 1.0; full = true; }

            bool tooFew = false;   // 窓内の inlier 不足。中断せず窓拡張で救済(疎なイントロ対策)。
            for (int iter = 0; iter < 8; iter++)
            {
                double tol = 0.18 * period;   // 拍頭近傍のみ採用(裏拍/連符を除外)
                double sumK = 0, sumK2 = 0, sumT = 0, sumKT = 0;
                int n = 0;
                for (int i = 0; i < onsets.Count; i++)
                {
                    double t = onsets[i];
                    if (t > window) break;               // onsets は時刻昇順
                    double k = Math.Round((t - phase) / period);
                    if (Math.Abs(t - (phase + k * period)) > tol) continue;
                    sumK += k; sumK2 += k * k; sumT += t; sumKT += k * t; n++;
                }
                double denom = n * sumK2 - sumK * sumK;
                if (n < 4 || Math.Abs(denom) < 1e-9) { tooFew = true; break; }  // 回帰不能 → 窓拡張
                double newPeriod = (n * sumKT - sumK * sumT) / denom;
                double newPhase  = (sumT - newPeriod * sumK) / n;
                if (newPeriod <= 0 || Math.Abs(newPeriod - period) > 0.5 * period) return; // 暴走ガード→粗推定据え置き

                bool converged = Math.Abs(newPeriod - period) < 1e-6
                              && Math.Abs(newPhase  - phase)  < 1e-4;
                period = newPeriod;
                phase  = newPhase;
                if (converged) break;
            }
            if (tooFew && full) return;   // 全曲窓でも inlier 不足 → 粗推定据え置き
            window *= 2.0;
        }

        double bpm = 60_000.0 / period;
        while (bpm < minBpm) bpm *= 2.0;
        while (bpm > maxBpm) bpm *= 0.5;
        bpm = Math.Round(bpm * 1000.0) / 1000.0;          // 0.001 BPM 精度(終盤ズレ < 約2ms)

        // FirstOnsetMs は丸め後 BPM の period と整合させ、最初の非負アンカー [0, period) へ正規化
        double p     = 60_000.0 / bpm;
        double first = phase - Math.Floor(phase / p) * p;
        result.EstimatedBpm = bpm;
        result.FirstOnsetMs = first;
    }

    /// <summary>In-place Cooley-Tukey radix-2 forward FFT。入力長は 2 の冪。正規化なし。</summary>
    static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        // bit-reversal permutation
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

    /// <summary>IOI ヒストグラム + オクターブ折り畳みでの簡易 BPM 推定。後方互換のため残置(現在 Detect は使用しない)。</summary>
    public static double EstimateBpmFromIois(List<double> onsetTimesMs, double minBpm, double maxBpm)
    {
        if (onsetTimesMs == null || onsetTimesMs.Count < 4) return 0;
        var bpmVotes = new Dictionary<int, double>();
        for (int i = 1; i < onsetTimesMs.Count; i++)
        {
            double iMs = onsetTimesMs[i] - onsetTimesMs[i - 1];
            if (iMs < 50 || iMs > 2000) continue;
            double bpm = 60000.0 / iMs;
            while (bpm > maxBpm) bpm *= 0.5;
            while (bpm < minBpm) bpm *= 2.0;
            int key = (int)Math.Round(bpm);
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

    /// <summary>マルチチャンネル interleaved PCM をモノラル平均にダウンミックス。</summary>
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
