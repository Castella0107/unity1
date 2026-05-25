using System.Collections.Generic;

namespace Domain.Calibration
{
    /// <summary>
    /// キャリブレーション計測サンプル(タップ時刻 - 期待ビート時刻、単位 ms)から
    /// 推奨判定オフセットを推定する純粋関数群。Unity 非依存。
    /// </summary>
    /// <remarks>
    /// 外れ値除去には Tukey の IQR フェンス(Q1 - 1.5*IQR, Q3 + 1.5*IQR)を使用し、
    /// 残ったサンプルの中央値を推奨値とする。サンプル数不足や全外れの場合は失敗。
    /// </remarks>
    public static class OffsetEstimator
    {
        /// <summary>外れ値除去後に有効と見なす最小サンプル数。</summary>
        public const int MinValidSamples = 8;

        /// <summary>推奨オフセット値の上限・下限(AppOffsetSettings と同じ範囲)。</summary>
        public const int MinOffsetMs = -200;
        public const int MaxOffsetMs =  200;

        /// <summary>オフセット推定の結果。成否・推奨値・採用/棄却サンプル数・標準偏差・失敗理由を保持する。</summary>
        public readonly struct Result
        {
            /// <summary>推定に成功したか。</summary>
            public readonly bool   Success;
            /// <summary>推奨判定オフセット(ms)。</summary>
            public readonly int    RecommendedOffsetMs;
            /// <summary>外れ値除去後に採用されたサンプル数。</summary>
            public readonly int    AcceptedCount;
            /// <summary>外れ値として棄却されたサンプル数。</summary>
            public readonly int    RejectedCount;
            /// <summary>採用サンプルの標準偏差(ms)。</summary>
            public readonly double StdDevMs;
            /// <summary>失敗時の理由(成功時は null)。</summary>
            public readonly string FailureReason;

            /// <summary>全フィールドを指定して結果を生成する。</summary>
            public Result(bool success, int recommended, int accepted, int rejected, double stdev, string reason)
            {
                Success             = success;
                RecommendedOffsetMs = recommended;
                AcceptedCount       = accepted;
                RejectedCount       = rejected;
                StdDevMs            = stdev;
                FailureReason       = reason;
            }

            /// <summary>失敗結果を生成する(理由と元サンプル数を記録)。</summary>
            public static Result Failure(string reason, int rawCount)
                => new Result(false, 0, 0, rawCount, 0.0, reason);
        }

        /// <summary>
        /// サンプル列から推奨オフセットを計算する。
        /// </summary>
        /// <param name="deltasMs">タップ時刻 - 期待ビート時刻 (ms)。正なら遅れ、負なら早押し。</param>
        public static Result Estimate(IReadOnlyList<double> deltasMs)
        {
            if (deltasMs == null || deltasMs.Count == 0)
                return Result.Failure("no samples", 0);

            if (deltasMs.Count < MinValidSamples)
                return Result.Failure("too few samples", deltasMs.Count);

            // Tukey IQR フィルタ
            var sorted = new List<double>(deltasMs);
            sorted.Sort();
            double q1 = Quantile(sorted, 0.25);
            double q3 = Quantile(sorted, 0.75);
            double iqr = q3 - q1;
            double lo  = q1 - 1.5 * iqr;
            double hi  = q3 + 1.5 * iqr;

            var accepted = new List<double>(sorted.Count);
            foreach (var v in sorted)
                if (v >= lo && v <= hi) accepted.Add(v);

            int rejectedCount = deltasMs.Count - accepted.Count;

            if (accepted.Count < MinValidSamples)
                return Result.Failure("too many outliers", deltasMs.Count);

            // 中央値(accepted は既にソート済み)
            double median = Quantile(accepted, 0.5);

            // 標準偏差
            double mean = 0;
            for (int i = 0; i < accepted.Count; i++) mean += accepted[i];
            mean /= accepted.Count;
            double sumSq = 0;
            for (int i = 0; i < accepted.Count; i++)
            {
                double d = accepted[i] - mean;
                sumSq += d * d;
            }
            double stdev = System.Math.Sqrt(sumSq / accepted.Count);

            // タップが遅れた(delta が正)なら判定オフセットは正方向(JudgmentTime をその分早める)
            int recommended = (int)System.Math.Round(median);
            if (recommended < MinOffsetMs) recommended = MinOffsetMs;
            if (recommended > MaxOffsetMs) recommended = MaxOffsetMs;

            return new Result(true, recommended, accepted.Count, rejectedCount, stdev, null);
        }

        // ソート済みリストからの線形補間分位点。
        static double Quantile(List<double> sorted, double p)
        {
            int n = sorted.Count;
            if (n == 1) return sorted[0];
            double pos = (n - 1) * p;
            int lo = (int)System.Math.Floor(pos);
            int hi = (int)System.Math.Ceiling(pos);
            if (lo == hi) return sorted[lo];
            double frac = pos - lo;
            return sorted[lo] * (1.0 - frac) + sorted[hi] * frac;
        }
    }
}
