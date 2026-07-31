using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// スライダー横の ◁ / ▷ ステップボタン (コンフィグのモック準拠 UI 用)。
    /// クリックで対象スライダーの値を _delta だけ増減する。シーンビルダーが baked-in 配線する。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SliderStepper : MonoBehaviour
    {
        [SerializeField] Slider _slider;
        [SerializeField] float  _delta = 1f;

        /// <summary>刻み幅の絶対値を実行時に変更する (方向 = 焼き込み時の符号を維持)。</summary>
        public void SetDeltaMagnitude(float magnitude) => _delta = Mathf.Sign(_delta) * magnitude;

        void Start()
        {
            var btn = GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (_slider == null) return;
                _slider.value = Mathf.Clamp(_slider.value + _delta, _slider.minValue, _slider.maxValue);
            });
        }
    }
}
