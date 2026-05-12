// Requires NAudio. Enable by adding NAUDIO to Scripting Define Symbols
// after installing via NuGetForUnity → NAudio 2.x.
#if UNITY_STANDALONE_WIN && NAUDIO
using System;
using System.Threading;
using NAudio.CoreAudioApi;
using UnityEngine;

public class WindowsAudioDeviceMonitor : IAudioDeviceMonitor
{
    public string CurrentDeviceName { get; private set; }
    public bool   IsAvailable       => true;

    public event Action<string> OnDeviceChanged;

    Thread    _pollingThread;
    bool      _running;
    readonly object _lock = new object();
    string    _lastNotifiedName;

    const int PollIntervalMs = 2000;

    public void Start()
    {
        if (_running) return;
        _running = true;

        // Snapshot initial device on calling thread
        try
        {
            CurrentDeviceName = GetDefaultFriendlyName();
            _lastNotifiedName = CurrentDeviceName;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[AudioDeviceMonitor] Initial query failed: " + e.Message);
            CurrentDeviceName = null;
        }

        _pollingThread = new Thread(PollLoop)
        {
            IsBackground = true,
            Name         = "AudioDevicePoll",
        };
        _pollingThread.Start();
        Debug.Log("[AudioDeviceMonitor] Started — current device: " + CurrentDeviceName);
    }

    public void Stop()
    {
        _running = false;
        _pollingThread?.Join(600);   // wait up to 600ms; thread may still be sleeping
        _pollingThread = null;
        Debug.Log("[AudioDeviceMonitor] Stopped");
    }

    void PollLoop()
    {
        while (_running)
        {
            try
            {
                Thread.Sleep(PollIntervalMs);
                if (!_running) return;

                string name = GetDefaultFriendlyName();

                lock (_lock)
                {
                    CurrentDeviceName = name;
                    if (name == _lastNotifiedName) continue;
                    _lastNotifiedName = name;
                }

                // Marshal to main thread; COM objects must not cross thread boundaries
                var dispatcher = MainThreadDispatcher.Instance;
                if (dispatcher != null)
                    dispatcher.Enqueue(() => OnDeviceChanged?.Invoke(name));
                else
                    Debug.LogWarning("[AudioDeviceMonitor] MainThreadDispatcher not available");
            }
            catch (ThreadInterruptedException)  { return; }
            catch (ThreadAbortException)        { return; }
            catch (Exception e)
            {
                // CoreAudio COM API occasionally throws; continue polling
                Debug.LogWarning("[AudioDeviceMonitor] Poll error: " + e.Message);
            }
        }
    }

    static string GetDefaultFriendlyName()
    {
        using (var enumerator = new MMDeviceEnumerator())
        {
            try
            {
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                    return device.FriendlyName;
            }
            catch
            {
                return null;   // no default device
            }
        }
    }
}
#endif
