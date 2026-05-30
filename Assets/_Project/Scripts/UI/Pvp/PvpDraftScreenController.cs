using System.Text;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PVP 正規フロー 3 画面 (Prematch / SongPick / BanPhase) 共通コントローラー。
    /// 現段階は「表示・演出のみ」: サーバーが決めた 3 曲をそのまま提示し、NEXT/START で進める
    /// (BAN/PICK の実選択・サーバー同期は未実装)。マッチ情報は PvpFlowController.Instance から読む。
    /// Inspector に Text/Button を割り当てれば正規 UI、未割当でも OnGUI フォールバックで動く。
    /// </summary>
    public class PvpDraftScreenController : MonoBehaviour
    {
        public enum Phase { Prematch, SongPick, BanPhase }

        [SerializeField] Phase _phase = Phase.Prematch;

        [Header("Optional UI (OnGUI fallback if header text is null)")]
        [SerializeField] TextMeshProUGUI _headerText;
        [SerializeField] TextMeshProUGUI _youNameText;
        [SerializeField] TextMeshProUGUI _oppNameText;
        [SerializeField] TextMeshProUGUI _infoText;
        [SerializeField] TextMeshProUGUI _songsText;
        [SerializeField] TextMeshProUGUI _primaryLabel;   // primary ボタンのラベル (NEXT / START)
        [SerializeField] Button          _primaryButton;
        [SerializeField] Button          _cancelButton;

        void Start()
        {
            // MCP/ウィンドウ非フォーカス時もループを止めないために必須 (他 PVP 画面と同様)。
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_primaryButton != null) _primaryButton.onClick.AddListener(OnPrimary);
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancel);

            Populate();
        }

        void Populate()
        {
            var pvp = PvpFlowController.Instance;
            bool active = pvp != null && pvp.IsActive;

            if (_headerText  != null) _headerText.text  = HeaderFor(_phase);
            if (_youNameText != null) _youNameText.text = active ? pvp.SelfUserId : LocalIdentity.UserId;
            if (_oppNameText != null) _oppNameText.text = active && !string.IsNullOrEmpty(pvp.OpponentId) ? pvp.OpponentId : "???";
            if (_infoText    != null) _infoText.text    = active ? InfoFor(_phase) : "(no active match)";
            if (_songsText   != null) _songsText.text   = active ? BuildSongs(pvp) : "";
            if (_primaryLabel != null) _primaryLabel.text = active ? PrimaryLabelFor(_phase) : "BACK";
        }

        static string HeaderFor(Phase p) => p switch
        {
            Phase.Prematch => "MATCH READY",
            Phase.SongPick => "SONG LINEUP",
            Phase.BanPhase => "READY TO BATTLE",
            _ => "PVP",
        };

        static string InfoFor(Phase p) => p switch
        {
            Phase.Prematch => "BEST OF 3   ·   3 SONGS × 5 SECTORS",
            Phase.SongPick => "These 3 songs were drawn from the season pool",
            Phase.BanPhase => "All locked in — press START to begin",
            _ => "",
        };

        static string PrimaryLabelFor(Phase p) => p == Phase.BanPhase ? "START MATCH" : "NEXT >";

        static string BuildSongs(PvpFlowController pvp)
        {
            var songs = pvp.Songs;
            if (songs == null || songs.Count == 0) return "(no songs)";
            var sb = new StringBuilder();
            for (int i = 0; i < songs.Count; i++)
            {
                var s = songs[i];
                string diff = string.IsNullOrEmpty(s.difficulty) ? "extra" : s.difficulty;
                sb.Append($"{i + 1}.   {s.songId}    [{diff}]");
                if (i < songs.Count - 1) sb.Append('\n');
            }
            return sb.ToString();
        }

        // ── フロー前進 ───────────────────────────────────────────────────────
        void OnPrimary()
        {
            var pvp = PvpFlowController.Instance;
            if (pvp == null || !pvp.IsActive)
            {
                // アクティブな試合が無い (単独 Play 等) → Title へ戻る
                SceneRouter.Instance?.GoTo(SceneId.Title);
                return;
            }
            switch (_phase)
            {
                case Phase.Prematch: SceneRouter.Instance?.GoTo(SceneId.PVPSongPick); break;
                case Phase.SongPick: SceneRouter.Instance?.GoTo(SceneId.PVPBanPhase); break;
                case Phase.BanPhase: pvp.BeginSongs(); break;   // 1 曲目の GamePlay を起動
            }
        }

        void OnCancel()
        {
            var pvp = PvpFlowController.Instance;
            if (pvp != null && pvp.IsActive) pvp.CancelMatch();
            else SceneRouter.Instance?.GoTo(SceneId.Title);
        }

        // ── OnGUI フォールバック (シーン UI 未組込みでも操作可) ─────────────────
        void OnGUI()
        {
            if (_headerText != null) return;
            var pvp = PvpFlowController.Instance;
            bool active = pvp != null && pvp.IsActive;

            const float w = 600f, h = 340f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, HeaderFor(_phase));
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));
            if (active)
            {
                string opp = string.IsNullOrEmpty(pvp.OpponentId) ? "???" : pvp.OpponentId;
                GUILayout.Label($"{pvp.SelfUserId}    VS    {opp}");
                GUILayout.Space(6);
                GUILayout.Label(InfoFor(_phase));
                GUILayout.Space(6);
                GUILayout.Label(BuildSongs(pvp));
                GUILayout.Space(10);
                if (GUILayout.Button(PrimaryLabelFor(_phase))) OnPrimary();
                if (GUILayout.Button("CANCEL")) OnCancel();
            }
            else
            {
                GUILayout.Label("(no active PVP match)");
                if (GUILayout.Button("BACK TO TITLE")) SceneRouter.Instance?.GoTo(SceneId.Title);
            }
            GUILayout.EndArea();
        }
    }
}
