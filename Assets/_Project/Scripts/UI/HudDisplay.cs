using TMPro;
using UnityEngine;

// Displays score, combo, judgment counts and rank in a top HUD.
// Reads JudgmentSystem every frame — no event subscription needed.

public class HudDisplay : MonoBehaviour
{
    [SerializeField] JudgmentSystem  _system;
    [SerializeField] TextMeshProUGUI _hudText;     // single text for all stats

    private void Update()
    {
        if (_system == null || _system.Aggregator == null || _hudText == null) return;

        var agg   = _system.Aggregator;
        var c     = agg.Counts;
        int score = agg.CurrentScore;

        _hudText.text = string.Format(
            "P+ <b>{0}</b>   P <b>{1}</b>   Gr <b>{2}</b>   Gd <b>{3}</b>   M <b>{4}</b>" +
            "     COMBO <b>{5}</b>     SCORE <b>{6}</b>     <b>{7}</b>",
            c[(int)Judgment.PerfectPlus],
            c[(int)Judgment.Perfect],
            c[(int)Judgment.Great],
            c[(int)Judgment.Good],
            c[(int)Judgment.Miss],
            agg.CurrentCombo,
            score.ToString("D7"),
            ScoreCalculator.ComputeRank(score));
    }
}
