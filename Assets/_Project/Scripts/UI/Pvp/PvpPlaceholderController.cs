using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// 仮実装の PVP 画面 (PVPPrematch / PVPSongPick / PVPBanPhase / PVPResult) に共通で付ける
    /// プレースホルダーコントローラー。画面名の表示と、次画面 / Title への遷移ボタンだけを持つ。
    /// 本実装時はシーンごとの専用コントローラーへ差し替える前提。
    /// </summary>
    public class PvpPlaceholderController : MonoBehaviour
    {
        [SerializeField] string  _screenTitle = "PVP SCREEN (placeholder)";
        [SerializeField] SceneId _nextScene   = SceneId.Title;
        [SerializeField] Button  _nextButton;
        [SerializeField] Button  _backButton;

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_nextButton != null) _nextButton.onClick.AddListener(GoNext);
            if (_backButton != null) _backButton.onClick.AddListener(GoTitle);
        }

        void GoNext()
        {
            if (SceneRouter.Instance != null) SceneRouter.Instance.GoTo(_nextScene);
        }

        void GoTitle()
        {
            if (SceneRouter.Instance != null) SceneRouter.Instance.GoTo(SceneId.Title);
        }

        // ── Fallback OnGUI (正規ボタン未配線でも操作可) ──────────────────────
        void OnGUI()
        {
            if (_nextButton != null || _backButton != null) return;

            const float w = 480f, h = 210f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, _screenTitle);
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 36, r.width - 32, r.height - 48));
            GUILayout.Label("(placeholder screen)");
            GUILayout.Space(12);
            if (GUILayout.Button("NEXT > " + _nextScene)) GoNext();
            if (GUILayout.Button("BACK > Title")) GoTitle();
            GUILayout.EndArea();
        }
    }
}
