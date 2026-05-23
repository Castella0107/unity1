using UnityEngine;

/// <summary>
/// サーバー接続設定。PlayerPrefs に永続化される軽量設定オブジェクト。
/// Config 画面の Account タブから編集可能にする想定。
/// </summary>
public static class ServerConfig
{
    const string PrefKey_BaseUrl       = "Network_BaseUrl";
    const string PrefKey_TimeoutSec    = "Network_TimeoutSec";
    const string PrefKey_Enabled       = "Network_Enabled";

    public const string DefaultBaseUrl    = "http://localhost:5246";
    public const int    DefaultTimeoutSec = 10;
    public const bool   DefaultEnabled    = true;

    public static string BaseUrl
    {
        get => PlayerPrefs.GetString(PrefKey_BaseUrl, DefaultBaseUrl);
        set
        {
            PlayerPrefs.SetString(PrefKey_BaseUrl, value ?? DefaultBaseUrl);
            PlayerPrefs.Save();
        }
    }

    public static int TimeoutSeconds
    {
        get => PlayerPrefs.GetInt(PrefKey_TimeoutSec, DefaultTimeoutSec);
        set
        {
            PlayerPrefs.SetInt(PrefKey_TimeoutSec, Mathf.Clamp(value, 1, 120));
            PlayerPrefs.Save();
        }
    }

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey_Enabled, DefaultEnabled ? 1 : 0) != 0;
        set
        {
            PlayerPrefs.SetInt(PrefKey_Enabled, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
