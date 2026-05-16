using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 判定種別に応じたテキストをポップアップ表示する UI コンポーネント。
/// 表示時にスケールフェードアニメーションを再生し、アニメーション終了後に自動的に非表示にする。
/// </summary>
public class JudgmentTextPopup : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;
    [SerializeField] CanvasGroup     _canvasGroup;

    [Header("Animation")]
    [SerializeField] float _duration   = 0.5f;
    [SerializeField] float _startScale = 1.5f;
    [SerializeField] float _endScale   = 1.0f;

    Coroutine _routine;

    void Awake()
    {
        // Force inactive on startup regardless of Inspector state.
        gameObject.SetActive(false);
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
    }

    public void Show(Judgment j)
    {
        if (_routine != null) StopCoroutine(_routine);

        // SetActive BEFORE StartCoroutine — coroutine can't start on an inactive object.
        gameObject.SetActive(true);

        var style      = JudgmentEffectStyleHelper.GetSaved();
        float scaleMul = JudgmentEffectStyleHelper.GetTextScale(style);

        _text.text  = JudgmentColors.GetText(j);
        _text.color = JudgmentColors.Get(j);

        _routine = StartCoroutine(Animate(scaleMul));
    }

    IEnumerator Animate(float scaleMul)
    {
        _canvasGroup.alpha = 1f;

        for (float t = 0; t < _duration; t += Time.deltaTime)
        {
            float p     = t / _duration;
            float scale = Mathf.Lerp(_startScale, _endScale, p) * scaleMul;
            _text.transform.localScale = Vector3.one * scale;
            _canvasGroup.alpha         = p < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.5f) * 2f);
            yield return null;
        }

        gameObject.SetActive(false);
        _routine = null;
    }
}
