using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 楽曲別ランキング画面 (画面遷移図 2026-06-07 改訂 ④)。
/// SongSelect の R キーで遷移し、選択中の曲・難易度のオンラインランキングを表示する。
///
/// データ: GET /api/leaderboard/{songId}/{difficulty} (上位) + /me (自分の順位)。
/// 操作: ↑↓=スクロール / ESC・Back=SongSelect へ復帰 (選曲状態も復元)。
/// UI は BuildSongRankingScene が baked-in 結線、未結線なら OnGUI フォールバック。
/// </summary>
public class SongRankingController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _artistText;
    [SerializeField] TextMeshProUGUI _diffText;

    [Header("Rows (baked-in)")]
    [SerializeField] RankingRowView[] _rows;
    [SerializeField] TextMeshProUGUI  _statusText;    // "取得中..." / "記録なし" / エラー
    [SerializeField] ScrollRect       _scrollRect;

    [Header("Footer")]
    [SerializeField] TextMeshProUGUI _personalText;   // YOUR RANK
    [SerializeField] Button          _backButton;

    const int FetchLimit = 20;

    SongRankingParameters _params;
    string _fallbackStatus = "LOADING...";

    void Start()
    {
        Application.runInBackground = true;
        JacketBackgroundController.Instance?.SetCanvasEnabled(true);

        _params = ParameterStore.GetPending<SongRankingParameters>();

        if (_backButton != null) _backButton.onClick.AddListener(OnBack);

        RhythmGame.UI.Common.ShortcutHintOverlay.Set("↑↓: スクロール      ESC: 選曲へ戻る");

        if (_titleText  != null) _titleText.text  = _params?.SongTitle ?? "---";
        if (_artistText != null) _artistText.text = _params?.Artist ?? "";
        if (_diffText   != null) _diffText.text   = (_params?.Difficulty ?? "-").ToUpperInvariant();
        if (_statusText != null) _statusText.text = "取得中...";
        if (_personalText != null) _personalText.text = "";
        if (_rows != null) foreach (var r in _rows) if (r != null) r.Hide();

        if (_params == null || string.IsNullOrEmpty(_params.SongId))
        {
            SetStatus("曲が指定されていません");
            return;
        }

        JacketBackgroundController.Instance?.SetJacket(_params.SongId);
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        var net = NetworkClient.Instance;
        if (net == null) { SetStatus("オフライン (サーバー未接続)"); return; }

        // 上位ランキング
        var r = await net.FetchLeaderboardAsync(_params.SongId, _params.Difficulty, FetchLimit);
        if (this == null) return;   // シーン離脱後の戻り
        if (!r.Ok || r.Body == null)
        {
            SetStatus($"取得失敗: {r.Error}");
            return;
        }

        var entries = r.Body.entries;
        if (entries == null || entries.Count == 0)
        {
            SetStatus("まだ記録がありません");
        }
        else
        {
            SetStatus(null);
            string self = LocalIdentity.UserId;
            for (int i = 0; _rows != null && i < _rows.Length; i++)
            {
                if (_rows[i] == null) continue;
                if (i < entries.Count)
                {
                    var e = entries[i];
                    _rows[i].SetEntry(e.rank, e.userId, e.score, e.scoreRank, e.maxCombo,
                                      e.isFullCombo, e.isAllPerfectPlus, e.userId == self);
                }
                else
                {
                    _rows[i].Hide();
                }
            }
        }

        // 自分の順位
        var me = await net.FetchPersonalBestAsync(_params.SongId, _params.Difficulty, LocalIdentity.UserId);
        if (this == null) return;
        if (_personalText == null) return;
        if (me.Ok && me.Body != null && me.Body.hasRecord)
            _personalText.text = $"YOUR RANK   #{me.Body.overallRank} / {me.Body.totalUsers}      SCORE {me.Body.best.score:N0}";
        else
            _personalText.text = "YOUR RANK   -- (記録なし)";
    }

    void SetStatus(string msg)
    {
        _fallbackStatus = msg ?? "";
        if (_statusText == null) return;
        _statusText.text = msg ?? "";
        _statusText.gameObject.SetActive(!string.IsNullOrEmpty(msg));
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame) OnBack();

            // ↑↓ でスクロール (ScrollRect 結線時のみ)
            if (_scrollRect != null)
            {
                float dir = 0f;
                if (kb.upArrowKey.isPressed   || kb.wKey.isPressed) dir = +1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) dir = -1f;
                if (dir != 0f)
                    _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(
                        _scrollRect.verticalNormalizedPosition + dir * Time.unscaledDeltaTime * 1.2f);
            }
        }

        var pad = Gamepad.current;
        if (pad != null && RhythmGame.Input.GamepadLayout.BackPressed(pad)) OnBack();
    }

    void OnBack()
    {
        // SongSelect へ復帰 (選曲カーソルと難易度を復元)
        SceneRouter.Instance?.GoTo(SceneId.SongSelect, new SongSelectParameters
        {
            FocusSongId = _params?.SongId,
            Difficulty  = _params?.Difficulty,
        });
    }

    // ── OnGUI フォールバック (シーン未結線時) ────────────────────────
    void OnGUI()
    {
        if (_backButton != null) return;
        const float w = 560f, h = 240f;
        var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
        GUI.Box(r, "SONG RANKING");
        GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));
        GUILayout.Label($"Song: {_params?.SongTitle ?? "---"} [{_params?.Difficulty ?? "-"}]");
        GUILayout.Label(_fallbackStatus);
        GUILayout.Space(10);
        if (GUILayout.Button("BACK (ESC)")) OnBack();
        GUILayout.EndArea();
    }
}
