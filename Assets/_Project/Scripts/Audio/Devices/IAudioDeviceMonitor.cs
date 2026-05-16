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
    /// OS-side friendly name of the current default output device (null if unavailable).
    string CurrentDeviceName { get; }

    /// True only on platforms where monitoring is actually implemented.
    bool IsAvailable { get; }

    void Start();
    void Stop();

    /// Fired on the main thread whenever the default output device changes.
    event Action<string> OnDeviceChanged;
}
