using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 楽曲別ランキング (SongRanking) の1行ビュー。BuildSongRankingScene が baked-in 結線する。
/// RANK / PLAYER / SCORE / 評価ランク / COMBO / バッジ(FC・AP+) の各 TMP と自分行ハイライト用背景を持つ。
/// </summary>
public class RankingRowView : MonoBehaviour
{
    [SerializeField] Image           _background;
    [SerializeField] TextMeshProUGUI _rankText;
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] TextMeshProUGUI _gradeText;
    [SerializeField] TextMeshProUGUI _comboText;
    [SerializeField] TextMeshProUGUI _badgeText;

    static readonly Color BgIdle      = new Color(1f, 1f, 1f, 0.05f);
    static readonly Color BgSelf      = new Color(0.17f, 0.85f, 0.90f, 0.18f);   // 自分=シアン系
    static readonly Color BadgeFc     = new Color(0.31f, 0.76f, 0.97f, 1f);
    static readonly Color BadgeAp     = new Color(1f, 0.82f, 0.24f, 1f);
    static readonly Color TopRankGold = new Color(1f, 0.82f, 0.24f, 1f);

    /// <summary>ランキングエントリ1件を表示する。isSelf で自分行をハイライト。</summary>
    public void SetEntry(int rank, string userId, int score, string grade, int maxCombo,
                         bool isFullCombo, bool isAllPerfectPlus, bool isSelf)
    {
        gameObject.SetActive(true);

        if (_rankText  != null)
        {
            _rankText.text  = rank.ToString();
            _rankText.color = rank <= 3 ? TopRankGold : Color.white;
        }
        if (_nameText  != null) _nameText.text  = userId;
        if (_scoreText != null) _scoreText.text = score.ToString("N0");
        if (_gradeText != null) _gradeText.text = string.IsNullOrEmpty(grade) ? "-" : grade;
        if (_comboText != null) _comboText.text = maxCombo.ToString("N0");
        if (_badgeText != null)
        {
            if      (isAllPerfectPlus) { _badgeText.text = "AP+"; _badgeText.color = BadgeAp; }
            else if (isFullCombo)      { _badgeText.text = "FC";  _badgeText.color = BadgeFc; }
            else                       { _badgeText.text = "";  }
        }
        if (_background != null) _background.color = isSelf ? BgSelf : BgIdle;
    }

    /// <summary>行を非表示にする(エントリ数が行数より少ない場合)。</summary>
    public void Hide() => gameObject.SetActive(false);
}
