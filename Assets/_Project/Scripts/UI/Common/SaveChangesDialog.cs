using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// 未保存変更の確認ダイアログ (キャンセル / 保存しない / 適用 の3択)。
    /// コンフィグ画面で設定変更が保存されないままタブ移動・クローズした際に挟む (ユーザー提供モック準拠)。
    /// シーン配線不要の自己生成シングルトン (OnGUI 描画、ConfirmDialog と同方式)。
    ///
    /// 操作: ←→ で選択、Space/Enter (パッド A) で確定、ESC (パッド B) でキャンセル。既定フォーカスは「保存して退出」。
    /// <see cref="IsOpen"/> が真の間、呼び出し画面は入力処理を抑止すること。
    /// </summary>
    public class SaveChangesDialog : MonoBehaviour
    {
        static SaveChangesDialog _instance;

        /// <summary>表示中か。閉じたフレームも true (確定キーの同フレーム再拾い防止)。</summary>
        public static bool IsOpen => _instance != null
            && (_instance._open || Time.frameCount == _instance._closeFrame);

        bool   _open;
        int    _openedFrame;
        int    _closeFrame = -1;
        string _title    = "";
        string _subText  = "";
        int    _selected = 2;            // 0=キャンセル, 1=保存しない, 2=適用(既定)
        Action _onCancel;
        Action _onDiscard;
        Action _onApply;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[SaveChangesDialog]");
            _instance = go.AddComponent<SaveChangesDialog>();
            DontDestroyOnLoad(go);
        }

        /// <summary>ダイアログを表示する。title=本文、subText=注意書き(赤字相当)。</summary>
        public static void Show(string title, string subText,
                                Action onApply, Action onDiscard, Action onCancel = null)
        {
            Bootstrap();
            _instance._title       = title ?? "";
            _instance._subText     = subText ?? "";
            _instance._onApply     = onApply;
            _instance._onDiscard   = onDiscard;
            _instance._onCancel    = onCancel;
            _instance._selected    = 2;
            _instance._open        = true;
            _instance._openedFrame = Time.frameCount;
        }

        /// <summary>遷移時などに開いていれば黙って閉じる (コールバックは呼ばない)。</summary>
        public static void ForceClose()
        {
            if (_instance == null || !_instance._open) return;
            _instance._open       = false;
            _instance._closeFrame = Time.frameCount;
            _instance._onApply    = null;
            _instance._onDiscard  = null;
            _instance._onCancel   = null;
        }

        void Update()
        {
            if (!_open) return;
            if (Time.frameCount == _openedFrame) return;

            var kb  = Keyboard.current;
            var pad = Gamepad.current;

            bool left  = (kb != null && (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame))
                       || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                       || (pad != null && pad.dpad.right.wasPressedThisFrame);
            if (left)  _selected = (_selected + 2) % 3;
            if (right) _selected = (_selected + 1) % 3;

            bool confirmKey = kb != null && (kb.spaceKey.wasPressedThisFrame
                                          || kb.enterKey.wasPressedThisFrame
                                          || kb.numpadEnterKey.wasPressedThisFrame);
            bool cancelKey  = kb != null && kb.escapeKey.wasPressedThisFrame;

            if (pad != null)
            {
                if (RhythmGame.Input.GamepadLayout.ConfirmPressed(pad)) confirmKey = true;
                if (RhythmGame.Input.GamepadLayout.BackPressed(pad))    cancelKey  = true;
            }

            if (cancelKey)       Close(0);
            else if (confirmKey) Close(_selected);
        }

        void Close(int choice)
        {
            _open       = false;
            _closeFrame = Time.frameCount;
            var cancel  = _onCancel;
            var discard = _onDiscard;
            var apply   = _onApply;
            _onCancel = null; _onDiscard = null; _onApply = null;

            switch (choice)
            {
                case 1:  discard?.Invoke(); break;
                case 2:  apply?.Invoke();   break;
                default: cancel?.Invoke();  break;
            }
        }

        Texture2D _dimTex;

        Texture2D DimTex()
        {
            if (_dimTex == null)
            {
                _dimTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
                _dimTex.Apply();
            }
            return _dimTex;
        }

        void OnGUI()
        {
            if (!_open) return;

            GUI.depth = -1000;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), DimTex());

            const float w = 640f, h = 240f;
            float x = (Screen.width  - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUI.Box(new Rect(x, y, w, h), GUIContent.none);

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true,
            };
            GUI.Label(new Rect(x + 24, y + 28, w - 48, 70), _title, titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 16, wordWrap = true,
            };
            subStyle.normal.textColor = new Color(0.96f, 0.45f, 0.55f);   // 注意書き=赤系
            GUI.Label(new Rect(x + 24, y + 104, w - 48, 30), _subText, subStyle);

            float bw = 180f, bh = 50f, gap = 18f;
            float totalW = bw * 3 + gap * 2;
            float bx = x + (w - totalW) * 0.5f;
            float by = y + h - bh - 26f;

            // 既定フォーカスは「保存して退出」(_selected==2)。文言は保存/破棄を取り違えないよう明示する。
            DrawButton(new Rect(bx,                 by, bw, bh), "キャンセル",      _selected == 0, new Color(.45f,.45f,.5f,1f),  () => Close(0));
            DrawButton(new Rect(bx + bw + gap,      by, bw, bh), "破棄して退出",    _selected == 1, new Color(.85f,.29f,.48f,1f), () => Close(1));
            DrawButton(new Rect(bx + (bw+gap)*2,    by, bw, bh), "保存して退出",    _selected == 2, new Color(.42f,.32f,.88f,1f), () => Close(2));
        }

        void DrawButton(Rect rect, string label, bool focused, Color accent, Action onClick)
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = focused ? accent : new Color(accent.r, accent.g, accent.b, 0.35f);
            if (GUI.Button(rect, label, style)) onClick();
            GUI.backgroundColor = prev;
        }
    }
}
