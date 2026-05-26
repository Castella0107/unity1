using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PvpMatchEnd.unity に付ける MonoBehaviour。
    /// PvpFlowController が GoTo(PVPMatchEnd, PvpMatchEndParameters) で遷移してくる。
    /// </summary>
    public class PvpMatchEndController : MonoBehaviour
    {
        [Header("Optional UI (auto fallback to OnGUI if null)")]
        [SerializeField] TextMeshProUGUI _resultHeaderText;
        [SerializeField] TextMeshProUGUI _scoreText;
        [SerializeField] TextMeshProUGUI _breakdownText;
        [SerializeField] TextMeshProUGUI _ratingText;
        [SerializeField] Button          _backToTitleButton;

        PvpMatchEndParameters _params;
        string _header    = "";
        string _scores    = "";
        string _breakdown = "";
        string _ratings   = "";

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            _params = ParameterStore.GetPending<PvpMatchEndParameters>();
            BuildText();
            ApplyToUi();

            if (_backToTitleButton != null)
                _backToTitleButton.onClick.AddListener(() =>
                {
                    if (SceneRouter.Instance != null)
                        SceneRouter.Instance.GoTo(SceneId.Title);
                });
        }

        void BuildText()
        {
            if (_params == null)
            {
                _header  = "PVP Result (no data)";
                _scores  = "";
                _ratings = "";
                return;
            }

            if (!string.IsNullOrEmpty(_params.ErrorMessage))
            {
                _header  = "Match Aborted";
                _scores  = _params.ErrorMessage;
                _ratings = "";
                return;
            }

            bool selfIsA = _params.SelfUserId == _params.UserIdA;
            double selfPts = selfIsA ? _params.TotalPointsA : _params.TotalPointsB;
            double oppPts  = selfIsA ? _params.TotalPointsB : _params.TotalPointsA;
            double selfBefore = selfIsA ? _params.RatingABefore : _params.RatingBBefore;
            double selfAfter  = selfIsA ? _params.RatingAAfter  : _params.RatingBAfter;
            double oppBefore  = selfIsA ? _params.RatingBBefore : _params.RatingABefore;
            double oppAfter   = selfIsA ? _params.RatingBAfter  : _params.RatingAAfter;
            string opponentId = selfIsA ? _params.UserIdB : _params.UserIdA;

            string verdict;
            if (_params.OutcomeKind == 0) verdict = "DRAW";
            else if ((_params.OutcomeKind == 1 && selfIsA) || (_params.OutcomeKind == 2 && !selfIsA)) verdict = "VICTORY";
            else verdict = "DEFEAT";

            _header  = $"{verdict}  vs {opponentId}";
            _scores  = $"You {selfPts:F1}  -  {oppPts:F1} Opponent";

            // 曲別内訳: 難易度と倍率を見せ、重み付き合計の内訳を示す (sum は _scores と一致)。
            if (_params.Songs != null && _params.Songs.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _params.Songs.Count; i++)
                {
                    var s = _params.Songs[i];
                    double sp   = selfIsA ? s.PointsA : s.PointsB;
                    double op   = selfIsA ? s.PointsB : s.PointsA;
                    double mult = Domain.Pvp.MatchScoring.DifficultyMultiplier(s.Difficulty);
                    sb.AppendLine($"{i + 1}. {s.SongId}  [{(s.Difficulty ?? "").ToUpper()} x{mult:0.00}]   {sp:F2} - {op:F2}");
                }
                _breakdown = sb.ToString().TrimEnd();
            }
            else _breakdown = "";

            _ratings = string.Format(
                "Your rating: {0:F1} → {1:F1} ({2:+0.0;-0.0})\nOpponent:   {3:F1} → {4:F1} ({5:+0.0;-0.0})",
                selfBefore, selfAfter, selfAfter - selfBefore,
                oppBefore,  oppAfter,  oppAfter  - oppBefore);
        }

        void ApplyToUi()
        {
            if (_resultHeaderText != null) _resultHeaderText.text = _header;
            if (_scoreText        != null) _scoreText.text        = _scores;
            if (_breakdownText    != null) _breakdownText.text    = _breakdown;
            if (_ratingText       != null) _ratingText.text       = _ratings;
        }

        // ── Fallback OnGUI ─────────────────────────────────────────────────────
        void OnGUI()
        {
            if (_resultHeaderText != null) return;

            const float w = 560f;
            const float h = 380f;   // taller to fit the per-song difficulty/multiplier breakdown
            var rect = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(rect, "PVP Match End");
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 28, rect.width - 32, rect.height - 36));
            GUILayout.Label(_header);
            GUILayout.Space(8);
            GUILayout.Label(_scores);
            if (!string.IsNullOrEmpty(_breakdown))
            {
                GUILayout.Space(8);
                GUILayout.Label(_breakdown);
            }
            GUILayout.Space(8);
            GUILayout.Label(_ratings);
            GUILayout.Space(16);
            if (GUILayout.Button("Back to Title"))
            {
                if (SceneRouter.Instance != null)
                    SceneRouter.Instance.GoTo(SceneId.Title);
            }
            GUILayout.EndArea();
        }
    }
}
