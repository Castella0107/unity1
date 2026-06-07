using UnityEngine;

/// <summary>
/// ウィンドウ切替ミュート (コンフィグ「オーディオ」タブの項目、モック適用で新設)。
/// 有効時、アプリがフォーカスを失うと AudioListener.volume を 0 にし、復帰で戻す。
/// AudioListener.pause は使わない(dspTime 同期や進行中のサウンドを止めないため)。
/// シーン配線不要の自己ブートストラップ。
/// </summary>
public static class MuteOnFocusLoss
{
    const string PrefKey = "MuteOnFocusLoss";

    /// <summary>ウィンドウ切替ミュートの有効/無効 (PlayerPrefs 永続化、既定=無効)。</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            // 無効化したら即座にミュート解除(フォーカス喪失中に切り替えた場合の保険)
            if (!value) AudioListener.volume = 1f;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Application.focusChanged += OnFocusChanged;
    }

    static void OnFocusChanged(bool hasFocus)
    {
        if (!Enabled) return;
        AudioListener.volume = hasFocus ? 1f : 0f;
    }
}
