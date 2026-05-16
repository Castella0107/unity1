using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// リプレイ再生中に再生速度とステータスを表示する最小構成の HUD オーバーレイ。
/// GamePlay シーンの Canvas（Sort Order > GameHud）にアタッチし、リプレイモード以外では自動的に非表示になる。
/// </summary>
// Minimal HUD overlay for replay playback.
// Attach to a Canvas (Sort Order > GameHud) in the GamePlay scene.
// Auto-hides when not in replay mode.
public class ReplayHud : MonoBehaviour
{
    [SerializeField] ReplayPlaybackController _controller;
    [SerializeField] TextMeshProUGUI          _speedText;
    [SerializeField] TextMeshProUGUI          _statusText;

    void Start()
    {
        var prm = ParameterStore.GetPending<GamePlayParameters>()
               ?? ParameterStore.GetCurrent<GamePlayParameters>();
        gameObject.SetActive(prm != null && prm.IsReplay);
    }

    void Update()
    {
        if (_controller == null) return;

        // Keyboard shortcuts: 1 / 2 / 4 for speed
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) _controller.SetPlaybackSpeed(1.0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) _controller.SetPlaybackSpeed(2.0);
            if (Keyboard.current.digit4Key.wasPressedThisFrame) _controller.SetPlaybackSpeed(4.0);
        }

        if (_speedText  != null)
            _speedText.text = string.Format("{0:F1}x", _controller.PlaybackSpeed);
        if (_statusText != null)
            _statusText.text = _controller.IsPlaying ? "▶" : "❚❚";
    }
}
