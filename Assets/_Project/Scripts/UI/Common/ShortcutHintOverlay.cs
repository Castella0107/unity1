using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// 画面下部に操作（ショートカット）説明を常時表示する共通オーバーレイ。
    /// シーン配線不要の自己生成シングルトン。各画面コントローラーが Start で
    /// <see cref="Set"/> を呼び、遷移時は SceneRouter が <see cref="Clear"/> する。
    ///
    /// 実装は uGUI（Canvas + Image + TMP）。文字列が変わったときだけ更新するので毎フレームの
    /// コストはゼロ。以前は OnGUI 描画で、表示中は毎フレーム GUIStyle を新規生成しており
    /// GC を踏み続けていた（2026-07-31 実測: GamePlay 中に OnGUI 2.8ms、
    /// URP の描画本体 1.0ms より重かった）。ゲームプレイ中も出しっぱなしの表示なので影響が大きい。
    /// </summary>
    public class ShortcutHintOverlay : MonoBehaviour
    {
        static ShortcutHintOverlay _instance;

        Canvas          _canvas;
        TextMeshProUGUI _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ShortcutHintOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShortcutHintOverlay>();   // Awake で UI を組む
        }

        /// <summary>表示する操作説明文を設定する（例: "Space: Play   ←→: 難易度   ESC: 戻る"）。</summary>
        public static void Set(string text)
        {
            Bootstrap();
            if (_instance != null) _instance.Apply(text ?? "");
        }

        /// <summary>説明を消す。</summary>
        public static void Clear() => Set("");

        void Awake()
        {
            if (_canvas == null) BuildUi();
        }

        void Apply(string text)
        {
            if (_canvas == null) BuildUi();
            if (_label != null) _label.text = text;
            // 空文字なら丸ごと非アクティブ（Canvas のリビルドも走らない）
            _canvas.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        void BuildUi()
        {
            var canvasGO = new GameObject("Canvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;   // 他の UI より前面

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 1f;   // 高さ基準（帯の厚みを一定に）
            // GraphicRaycaster は付けない — 入力を奪わせない

            var bgGO = new GameObject("Bg", typeof(RectTransform));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin        = new Vector2(0f, 0f);
            bgRt.anchorMax        = new Vector2(1f, 0f);
            bgRt.pivot            = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta        = new Vector2(0f, 45f);
            var bg = bgGO.AddComponent<Image>();
            bg.color         = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(bgGO.transform, false);
            var txtRt = txtGO.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            _label = txtGO.AddComponent<TextMeshProUGUI>();
            _label.alignment     = TextAlignmentOptions.Center;
            _label.fontSize      = 22f;
            _label.color         = new Color(1f, 1f, 1f, 0.92f);
            _label.raycastTarget = false;

            canvasGO.SetActive(false);
        }
    }
}
