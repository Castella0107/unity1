using System.Text;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PVP 本戦の各曲完走後に出す「曲リザルト」画面 (フロー ⑩、PVPResult シーンを使用)。
    /// 完全同期で取得した <see cref="PvpFlowController.LastSongResult"/> を読み、
    ///   - この曲のセクター単位 (S1..S5) の勝敗 ◆WIN / ◇LOSE / — DRAW
    ///   - この曲の獲得ポイント (難易度倍率込み)
    ///   - 両者の累計ポイント (8pt 先取で決着)
    /// を自分(シアン)/相手(レッド)視点で表示する。NEXT で次曲 or 最終結果へ。
    /// matchOver(8pt クリンチ or 最終曲) のときは NEXT を "FINAL RESULT" にして PvpMatchEnd へ。
    /// </summary>
    public class PvpSongResultController : MonoBehaviour
    {
        [Header("Optional UI (OnGUI fallback if header is null)")]
        [SerializeField] TextMeshProUGUI _headerText;     // "SONG 1 / 3  RESULT"
        [SerializeField] TextMeshProUGUI _songTitleText;
        [SerializeField] TextMeshProUGUI _selfPointsText; // この曲の自分pt
        [SerializeField] TextMeshProUGUI _oppPointsText;  // この曲の相手pt
        [SerializeField] TextMeshProUGUI _sectorsText;    // S1..S5 の勝敗行
        [SerializeField] TextMeshProUGUI _cumulativeText; // 累計 YOU x.x - y.y OPP
        [SerializeField] TextMeshProUGUI _clinchText;     // クリンチ告知
        [SerializeField] TextMeshProUGUI _primaryLabel;
        [SerializeField] Button          _primaryButton;

        static readonly Color Cyan = new Color(0.17f, 0.85f, 0.90f, 1f);
        static readonly Color Red  = new Color(0.95f, 0.30f, 0.42f, 1f);
        const string CyanHex = "#2BD9E6";
        const string RedHex  = "#F24D6B";

        PvpFlowController Pvp => PvpFlowController.Instance;

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_primaryButton != null) _primaryButton.onClick.AddListener(OnPrimary);
            if (_clinchText    != null) _clinchText.text = "";

            Render();
        }

        void OnPrimary()
        {
            var pvp = Pvp;
            if (pvp != null && pvp.IsActive) pvp.AfterSongResult();
            else SceneRouter.Instance?.GoTo(SceneId.Title);
        }

        void Render()
        {
            var pvp = Pvp;
            var sr  = pvp?.LastSongResult;
            if (pvp == null || sr == null)
            {
                if (_headerText != null) _headerText.text = "SONG RESULT";
                if (_sectorsText != null) _sectorsText.text = "(no result)";
                SetPrimary("CONTINUE");
                return;
            }

            int idx   = pvp.CurrentSongIndex;
            int total = pvp.Songs?.Count ?? 3;
            string songId = (pvp.Songs != null && idx < pvp.Songs.Count) ? pvp.Songs[idx].songId : "";
            string diff   = (pvp.Songs != null && idx < pvp.Songs.Count) ? pvp.Songs[idx].difficulty : "";

            if (_headerText    != null) _headerText.text    = $"SONG {idx + 1} / {total}   RESULT";
            if (_songTitleText != null) _songTitleText.text = string.IsNullOrEmpty(diff) ? songId : $"{songId}   [{diff}]";

            if (_selfPointsText != null) { _selfPointsText.text = $"YOU  +{sr.selfSongPoints:0.0}"; _selfPointsText.color = Cyan; }
            if (_oppPointsText  != null) { _oppPointsText.text  = $"OPP  +{sr.oppSongPoints:0.0}";  _oppPointsText.color  = Red; }

            // セクター勝敗行 (selfSectors vs oppSectors)
            if (_sectorsText != null)
                _sectorsText.text = BuildSectorLine(sr);

            // 累計 (8pt で決着)
            if (_cumulativeText != null)
            {
                _cumulativeText.text =
                    $"MATCH TOTAL    <color={CyanHex}>YOU {sr.selfCumulative:0.0}</color>   -   " +
                    $"<color={RedHex}>{sr.oppCumulative:0.0} OPP</color>      (first to 8.0)";
            }

            if (_clinchText != null)
                _clinchText.text = sr.clinch ? "CLINCH!  8 POINTS REACHED" : "";

            SetPrimary(sr.matchOver ? "FINAL RESULT" : "NEXT SONG");
        }

        string BuildSectorLine(SongResultDto sr)
        {
            var self = sr.selfSectors;
            var opp  = sr.oppSectors;
            var sb = new StringBuilder();
            sb.Append("SECTORS   ");
            for (int i = 0; i < 5; i++)
            {
                int a = (self != null && i < self.Count) ? self[i] : 0;
                int b = (opp  != null && i < opp.Count)  ? opp[i]  : 0;
                string tag;
                if      (a > b) tag = $"<color={CyanHex}>S{i + 1}◆</color>";   // ◆ WIN (cyan)
                else if (a < b) tag = $"<color={RedHex}>S{i + 1}◇</color>";    // ◇ LOSE (red)
                else            tag = $"S{i + 1}—";                            // — DRAW
                sb.Append(tag);
                if (i < 4) sb.Append("   ");
            }
            return sb.ToString();
        }

        void SetPrimary(string label)
        {
            if (_primaryLabel  != null) _primaryLabel.text = label;
            if (_primaryButton != null) _primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }

        // ── OnGUI フォールバック ───────────────────────────────────────
        void OnGUI()
        {
            if (_headerText != null) return;
            var pvp = Pvp;
            var sr  = pvp?.LastSongResult;

            const float w = 560f, h = 360f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, "SONG RESULT");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));

            if (pvp == null || sr == null)
            {
                GUILayout.Label("(no result)");
                if (GUILayout.Button("CONTINUE")) OnPrimary();
                GUILayout.EndArea();
                return;
            }

            int idx = pvp.CurrentSongIndex;
            GUILayout.Label($"SONG {idx + 1} / {pvp.Songs?.Count ?? 3}");
            GUILayout.Space(4);
            GUILayout.Label($"This song:  YOU +{sr.selfSongPoints:0.0}   OPP +{sr.oppSongPoints:0.0}");

            var self = sr.selfSectors; var opp = sr.oppSectors;
            var line = new StringBuilder("Sectors: ");
            for (int i = 0; i < 5; i++)
            {
                int a = (self != null && i < self.Count) ? self[i] : 0;
                int b = (opp  != null && i < opp.Count)  ? opp[i]  : 0;
                line.Append(a > b ? "WIN " : a < b ? "LOSE " : "DRAW ");
            }
            GUILayout.Label(line.ToString());
            GUILayout.Space(4);
            GUILayout.Label($"TOTAL: YOU {sr.selfCumulative:0.0} - {sr.oppCumulative:0.0} OPP  (first to 8.0)");
            if (sr.clinch) GUILayout.Label("CLINCH! 8 points reached");

            GUILayout.Space(10);
            if (GUILayout.Button(sr.matchOver ? "FINAL RESULT" : "NEXT SONG")) OnPrimary();
            GUILayout.EndArea();
        }
    }
}
