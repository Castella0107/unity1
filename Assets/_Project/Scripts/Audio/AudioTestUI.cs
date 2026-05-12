using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Test harness for AudioConductor.
//
// HOW TO TEST
//   1. Open Assets/_Project/Scenes/AudioTest.unity and enter Play Mode.
//   2. (Optional) Place audio.ogg at StreamingAssets/Songs/test/audio.ogg.
//      The Status line confirms whether the file was found.
//   3. Click [Start]  — SongTimeMs counts from ≈ -1000 ms (preroll) then crosses 0.
//   4. Click [Pause]  — all three time values freeze at the current position.
//   5. Click [Resume] — values resume from the frozen position after 0.5 s preroll.
//   6. Click [Stop]   — resets to 0.
//
// Correctness check:
//   Switch the Editor between 60 fps (VSyncCount=1) and 240 fps (VSyncCount=0).
//   SongTimeMs should advance at the same wall-clock rate in both cases —
//   proving the dspTime-based formula has no frame-rate-dependent drift.

public sealed class AudioTestUI : MonoBehaviour
{
    [Header("Time Display")]
    [SerializeField] private TextMeshProUGUI _songTimeText;
    [SerializeField] private TextMeshProUGUI _judgmentTimeText;
    [SerializeField] private TextMeshProUGUI _visualTimeText;
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Controls")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _pauseButton;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _stopButton;

    [Header("Fallback (drag any AudioClip here for offline testing)")]
    [SerializeField] private AudioClip _fallbackClip;

    private AudioConductor _conductor;
    private AudioClip      _loadedClip;

    private void Start()
    {
        _conductor = AudioConductor.Instance;
        if (_conductor == null)
        {
            Debug.LogError("[AudioTestUI] AudioConductor.Instance is null. " +
                           "Make sure AudioConductor is in the scene.");
            return;
        }

        _startButton .onClick.AddListener(OnStart);
        _pauseButton .onClick.AddListener(OnPause);
        _resumeButton.onClick.AddListener(OnResume);
        _stopButton  .onClick.AddListener(OnStop);

        StartCoroutine(LoadClipFromStreamingAssets());
    }

    // Try to load StreamingAssets/Songs/test/audio.ogg; fall back to _fallbackClip.
    private IEnumerator LoadClipFromStreamingAssets()
    {
        SetStatus("Loading clip…");
        string path = System.IO.Path.Combine(
            Application.streamingAssetsPath, "Songs/test/audio.ogg")
            .Replace("\\", "/");
        string url = "file://" + path;

        using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.OGGVORBIS);
        ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio = false;
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            _loadedClip = DownloadHandlerAudioClip.GetContent(req);
            SetStatus("Clip loaded: StreamingAssets/Songs/test/audio.ogg");
        }
        else
        {
            _loadedClip = _fallbackClip;
            SetStatus(_fallbackClip != null
                ? "StreamingAssets not found — using fallback clip"
                : "No clip available. Place audio.ogg in StreamingAssets/Songs/test/");
        }
    }

    private void Update()
    {
        if (_conductor == null) return;
        _songTimeText    .text = $"SongTime      {_conductor.SongTimeMs,12:F3} ms";
        _judgmentTimeText.text = $"JudgmentTime  {_conductor.JudgmentTimeMs,12:F3} ms";
        _visualTimeText  .text = $"VisualTime    {_conductor.VisualTimeMs,12:F3} ms";
    }

    private void OnStart()  => _conductor.StartSong(_loadedClip, prerollSec: 1.0);
    private void OnPause()  => _conductor.Pause();
    private void OnResume() => _conductor.Resume(prerollSec: 0.5);
    private void OnStop()   => _conductor.Stop();

    private void SetStatus(string msg)
    {
        if (_statusText != null) _statusText.text = msg;
        Debug.Log($"[AudioTestUI] {msg}");
    }
}
