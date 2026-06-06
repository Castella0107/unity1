using UnityEngine;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// 画面下部に操作（ショートカット）説明を常時表示する共通オーバーレイ。
    /// シーン配線不要の自己生成シングルトン（OnGUI 描画）。各画面コントローラーが Start で
    /// <see cref="Set"/> を呼び、遷移時は SceneRouter が <see cref="Clear"/> する。
    /// </summary>
    public class ShortcutHintOverlay : MonoBehaviour
    {
        static ShortcutHintOverlay _instance;
        string _text = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ShortcutHintOverlay]");
            _instance = go.AddComponent<ShortcutHintOverlay>();
            DontDestroyOnLoad(go);
        }

        /// <summary>表示する操作説明文を設定する（例: "Space: Play   ←→: 難易度   ESC: 戻る"）。</summary>
        public static void Set(string text)
        {
            Bootstrap();
            _instance._text = text ?? "";
        }

        /// <summary>説明を消す。</summary>
        public static void Clear() => Set("");

        Texture2D _bgTex;

        Texture2D BgTex()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _bgTex.Apply();
            }
            return _bgTex;
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(_text)) return;

            const float h = 30f;
            float y = Screen.height - h;

            GUI.DrawTexture(new Rect(0, y, Screen.width, h), BgTex());

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize  = 15,
            };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
            GUI.Label(new Rect(0, y, Screen.width, h), _text, style);
        }
    }
}
