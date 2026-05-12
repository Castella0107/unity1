using System;

// Stub used on non-Windows platforms or when NAudio is not installed.
public class NoOpAudioDeviceMonitor : IAudioDeviceMonitor
{
    public string CurrentDeviceName => null;
    public bool   IsAvailable       => false;

#pragma warning disable CS0067   // event is never used — intentional for the no-op
    public event Action<string> OnDeviceChanged;
#pragma warning restore CS0067

    public void Start() { }
    public void Stop()  { }
}
