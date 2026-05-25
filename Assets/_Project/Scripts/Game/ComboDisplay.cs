using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// コンボ数を UI テキストで表示し、コンボ更新時にスケールアニメーション（バンプ）を再生する。
/// マイルストーン（50, 100, 250, 500, 1000）達成時は通常より大きく強調表示される。
/// </summary>
public class ComboDisplay : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _comboText;
    [SerializeField] TextMeshProUGUI _comboLabel;

    [Header("Animation")]
    [SerializeField] float _bumpScale    = 1.3f;
    [SerializeField] float _bumpDuration = 0.15f;

    static readonly int[] Milestones = { 50, 100, 250, 500, 1000 };

    int       _currentCombo = -1;
    Coroutine _bumpRoutine;

    void Awake()
    {
        if (_comboText  != null) _comboText.text  = "0";
        if (_comboLabel != null) _comboLabel.text  = "COMBO";
        gameObject.SetActive(false);
    }

    /// <summary>コンボ数を設定して表示を更新する(0 でリセット)。</summary>
    public void SetCombo(int combo)
    {
        if (combo == _currentCombo) return;

        if (combo == 0)
        {
            _currentCombo = 0;
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        bool milestone = IsMilestone(combo) && combo > _currentCombo;
        _currentCombo     = combo;
        if (_comboText != null) _comboText.text = combo.ToString();

        if (_bumpRoutine != null) StopCoroutine(_bumpRoutine);
        _bumpRoutine = StartCoroutine(Bump(milestone));
    }

    static bool IsMilestone(int combo)
    {
        foreach (int m in Milestones) if (combo == m) return true;
        return false;
    }

    IEnumerator Bump(bool milestone)
    {
        float targetScale = milestone ? _bumpScale * 1.5f : _bumpScale;
        Color targetColor = milestone ? new Color(1f, 0.9f, 0.4f) : Color.white;

        // Expand
        float riseTime = _bumpDuration * 0.4f;
        for (float t = 0; t < riseTime; t += Time.deltaTime)
        {
            float p = t / riseTime;
            if (_comboText != null)
            {
                _comboText.transform.localScale = Vector3.one * Mathf.Lerp(1f, targetScale, p);
                _comboText.color = Color.Lerp(Color.white, targetColor, p);
            }
            yield return null;
        }

        // Shrink back
        float fallTime = _bumpDuration * 0.6f;
        for (float t = 0; t < fallTime; t += Time.deltaTime)
        {
            float p = t / fallTime;
            if (_comboText != null)
            {
                _comboText.transform.localScale = Vector3.one * Mathf.Lerp(targetScale, 1f, p);
                _comboText.color = Color.Lerp(targetColor, Color.white, p);
            }
            yield return null;
        }

        if (_comboText != null)
        {
            _comboText.transform.localScale = Vector3.one;
            _comboText.color = Color.white;
        }
        _bumpRoutine = null;
    }
}
