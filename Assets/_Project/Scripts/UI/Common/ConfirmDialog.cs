using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// 全画面共通の確認ダイアログ。シーン配線不要の自己生成シングルトン（OnGUI 描画）。
    /// ESC で「1画面戻る」する際、離脱が相手に影響する画面（マッチング中・ドラフト中・対戦中）で挟む。
    ///
    /// 操作: ←→ で選択、Space/Enter（またはパッド A）で選択中ボタンを確定、ESC（パッド B）でキャンセル。
    /// 既定フォーカスはキャンセル側（誤離脱防止）。<see cref="IsOpen"/> が真の間、各画面は ESC 処理を止めること。
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        static ConfirmDialog _instance;

        /// <summary>
        /// ダイアログ表示中か。各画面はこの間 ESC 等の入力処理を抑止する。
        /// 閉じたフレームも true を返す（確定キーを裏画面が同フレームで再拾いするのを防ぐ）。
        /// </summary>
        public static bool IsOpen => _instance != null
            && (_instance._open || Time.frameCount == _instance._closeFrame);

        bool   _open;
        int    _openedFrame;
        int    _closeFrame = -1;
        string _message  = "";
        string _confirm  = "OK";
        string _cancel   = "CANCEL";
        int    _selected;            // 0 = cancel(左 / 既定), 1 = confirm(右)
        Action _onConfirm;
        Action _onCancel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ConfirmDialog]");
            _instance = go.AddComponent<ConfirmDialog>();
            DontDestroyOnLoad(go);
        }

        /// <summary>確認ダイアログを表示する。</summary>
        public static void Show(string message, string confirmLabel, string cancelLabel,
                                Action onConfirm, Action onCancel = null)
        {
            Bootstrap();
            _instance.Open(message, confirmLabel, cancelLabel, onConfirm, onCancel);
        }

        /// <summary>遷移時などに開いていれば黙って閉じる（コールバックは呼ばない）。</summary>
        public static void ForceClose()
        {
            if (_instance == null || !_instance._open) return;
            _instance._open       = false;
            _instance._closeFrame = Time.frameCount;
            _instance._onConfirm  = null;
            _instance._onCancel   = null;
        }

        void Open(string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
        {
            _message     = message ?? "";
            _confirm     = string.IsNullOrEmpty(confirmLabel) ? "OK" : confirmLabel;
            _cancel      = string.IsNullOrEmpty(cancelLabel)  ? "CANCEL" : cancelLabel;
            _onConfirm   = onConfirm;
            _onCancel    = onCancel;
            _selected    = 0;                 // 既定はキャンセル側
            _open        = true;
            _openedFrame = Time.frameCount;
        }

        void Update()
        {
            if (!_open) return;
            if (Time.frameCount == _openedFrame) return;   // 開いたフレームの入力は無視（ESC 二重発火回避）

            var kb  = Keyboard.current;
            var pad = Gamepad.current;

            bool left  = (kb != null && (kb.leftArrowKey.wasPressedThisFrame  || kb.aKey.wasPressedThisFrame))
                       || (pad != null && pad.dpad.left.wasPressedThisFrame);
            bool right = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame))
                       || (pad != null && pad.dpad.right.wasPressedThisFrame);
            if (left)  _selected = 0;
            if (right) _selected = 1;

            bool confirmKey = (kb != null && (kb.spaceKey.wasPressedThisFrame
                                           || kb.enterKey.wasPressedThisFrame
                                           || kb.numpadEnterKey.wasPressedThisFrame));
            bool cancelKey  = (kb != null && kb.escapeKey.wasPressedThisFrame);

            // パッド: 既定 Xbox 配置（A=確定 / B=戻る）。Config の任天堂配置切替を反映。
            if (pad != null)
            {
                if (RhythmGame.Input.GamepadLayout.ConfirmPressed(pad)) confirmKey = true;
                if (RhythmGame.Input.GamepadLayout.BackPressed(pad))    cancelKey  = true;
            }

            if (cancelKey)        Close(confirmed: false);
            else if (confirmKey)  Close(confirmed: _selected == 1);
        }

        void Close(bool confirmed)
        {
            _open       = false;
            _closeFrame = Time.frameCount;
            var c  = _onConfirm;
            var x  = _onCancel;
            _onConfirm = null;
            _onCancel  = null;
            if (confirmed) c?.Invoke();
            else           x?.Invoke();
        }

        Texture2D _dimTex;

        Texture2D DimTex()
        {
            if (_dimTex == null)
            {
                _dimTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
                _dimTex.Apply();
            }
            return _dimTex;
        }

        void OnGUI()
        {
            if (!_open) return;

            // 背景の暗幕（クリック透過防止も兼ねる）
            GUI.depth = -1000;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), DimTex());

            const float w = 520f, h = 200f;
            float x = (Screen.width  - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            var box = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize  = 20,
                wordWrap  = true,
                padding   = new RectOffset(20, 20, 24, 20),
            };
            GUI.Box(new Rect(x, y, w, h), _message, box);

            float bw = 200f, bh = 52f, gap = 24f;
            float by = y + h - bh - 24f;
            float cancelX  = x + (w * 0.5f - bw - gap * 0.5f);
            float confirmX = x + (w * 0.5f + gap * 0.5f);

            DrawButton(new Rect(cancelX,  by, bw, bh), _cancel,  _selected == 0, () => Close(false));
            DrawButton(new Rect(confirmX, by, bw, bh), _confirm, _selected == 1, () => Close(true));
        }

        void DrawButton(Rect rect, string label, bool focused, Action onClick)
        {
            var style = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            var prev = GUI.backgroundColor;
            if (focused) GUI.backgroundColor = new Color(0.17f, 0.55f, 0.85f, 1f);
            if (GUI.Button(rect, label)) onClick();
            GUI.backgroundColor = prev;
        }
    }
}
