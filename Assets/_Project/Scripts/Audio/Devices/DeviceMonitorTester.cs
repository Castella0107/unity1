using UnityEngine;

// Debug overlay for real-device testing of the audio device monitor.
// Attach to any GameObject in a scene (e.g. Title.unity).
// REMOVE before shipping.
/// <summary>
/// オーディオデバイスモニターの実機テスト用デバッグオーバーレイ。
/// RepositoryService のプロファイル変更イベントを購読してログを表示する。出荷前に削除すること。
/// </summary>
public class DeviceMonitorTester : MonoBehaviour
{
    string  _log = "";
    Vector2 _scroll;

    void Start()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged += OnProfileChanged;
    }

    void OnDestroy()
    {
        if (RepositoryService.Instance != null)
            RepositoryService.Instance.OnActiveProfileChanged -= OnProfileChanged;
    }

    void OnProfileChanged(DeviceProfile p)
    {
        _log = string.Format("[{0:HH:mm:ss}] Profile: {1}  OS: {2}\n",
                             System.DateTime.Now, p.DisplayName, p.OsDeviceName) + _log;
    }

    void OnGUI()
    {
        var boxRect = new Rect(10, 10, 520, 210);
        GUI.Box(boxRect, "Audio Device Monitor");

        string current = DeviceProfileService.Instance?.CurrentOsDeviceName ?? "(null)";
        string profile = RepositoryService.Instance?.ActiveProfile?.DisplayName ?? "(null)";
        bool   ready   = RepositoryService.Instance?.IsReady ?? false;

        GUI.Label(new Rect(20, 35, 500, 20), "Repo ready : " + ready);
        GUI.Label(new Rect(20, 55, 500, 20), "OS device  : " + current);
        GUI.Label(new Rect(20, 75, 500, 20), "Profile    : " + profile);
        GUI.Label(new Rect(20, 98, 500, 18), "── Change log ──");

        _scroll = GUI.BeginScrollView(
            new Rect(20, 118, 490, 92), _scroll, new Rect(0, 0, 470, 600));
        GUI.Label(new Rect(0, 0, 470, 600), _log);
        GUI.EndScrollView();
    }
}
