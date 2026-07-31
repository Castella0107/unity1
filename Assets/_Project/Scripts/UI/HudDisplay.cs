using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// スコア・コンボ・判定カウント・ランクをトップHUDに表示するコンポーネント。
/// JudgmentSystem を毎フレーム参照するポーリング方式で動作する。
///
/// 表示値が変わったフレームだけ文字列を組み直す。以前は毎フレーム string.Format +
/// score.ToString("D7") を実行しており、値が動かない間もゴミを出し続けていた
/// (2026-07-31 の軽量化: ゲームプレイ中は毎フレーム数万バイト・千数百回のアロケーションがあり、
/// GC スパイクが体感の引っかかりにつながっていた)。
/// </summary>
// Displays score, combo, judgment counts and rank in a top HUD.
// Rebuilds the string only when a displayed value actually changes.

public class HudDisplay : MonoBehaviour
{
    [SerializeField] JudgmentSystem  _system;
    [SerializeField] TextMeshProUGUI _hudText;     // single text for all stats

    readonly StringBuilder _sb = new StringBuilder(160);

    // 直近に表示した値 (-1 = 未表示)
    int _pPlus = -1, _perfect = -1, _great = -1, _good = -1, _miss = -1;
    int _combo = -1, _score = -1;

    private void Update()
    {
        if (_system == null || _system.Aggregator == null || _hudText == null) return;

        var agg   = _system.Aggregator;
        var c     = agg.Counts;
        int score = agg.CurrentScore;
        int combo = agg.CurrentCombo;

        int pPlus   = c[(int)Judgment.PerfectPlus];
        int perfect = c[(int)Judgment.Perfect];
        int great   = c[(int)Judgment.Great];
        int good    = c[(int)Judgment.Good];
        int miss    = c[(int)Judgment.Miss];

        if (pPlus == _pPlus && perfect == _perfect && great == _great && good == _good &&
            miss == _miss && combo == _combo && score == _score)
            return;

        _pPlus = pPlus; _perfect = perfect; _great = great; _good = good; _miss = miss;
        _combo = combo; _score = score;

        _sb.Clear();
        _sb.Append("P+ <b>").Append(pPlus)
           .Append("</b>   P <b>").Append(perfect)
           .Append("</b>   Gr <b>").Append(great)
           .Append("</b>   Gd <b>").Append(good)
           .Append("</b>   M <b>").Append(miss)
           .Append("</b>     COMBO <b>").Append(combo)
           .Append("</b>     SCORE <b>");
        AppendPadded7(_sb, score);
        _sb.Append("</b>     <b>").Append(ScoreCalculator.ComputeRank(score)).Append("</b>");

        // TMP は SetText(StringBuilder) で中間の string を作らずに反映できる
        _hudText.SetText(_sb);
    }

    /// <summary>score.ToString("D7") 相当をアロケーション無しで書き込む。</summary>
    static void AppendPadded7(StringBuilder sb, int value)
    {
        if (value < 0) { sb.Append(value); return; }
        int digits = 1;
        for (int v = value; v >= 10; v /= 10) digits++;
        for (int i = digits; i < 7; i++) sb.Append('0');
        sb.Append(value);
    }
}
