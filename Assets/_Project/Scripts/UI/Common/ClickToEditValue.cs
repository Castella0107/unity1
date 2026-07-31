using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// 数値ラベルをクリックするとその場でキーボード入力できるようにする (K 指示 2026-07-31)。
    /// ラベルの子として TMP_InputField を動的生成して重ね、Enter/フォーカス喪失で確定、
    /// ESC でキャンセル。半角数字・小数点・マイナスのみ受け付け (全角は半角へ変換、他は拒否)。
    /// 確定値は commit コールバック経由で既存スライダーへ書き戻す (保存やラベル更新は
    /// スライダーの onValueChanged がそのまま担うので、保存経路は従来と同一)。
    /// </summary>
    public class ClickToEditValue : MonoBehaviour, IPointerClickHandler
    {
        // 入力中は ConfigController のキー操作 (ESC/Tab/Q/E/F5/Space/矢印) を止めるための旗。
        // 終了フレームの +1 まで true にして、ESC キャンセル等が同フレームで画面側に流れるのを防ぐ。
        static int _editingUntilFrame = -1;
        public static bool IsEditing => Time.frameCount <= _editingUntilFrame;

        Func<float>   _get;
        Action<float> _commit;
        float _min, _max;
        bool  _integer;

        TextMeshProUGUI _label;
        TMP_InputField  _field;
        GameObject      _editRoot;

        /// <summary>ラベルへ後付けする。get=現在値、commit=確定値の反映 (通常はスライダーへ代入)。</summary>
        public static void Attach(TextMeshProUGUI label, float min, float max, bool integer,
                                  Func<float> get, Action<float> commit)
        {
            if (label == null) return;
            var c = label.gameObject.GetComponent<ClickToEditValue>();
            if (c == null) c = label.gameObject.AddComponent<ClickToEditValue>();
            c._label = label;
            c._min = min; c._max = max; c._integer = integer;
            c._get = get; c._commit = commit;
            label.raycastTarget = true;   // クリックを受けるため
        }

        public void OnPointerClick(PointerEventData eventData) => BeginEdit();

        void BeginEdit()
        {
            if (_editRoot != null || _label == null) return;

            // ラベルの「子」として生成する: 親のレイアウトグループに影響を与えず、
            // ラベルの表示矩形にぴったり重なる。
            _editRoot = new GameObject("InlineValueEdit", typeof(RectTransform));
            var rt = (RectTransform)_editRoot.transform;
            rt.SetParent(_label.rectTransform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-4f, -2f);
            rt.offsetMax = new Vector2(4f, 2f);

            var bg = _editRoot.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.12f, 0.92f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)textGO.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 1f);
            trt.offsetMax = new Vector2(-6f, -1f);
            var txt = textGO.AddComponent<TextMeshProUGUI>();
            txt.font              = _label.font;
            txt.fontSize          = _label.fontSize;
            txt.color             = Color.white;
            txt.alignment         = _label.alignment;
            txt.enableWordWrapping = false;

            _field = _editRoot.AddComponent<TMP_InputField>();
            _field.targetGraphic   = bg;
            _field.textComponent   = txt;
            _field.lineType        = TMP_InputField.LineType.SingleLine;
            _field.characterLimit  = 8;
            _field.onValidateInput = ValidateChar;

            float cur = 0f;
            try { cur = _get != null ? _get() : 0f; } catch { }
            _field.text = _integer
                ? Mathf.RoundToInt(cur).ToString(CultureInfo.InvariantCulture)
                : cur.ToString("0.###", CultureInfo.InvariantCulture);

            _field.onEndEdit.AddListener(OnEndEdit);

            _label.enabled = false;             // ラベル本文は隠す (子は表示されたまま)
            _editingUntilFrame = int.MaxValue;  // 入力中は画面側のキー操作を停止

            _field.Select();
            _field.ActivateInputField();
        }

        // 半角数字/小数点/マイナスのみ許可。全角数字などは半角へ変換して受け入れ、他は破棄。
        static char ValidateChar(string text, int charIndex, char addedChar)
        {
            char ch = addedChar;
            if (ch >= '０' && ch <= '９') ch = (char)('0' + (ch - '０'));   // 全角数字 → 半角
            else if (ch == '．')          ch = '.';                          // 全角ピリオド
            else if (ch == '－' || ch == '−' || ch == 'ー') ch = '-';       // 全角/長音マイナス

            bool ok = (ch >= '0' && ch <= '9') || ch == '.' || ch == '-';
            return ok ? ch : '\0';
        }

        void OnEndEdit(string raw)
        {
            bool canceled = _field != null && _field.wasCanceled;   // ESC
            if (!canceled &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) &&
                !float.IsNaN(v) && !float.IsInfinity(v))
            {
                v = Mathf.Clamp(v, _min, _max);
                if (_integer) v = Mathf.Round(v);
                try { _commit?.Invoke(v); }
                catch (Exception ex) { Debug.LogWarning("[ClickToEdit] 反映失敗: " + ex.Message); }
            }
            // パース不能 (空文字等) は変更なしで閉じる

            if (_label != null) _label.enabled = true;
            if (_editRoot != null) Destroy(_editRoot);
            _editRoot = null;
            _field = null;
            _editingUntilFrame = Time.frameCount + 1;   // 同フレームのキーを画面側に流さない
        }

        void OnDisable()
        {
            // タブ切替等で編集中のまま非表示になった場合の後始末
            if (_editRoot != null)
            {
                if (_label != null) _label.enabled = true;
                Destroy(_editRoot);
                _editRoot = null;
                _field = null;
                _editingUntilFrame = Time.frameCount + 1;
            }
        }
    }
}
