using System;
using System.Collections.Generic;

// Unity-independent. No UnityEngine references allowed in this assembly.
namespace Domain.Pvp
{
    /// <summary>
    /// Glicko-2 レーティング計算 (Mark E. Glickman, "Example of the Glicko-2 system", 2012)。
    /// クライアント・サーバー間で bit-perfect 同期する必要があるため、
    /// すべて Pure C# で double 演算を行う (浮動小数点丸めは IEEE 754 に従う)。
    ///
    /// 使い方:
    ///   var p   = Glicko2Player.CreateDefault();
    ///   var rs  = new[] { new Glicko2Result(1400, 30, 1.0), ... };
    ///   var p2  = Glicko2Calculator.Update(p, rs);
    ///
    /// 試合がないレーティング期間 (休眠) の場合は <see cref="Decay"/> を呼ぶ。
    /// </summary>
    public static class Glicko2Calculator
    {
        /// <summary>システム定数 τ。レーティング変動の制約 (典型 0.3〜1.2、Glickman 推奨 0.5)。</summary>
        public const double Tau = 0.5;

        /// <summary>表示レーティング (Elo 系) から内部スケール (μ, φ) への変換係数 = 173.7178。</summary>
        public const double Scale = 173.7178;

        /// <summary>収束判定閾値。論文の例と一致させるため小さめに設定。</summary>
        public const double ConvergenceEpsilon = 1e-6;

        /// <summary>
        /// 1 レーティング期間分の試合結果を適用して新しい <see cref="Glicko2Player"/> を返す。
        /// results が空の場合はレーティングは変化せず RD のみ拡張される (Decay 相当)。
        /// </summary>
        public static Glicko2Player Update(Glicko2Player player, IReadOnlyList<Glicko2Result> results)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (results == null || results.Count == 0)
                return Decay(player);

            // Step 2: 内部スケールへ変換
            double mu  = (player.Rating - 1500.0) / Scale;
            double phi = player.RatingDeviation / Scale;
            double sig = player.Volatility;

            // Step 3: variance v と improvement delta
            double vInv = 0.0;
            double delta = 0.0;
            foreach (var r in results)
            {
                double muJ  = (r.OpponentRating - 1500.0) / Scale;
                double phiJ = r.OpponentRatingDeviation / Scale;
                double g    = G(phiJ);
                double e    = E(mu, muJ, g);
                vInv += g * g * e * (1.0 - e);
                delta += g * (r.Score - e);
            }
            double v = 1.0 / vInv;
            delta *= v;

            // Step 5: 新しい σ' を反復法 (Glickman 論文の Section 5.4)
            double sigPrime = ComputeNewVolatility(sig, phi, v, delta);

            // Step 6: 前段の RD を時間方向に拡張 → 新 φ'
            double phiStar = Math.Sqrt(phi * phi + sigPrime * sigPrime);

            // Step 7: 新 φ', μ'
            double phiPrime = 1.0 / Math.Sqrt(1.0 / (phiStar * phiStar) + vInv);
            double muPrime  = mu + phiPrime * phiPrime * (delta / v);

            // Step 8: 表示スケールへ戻す
            double newRating = muPrime  * Scale + 1500.0;
            double newRD     = phiPrime * Scale;

            return new Glicko2Player(newRating, newRD, sigPrime);
        }

        /// <summary>
        /// 試合がないレーティング期間の処理。レーティングは変わらず RD のみ広がる。
        /// φ_new = sqrt(φ² + σ²) を 1 期分。
        /// </summary>
        public static Glicko2Player Decay(Glicko2Player player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            double phi = player.RatingDeviation / Scale;
            double sig = player.Volatility;
            double phiNew = Math.Sqrt(phi * phi + sig * sig);
            return new Glicko2Player(player.Rating, phiNew * Scale, sig);
        }

        /// <summary>
        /// シーズン跨ぎ減衰 (設計書): new_R = 1500 + (old_R - 1500) × decay。
        /// RD は <paramref name="floorRd"/> 以下のときに新規プレイヤー寄りに広げる (デフォルト 100)。
        /// </summary>
        public static Glicko2Player SeasonDecay(Glicko2Player player, double decay = 0.5, double floorRd = 100.0)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            double newR  = 1500.0 + (player.Rating - 1500.0) * decay;
            double newRd = Math.Max(player.RatingDeviation, floorRd);
            return new Glicko2Player(newR, newRd, player.Volatility);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        // g(φ) = 1 / sqrt(1 + 3φ² / π²)
        static double G(double phi)
        {
            return 1.0 / Math.Sqrt(1.0 + 3.0 * phi * phi / (Math.PI * Math.PI));
        }

        // E(μ, μ_j, g_j) = 1 / (1 + exp(-g_j (μ - μ_j)))
        static double E(double mu, double muJ, double gJ)
        {
            return 1.0 / (1.0 + Math.Exp(-gJ * (mu - muJ)));
        }

        // Glickman 論文 Section 5.4 — Illinois Algorithm (modified regula falsi)
        static double ComputeNewVolatility(double sigma, double phi, double v, double delta)
        {
            double a = Math.Log(sigma * sigma);
            double tau2 = Tau * Tau;
            double phi2 = phi * phi;
            double delta2 = delta * delta;

            Func<double, double> f = x =>
            {
                double ex = Math.Exp(x);
                double num = ex * (delta2 - phi2 - v - ex);
                double den = 2.0 * (phi2 + v + ex) * (phi2 + v + ex);
                return num / den - (x - a) / tau2;
            };

            double A = a;
            double B;
            if (delta2 > phi2 + v)
            {
                B = Math.Log(delta2 - phi2 - v);
            }
            else
            {
                int k = 1;
                while (f(a - k * Tau) < 0)
                    k++;
                B = a - k * Tau;
            }

            double fA = f(A);
            double fB = f(B);

            int safetyCounter = 0;
            while (Math.Abs(B - A) > ConvergenceEpsilon)
            {
                if (safetyCounter++ > 100) break; // 念のため発散ガード
                double C  = A + (A - B) * fA / (fB - fA);
                double fC = f(C);
                if (fC * fB <= 0) { A = B; fA = fB; }
                else              { fA /= 2.0; }
                B  = C;
                fB = fC;
            }

            return Math.Exp(A / 2.0);
        }
    }
}
