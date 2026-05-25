using System;

// Platform-agnostic interface for default audio output device monitoring.
// Windows: WindowsAudioDeviceMonitor (NAudio polling)
// Other:   NoOpAudioDeviceMonitor (no-op stub)
/// <summary>
/// デフォルトオーディオ出力デバイスの監視を抽象化するプラットフォーム非依存インターフェース。
/// Windows 実装は WindowsAudioDeviceMonitor、その他は NoOpAudioDeviceMonitor が使用される。
/// </summary>
public interface IAudioDeviceMonitor
{
    /// <summary>現在の既定出力デバイスの OS 表示名(取得不可なら null)。</summary>
    string CurrentDeviceName { get; }

    /// <summary>このプラットフォームで監視が実装されている場合のみ true。</summary>
    bool IsAvailable { get; }

    /// <summary>デバイス監視を開始する。</summary>
    void Start();
    /// <summary>デバイス監視を停止する。</summary>
    void Stop();

    /// <summary>既定出力デバイスが変化したときメインスレッドで発火する。</summary>
    event Action<string> OnDeviceChanged;
}
