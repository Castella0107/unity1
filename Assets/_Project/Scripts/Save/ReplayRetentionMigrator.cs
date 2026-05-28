using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 旧仕様(全プレイのリプレイを永久保存)で溜まったソロのリプレイファイルを、
/// 「各楽曲×難易度の最高スコアのリプレイだけ残す」新方針に合わせて一度だけ刈り込む。
/// DB 初期化後の初回起動時に1回のみ実行される。
/// PVP プレイ(IsPvP)のリプレイは PVP 履歴側が管理するため対象外。
/// </summary>
public static class ReplayRetentionMigrator
{
    const string FlagKey = "ReplayRetentionMigration_v1";

    /// <summary>未実行ならソロの非ベストリプレイを削除し、その行の ReplayPath を null 化する(初回のみ)。</summary>
    public static async Task MigrateIfNeeded(IPlayRecordRepository repo, ReplayStorage replays)
    {
        if (repo == null || replays == null) return;
        if (PlayerPrefs.GetInt(FlagKey, 0) == 1) return;

        try
        {
            // 残す対象 = 各 (曲×難易度) のベストプレイ
            var keep  = new HashSet<string>();
            var bests = await repo.GetAllBestsAsync();
            foreach (var b in bests)
                if (!string.IsNullOrEmpty(b.BestPlayId)) keep.Add(b.BestPlayId);

            var all    = await repo.GetAllHistoryAsync(limit: 1_000_000, offset: 0);
            int pruned = 0;
            foreach (var rec in all)
            {
                if (rec.IsPvP) continue;                     // PVP は別管理
                if (keep.Contains(rec.PlayId)) continue;     // ベストは残す
                if (string.IsNullOrEmpty(rec.ReplayPath)) continue;

                replays.Delete(rec.ReplayPath);
                await repo.ClearReplayPathAsync(rec.PlayId);
                pruned++;
            }

            Debug.Log($"[ReplayRetention] Pruned {pruned} non-best solo replays "
                      + $"(kept {keep.Count} bests)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ReplayRetention] Migration failed: " + e.Message);
        }
        finally
        {
            PlayerPrefs.SetInt(FlagKey, 1);
            PlayerPrefs.Save();
        }
    }
}
