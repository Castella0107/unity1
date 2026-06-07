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
