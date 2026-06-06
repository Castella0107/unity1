using UnityEngine;
using UnityEngine.InputSystem;

namespace RhythmGame.Input
{
    /// <summary>
    /// ゲームパッドの「決定 / 戻る」フェイスボタン配置を扱う共通ヘルパー。
    /// Xbox 配置（A=下=決定 / B=右=戻る）と任天堂配置（右=決定 / 下=戻る）を Config で切替可能にする。
    /// Input System の buttonSouth/buttonEast は物理位置基準（下/右）なので、配置に応じて入れ替える。
    /// </summary>
    public static class GamepadLayout
    {
        /// <summary>0 = Xbox 配置（既定） / 1 = 任天堂配置。</summary>
        public enum Layout { Xbox = 0, Nintendo = 1 }

        const string PrefKey = "GamepadLayout";

        public static Layout Current
        {
            get => (Layout)PlayerPrefs.GetInt(PrefKey, (int)Layout.Xbox);
            set { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>決定ボタン（Space 相当）が押されたか。</summary>
        public static bool ConfirmPressed(Gamepad pad)
        {
            if (pad == null) return false;
            return Current == Layout.Nintendo
                ? pad.buttonEast.wasPressedThisFrame    // 右ボタン
                : pad.buttonSouth.wasPressedThisFrame;  // 下ボタン
        }

        /// <summary>戻る／キャンセルボタン（ESC 相当）が押されたか。</summary>
        public static bool BackPressed(Gamepad pad)
        {
            if (pad == null) return false;
            return Current == Layout.Nintendo
                ? pad.buttonSouth.wasPressedThisFrame   // 下ボタン
                : pad.buttonEast.wasPressedThisFrame;   // 右ボタン
        }

        /// <summary>左バンパー（LB）= タブ・カテゴリを左へ。</summary>
        public static bool PrevTabPressed(Gamepad pad)
            => pad != null && pad.leftShoulder.wasPressedThisFrame;

        /// <summary>右バンパー（RB）= タブ・カテゴリを右へ。</summary>
        public static bool NextTabPressed(Gamepad pad)
            => pad != null && pad.rightShoulder.wasPressedThisFrame;
    }
}
