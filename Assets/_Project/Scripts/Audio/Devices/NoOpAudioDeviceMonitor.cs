using System;

// Stub used on non-Windows platforms or when NAudio is not installed.
/// <summary>
/// 非 Windows プラットフォームまたは NAudio 未インストール時に使用される IAudioDeviceMonitor のスタブ実装。
/// デバイス監視機能を持たず、IsAvailable は常に false を返す。
/// </summary>
public class NoOpAudioDeviceMonitor : IAudioDeviceMonitor
{
    /// <inheritdoc/>
    public string CurrentDeviceName => null;
    /// <inheritdoc/>
    public bool   IsAvailable       => false;

#pragma warning disable CS0067   // event is never used — intentional for the no-op
    /// <inheritdoc/>
    public event Action<string> OnDeviceChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public void Start() { }
    /// <inheritdoc/>
    public void Stop()  { }
}
