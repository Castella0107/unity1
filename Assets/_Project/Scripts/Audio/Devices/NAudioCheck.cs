// Compile-time verification that NAudio is installed and accessible.
// This file must compile without errors when NAUDIO is defined.
// It is not called at runtime.
#if UNITY_STANDALONE_WIN && NAUDIO
using NAudio.CoreAudioApi;

/// <summary>
/// NAudio がインストール済みでアクセス可能であることをコンパイル時に検証するユーティリティクラス。
/// ランタイムでは呼び出されず、NAUDIO シンボル定義時のビルド確認専用。
/// </summary>
public static class NAudioCheck
{
    /// <summary>既定オーディオ出力デバイスのフレンドリ名と ID を返す(NAudio 動作確認用)。</summary>
    public static string GetCurrentDeviceInfo()
    {
        using (var enumerator = new MMDeviceEnumerator())
        using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            return string.Format("{0}  ID={1}", device.FriendlyName, device.ID);
    }
}
#endif
