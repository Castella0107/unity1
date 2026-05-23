using System;
using System.Collections.Generic;

/// <summary>
/// 起動時コマンドライン引数を <c>--key value</c> 形式でパースし、辞書として提供するユーティリティ。
/// 譜面エディタ (TestPlayLauncher) からの <c>--chart &lt;path&gt; --difficulty &lt;diff&gt;</c> 連携で使用する。
/// </summary>
public static class CommandLineArgs
{
    static Dictionary<string, string> _cache;

    /// <summary>外部から argv を差し替えてパースするテスト用フック。null クリア で次回 Environment 再読込。</summary>
    public static void OverrideForTests(string[] args)
    {
        _cache = args == null ? null : Parse(args);
    }

    /// <summary>--key value 形式の引数を取得。未指定なら null。</summary>
    public static string Get(string key)
    {
        EnsureParsed();
        return _cache.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>--key 形式の単独フラグが含まれているか。</summary>
    public static bool HasFlag(string key)
    {
        EnsureParsed();
        return _cache.ContainsKey(key);
    }

    static void EnsureParsed()
    {
        if (_cache != null) return;
        _cache = Parse(Environment.GetCommandLineArgs());
    }

    static Dictionary<string, string> Parse(string[] argv)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (argv == null) return dict;

        // argv[0] は実行ファイルパス。1 から走査。
        for (int i = 1; i < argv.Length; i++)
        {
            string a = argv[i];
            if (string.IsNullOrEmpty(a) || !a.StartsWith("--")) continue;
            string key = a.Substring(2);
            string val = "";
            if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--"))
            {
                val = argv[i + 1];
                i++;
            }
            dict[key] = val;
        }
        return dict;
    }
}
