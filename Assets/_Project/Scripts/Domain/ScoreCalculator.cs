// Unity-independent. No UnityEngine references allowed in this assembly.
// Shared verbatim with the server-side score validation pipeline.
//
// Micro-point system
//   Each note is worth X = 1,000,000 / N display points.
//   Internally we track X_micro using CEILING division so that
//   N * X_micro >= 10^12, guaranteeing all-perfect = 1,000,000 exactly
//   for any N (even if N doesn't divide 10^12 evenly).
//   Partial-score judgments (Great = 199/200, Good = 3/4) are applied to X_micro.

public class ScoreCalculator
{
    private readonly long _xMicro;   // micro-points per perfect note
    private long          _scoreMicro;

    public ScoreCalculator(int totalNotes)
    {
        // Ceiling division: ensures totalNotes * _xMicro >= 10^12
        // so all-perfect always rounds down to exactly 1,000,000.
        _xMicro = (1_000_000_000_000L + totalNotes - 1) / totalNotes;
    }

    public void Add(Judgment j)
    {
        switch (j)
        {
            case Judgment.PerfectPlus: _scoreMicro += _xMicro;               break;
            case Judgment.Perfect:     _scoreMicro += _xMicro;               break;
            case Judgment.Great:       _scoreMicro += _xMicro * 199L / 200L; break;
            case Judgment.Good:        _scoreMicro += _xMicro * 3L   / 4L;   break;
            case Judgment.Miss:        /* +0 */                               break;
        }
    }

    /// Display score in [0, 1,000,000] range.
    public int CurrentScore => (int)(_scoreMicro / 1_000_000L);

    public static string ComputeRank(int score)
    {
        if (score >= 997_000) return "S+";
        if (score >= 990_000) return "S";
        if (score >= 950_000) return "A+";
        if (score >= 900_000) return "A";
        if (score >= 800_000) return "B";
        if (score >= 700_000) return "C";
        return "D";
    }
}
