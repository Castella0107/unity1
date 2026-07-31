using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ───────────────────────────────────────────────────────────────────────────
// テスト用判定トレース計測 (K とのタイミング判定突き合わせ調査, 2026-06-10)
//
// 目的: 実対戦中にクライアントのライブ判定を 1 ノートずつファイルへ書き出し、
//       K が拡張したサーバー側リプレイ再判定ログと delta 単位で突き合わせる。
//       P+ 16ms 境界を含む全判定の一致 / 不一致を特定する。
//
// 入力の丸めはライブ / リプレイで整合 (JudgmentSystem が Round(絶対時刻)、
// ReplayInputBuffer が丸め済み delta を累積) しているため、ここで記録する
// ライブ判定列はサーバーが再判定で見る入力列と同一になる。よって差分が出れば
// それは純粋に判定ロジックの差。
//
// ⚠ 調査完了後はこの計測を削除 / 無効化すること。Enabled=false で全 no-op。
// ───────────────────────────────────────────────────────────────────────────

/// <summary>
/// 判定トレースをファイル (persistentDataPath/judgment_trace/) へ即時フラッシュ出力する静的ロガー。
/// クラッシュしても直前までの行が残るよう AutoFlush で書き込む。
/// </summary>
public static class JudgmentTrace
{
    /// <summary>計測を有効にするか。false なら全メソッドが no-op。
    ///
    /// **既定は無効**。2026-06-10 のタイミング調査用に既定 true のまま残っており、
    /// 入力 1 回ごとに string.Format + lock + AutoFlush のディスク書き込みが走っていた。
    /// 密な譜面を高速に叩くとこれが効いて、実測でフレーム時間が約 2 倍
    /// (1.2ms → 2.3ms)、GC が約 2 倍 (6KB → 12KB/フレーム) に悪化していた
    /// (2026-07-31 スタンドアロンビルドで 25 秒間に 6044 打鍵して計測)。
    ///
    /// 再度調査するときは PlayerPrefs "JudgmentTrace" を 1 にするか、
    /// 起動引数 `--judgment-trace` を付ける。</summary>
    public static bool Enabled = ResolveEnabled();

    static bool ResolveEnabled()
    {
        if (PlayerPrefs.GetInt("JudgmentTrace", 0) == 1) return true;
        foreach (var a in System.Environment.GetCommandLineArgs())
            if (a == "--judgment-trace") return true;
        return false;
    }

    static StreamWriter _writer;
    static int          _seq;
    static readonly object _lock = new object();

    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>現在セッションのトレースファイルの絶対パス (未開始なら null)。</summary>
    public static string CurrentPath { get; private set; }

