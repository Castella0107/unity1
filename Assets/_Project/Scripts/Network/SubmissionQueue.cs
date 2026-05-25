using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace RhythmGame.Network
{
    /// <summary>
    /// 送信失敗・サーバー未起動・タイムアウト等で投函できなかった PlayRecord を
    /// `Application.persistentDataPath/submission_queue/` に JSON サイドカーとしてキュー化する。
    /// 各エントリは {playId}.json (メタ) + 既存の Replays/ にある .replay ファイル参照。
    ///
    /// flush 戦略:
    ///   - NetworkClient bootstrap 完了直後 (1回)
    ///   - Ping 成功時 (= サーバー復活検知)
    ///   - 手動 (DebugNetworkOverlay の "Flush Queue" ボタン)
    /// </summary>
    public static class SubmissionQueue
    {
        const string DirName = "submission_queue";

        /// <summary>送信待ちの1件。譜面ハッシュ・リプレイパス・主張結果・メタ・投入時刻・試行回数を保持する。</summary>
        [Serializable]
        public class QueuedEntry
        {
            /// <summary>譜面ハッシュ。</summary>
            public string         ChartHash;
            /// <summary>リプレイファイルへのパス(再読込時に存在しなければスキップ)。</summary>
            public string         ReplayPath;
            /// <summary>クライアントが主張するプレイ結果。</summary>
            public ResultClaimDto Claim;
            /// <summary>検証/永続化用メタデータ。</summary>
            public ValidateRequestDto Meta;
            /// <summary>キュー投入時刻(Unix ms)。</summary>
            public long           EnqueuedAtUnixMs;
            /// <summary>送信試行回数。</summary>
            public int            Attempts;
        }

        static string Root => Path.Combine(Application.persistentDataPath, DirName);

        static void EnsureDir()
        {
            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
        }

        /// <summary>キューに積む。Replay ファイルへの参照はパス保持のみ (再 read 時に存在しないとスキップ)。</summary>
        public static void Enqueue(QueuedEntry entry)
        {
            try
            {
                EnsureDir();
                if (entry.Meta == null) entry.Meta = new ValidateRequestDto();
                if (string.IsNullOrEmpty(entry.Meta.playId))
                    entry.Meta.playId = Guid.NewGuid().ToString();
                if (entry.EnqueuedAtUnixMs == 0)
                    entry.EnqueuedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                string path = Path.Combine(Root, entry.Meta.playId + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(entry));
                Debug.Log($"[SubmissionQueue] Enqueued {entry.Meta.playId} (song={entry.Meta.songId}) → {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SubmissionQueue] Enqueue failed: " + e.Message);
            }
        }

        /// <summary>キューに残っている件数を返す。</summary>
        public static int Count()
        {
            try
            {
                if (!Directory.Exists(Root)) return 0;
                return Directory.GetFiles(Root, "*.json").Length;
            }
            catch { return 0; }
        }

        /// <summary>
        /// キュー全件を順に送信。VALID/INVALID どちらでも検証完了したら削除 (再送しない)。
        /// transport 失敗のみキューに残す。
        /// </summary>
        public static async Task<int> FlushAsync()
        {
            if (NetworkClient.Instance == null) return 0;
            if (!ServerConfig.Enabled) return 0;
            if (!Directory.Exists(Root)) return 0;

            int submitted = 0;
            var files = Directory.GetFiles(Root, "*.json");
            foreach (var file in files)
            {
                QueuedEntry entry;
                try { entry = JsonConvert.DeserializeObject<QueuedEntry>(File.ReadAllText(file)); }
                catch (Exception e)
                {
                    Debug.LogWarning("[SubmissionQueue] Skip corrupt entry " + file + ": " + e.Message);
                    try { File.Delete(file); } catch { }
                    continue;
                }
                if (entry == null) { try { File.Delete(file); } catch { } continue; }

                if (string.IsNullOrEmpty(entry.ReplayPath) || !File.Exists(entry.ReplayPath))
                {
                    Debug.LogWarning("[SubmissionQueue] Replay missing for " + entry.Meta?.playId + " — dropping");
                    try { File.Delete(file); } catch { }
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(entry.ReplayPath);
                entry.Attempts += 1;

                var r = await NetworkClient.Instance.ValidateReplayAsync(entry.ChartHash, bytes, entry.Claim, entry.Meta);
                if (!r.Ok)
                {
                    // transport 失敗 → 試行回数だけ更新して残す
                    try { File.WriteAllText(file, JsonConvert.SerializeObject(entry)); } catch { }
                    Debug.LogWarning($"[SubmissionQueue] Transport fail for {entry.Meta?.playId} (attempts={entry.Attempts}) — leaving in queue");
                    // サーバー応答なしと推定して残りもスキップ
                    break;
                }

                // サーバーが応答した = VALID も INVALID も検証完了
                Debug.Log($"[SubmissionQueue] Submitted {entry.Meta?.playId} → isValid={r.Body?.isValid}");
                try { File.Delete(file); } catch { }
                submitted++;
            }
            return submitted;
        }
    }
}
