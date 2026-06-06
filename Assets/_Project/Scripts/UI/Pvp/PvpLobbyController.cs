using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// オンラインロビー (対戦待合, フロー ②)。Title の Online → ここ → START で Matchmaking。
    ///
    /// データ方針 (UIガワ + 既存データのみ結線):
    ///   - 右パネルの TOTAL MATCH / MATCH WIN / WIN RATIO は <c>GET /api/pvp/user/{id}/stats</c> の実データ。
    ///   - ティア(LADDER TIER) / LP / SEASON TOP5 / YOUR RANKING / 難易度別スタッツ表 は
    ///     K のレーティング設計ドメインのため未実装 → "UNRANKED" / "--" / "0" のプレースホルダー表示。
    ///     K のラダー/ティア API ができたら該当フィールドに結線する。
    ///
    /// START: F5 キー or START ボタンで <see cref="SceneId.Matchmaking"/> へ。
    /// UI は BuildPvpScenes が baked-in 結線、未結線なら OnGUI フォールバック。
    /// </summary>
    public class PvpLobbyController : MonoBehaviour
    {
        [Header("Right panel — real data")]
        [SerializeField] TextMeshProUGUI _ladderTierText;   // "UNRANKED" (placeholder)
        [SerializeField] TextMeshProUGUI _totalMatchText;
        [SerializeField] TextMeshProUGUI _matchWinText;
        [SerializeField] TextMeshProUGUI _winRatioText;
        [SerializeField] TextMeshProUGUI _centerTierText;   // 中央の大バッジ "UNRANKED"

        [Header("Left panel — placeholder (K ladder API 待ち)")]
        [SerializeField] TextMeshProUGUI _seasonText;       // "SEASON --"
        [SerializeField] TextMeshProUGUI _yourRankingText;  // "-.--% OF TOP"

        [Header("Buttons")]
        [SerializeField] Button _startButton;
        [SerializeField] Button _backButton;

        static readonly Color Gold = new Color(0.97f, 0.78f, 0.25f, 1f);

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();

            if (_startButton != null) _startButton.onClick.AddListener(OnStart);
            if (_backButton  != null) _backButton.onClick.AddListener(OnBack);

            RhythmGame.UI.Common.ShortcutHintOverlay.Set("Space: START (RANKED MATCH)      ESC: タイトルへ");

            // プレースホルダー初期値 (K ドメイン)
            if (_ladderTierText != null) _ladderTierText.text = "UNRANKED";
            if (_centerTierText != null) _centerTierText.text = "UNRANKED";
            if (_seasonText     != null) _seasonText.text     = "SEASON --";
            if (_yourRankingText!= null) _yourRankingText.text= "-.--%  OF TOP";

            // 実データ初期値 (取得前)
            if (_totalMatchText != null) _totalMatchText.text = "0";
            if (_matchWinText   != null) _matchWinText.text   = "0";
            if (_winRatioText   != null) _winRatioText.text   = "0.00%";

            _ = LoadStatsAsync();
        }

        async Task LoadStatsAsync()
        {
            var net = NetworkClient.Instance;
            if (net == null) return;
            var r = await net.FetchPvpUserStatsAsync(LocalIdentity.UserId);
            if (!r.Ok || r.Body == null) return;

            if (_totalMatchText != null) _totalMatchText.text = r.Body.totalMatches.ToString();
            if (_matchWinText   != null) _matchWinText.text   = r.Body.wins.ToString();
            if (_winRatioText   != null) _winRatioText.text   = $"{r.Body.winRatio * 100.0:0.00}%";
        }

        void Update()
        {
            // Space=START / ESC=タイトルへ（未マッチなので確認ダイアログなし）。Input System 専用。
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame)  OnStart();
                if (kb.escapeKey.wasPressedThisFrame) OnBack();
            }

            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                if (RhythmGame.Input.GamepadLayout.ConfirmPressed(pad)) OnStart();
                if (RhythmGame.Input.GamepadLayout.BackPressed(pad))    OnBack();
            }
        }

        void OnStart()
        {
            SceneRouter.Instance?.GoTo(SceneId.Matchmaking);
        }

        void OnBack()
        {
            SceneRouter.Instance?.GoTo(SceneId.Title);
        }

        // ── OnGUI フォールバック ───────────────────────────────────────
        void OnGUI()
        {
            if (_startButton != null) return;
            const float w = 520f, h = 260f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, "ONLINE LOBBY");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));
            GUILayout.Label("LADDER TIER: UNRANKED");
            GUILayout.Label($"Player: {LocalIdentity.UserId}");
            GUILayout.Space(10);
            if (GUILayout.Button("START  (RANKED MATCH)")) OnStart();
            if (GUILayout.Button("BACK")) OnBack();
            GUILayout.EndArea();
        }
    }
}