    /// <summary>
    /// 新しいトレースセッションを開始する。matchId / songOrder / 自分の userId 等を
    /// ヘッダに焼き込み、サーバーログとのキー対応を取れるようにする。
    /// 既存セッションがあれば先に閉じる。
    /// </summary>
    public static void Begin(
        string matchId, int songOrder, string songId, string difficulty,
        string selfUserId, bool selfIsA,
        string chartHash, int appJudgmentOffsetMs, int perSongJudgmentOffsetMs)
    {
        if (!Enabled) return;
        lock (_lock)
        {
            CloseInternal(null);

            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "judgment_trace");
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
                string safeMatch = string.IsNullOrEmpty(matchId) ? "solo" : Sanitize(matchId);
                string file = $"{stamp}_match-{safeMatch}_song{songOrder}.log";
                CurrentPath = Path.Combine(dir, file);

                _writer = new StreamWriter(CurrentPath, append: false, Encoding.UTF8) { AutoFlush = true };
                _seq    = 0;

                _writer.WriteLine("# JudgmentTrace (client live) — K timing investigation 2026-06-10");
                _writer.WriteLine($"# wrotenAt={DateTime.Now.ToString("o", Inv)}");
                _writer.WriteLine($"# matchId={matchId} songOrder={songOrder} songId={songId} difficulty={difficulty}");
                _writer.WriteLine($"# selfUserId={selfUserId} selfIsA={selfIsA}");
                _writer.WriteLine($"# chartHash={chartHash}");
                // K のサーバー WARN ダンプ (app_judgment_offset_ms / per_song_offset_ms) と突き合わせる。
                // このオフセットはクライアントが JudgmentTimeMs に焼き込み済み → リプレイにも入っているので
                // client/server で一致するはず。ズレていたら焼き込み経路を疑う。
                _writer.WriteLine($"# appJudgmentOffsetMs={appJudgmentOffsetMs} perSongJudgmentOffsetMs={perSongJudgmentOffsetMs}");
                _writer.WriteLine($"# windows(ms) P+={JudgmentWindow.ActivePerfectPlusMs} P={JudgmentWindow.ActivePerfectMs} G={JudgmentWindow.ActiveGreatMs} Good={JudgmentWindow.GoodMs} wide={JudgmentWindow.WideActive} (boundary inclusive: abs<=window)");
                _writer.WriteLine("# columns: TAG seq=.. ..");
                _writer.WriteLine("# TAG=INPUT  → one lane down/up event (rawMs = pre-round time, roundedMs = what the engine actually receives)");
                _writer.WriteLine("# TAG=JUDGE  → one resolved judgment (deltaMs/abs only meaningful for hits; auto-miss has delta=0)");
                Debug.Log($"[JudgmentTrace] session started: {CurrentPath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JudgmentTrace] failed to open trace file: {e.Message}");
                _writer     = null;
                CurrentPath = null;
            }
        }
    }

    /// <summary>レーン入力 1 件 (down/up) を記録する。丸め前後の両時刻を残す。</summary>
    public static void LogInput(int lane, bool isDown, double rawMs, double roundedMs)
    {
        if (!Enabled) return;
        lock (_lock)
        {
            if (_writer == null) return;
            _writer.WriteLine(string.Format(Inv,
                "INPUT seq={0} lane={1} down={2} rawMs={3:0.###} roundedMs={4:0}",
                _seq++, lane, isDown, rawMs, roundedMs));
        }
    }

    /// <summary>確定した判定イベント 1 件を記録する。</summary>
    public static void LogJudgment(in JudgmentEvent ev)
    {
        if (!Enabled) return;
        lock (_lock)
        {
            if (_writer == null) return;
            double abs = Math.Abs(ev.DeltaMs);
            // noteMs = 判定対象ノートの時刻。ヒットは inputTime(=TimeMs) − delta で復元できる。
            // PP/P 境界ズレの主容疑は client/server で note.TimeMs の解釈が違うこと。サーバーログの
            // ノート時刻と突き合わせるため明示記録する。オートミス/ティックは delta=0 なので TimeMs がそのまま。
            double noteMs = ev.TimeMs - ev.DeltaMs;
            _writer.WriteLine(string.Format(Inv,
                "JUDGE seq={0} kind={1} note={2} lane={3} noteMs={4:0.###} timeMs={5:0.###} deltaMs={6:0.###} abs={7:0.###} judge={8} combo={9} autoMiss={10}",
                _seq++, ev.Kind, ev.NoteId, (int)ev.Lane, noteMs, ev.TimeMs, ev.DeltaMs, abs, ev.Judgment, ev.Combo, ev.IsAutoMiss));
        }
    }

    /// <summary>セッションを集計サマリ付きで閉じる。複数回呼んでも安全 (2 回目以降は no-op)。</summary>
    public static void End(PlayProgressSnapshot snap)
    {
        if (!Enabled) return;
        lock (_lock) CloseInternal(snap);
    }

    static void CloseInternal(PlayProgressSnapshot snap)
    {
        if (_writer == null) return;
        try
        {
            if (snap != null)
            {
                _writer.WriteLine("# ── summary (client) ──");
                _writer.WriteLine(string.Format(Inv,
                    "SUMMARY score={0} maxCombo={1} P+={2} P={3} G={4} Good={5} Miss={6} fast={7} late={8}",
                    snap.CurrentScore, snap.MaxCombo,
                    snap.PerfectPlusCount, snap.PerfectCount, snap.GreatCount,
                    snap.GoodCount, snap.MissCount, snap.FastCount, snap.LateCount));
            }
            _writer.Flush();
            _writer.Dispose();
            Debug.Log($"[JudgmentTrace] session closed: {CurrentPath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[JudgmentTrace] failed to close trace file: {e.Message}");
        }
        finally
        {
            _writer = null;
        }
    }

    static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return sb.ToString();
    }
}
