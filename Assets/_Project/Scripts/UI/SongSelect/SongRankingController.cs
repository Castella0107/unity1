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
        // リーダーボード結線 (docs/design_doc/leaderboard_client.md §5):
        // 上位 FetchLimit 件 + 自分のパーソナルベストを並列取得して表示する。
        SetStatus("取得中...");

        var lbTask = RhythmGame.Network.Api.LeaderboardApi.GetLeaderboardAsync(
            _params.SongId, _params.Difficulty ?? "extra", FetchLimit);
        var pbTask = RhythmGame.Network.Api.LeaderboardApi.GetPersonalBestAsync(
            _params.SongId, _params.Difficulty ?? "extra");
        await Task.WhenAll(lbTask, pbTask);
        if (this == null) return;

        var lb = lbTask.Result;
        if (!lb.Ok || lb.Data == null)
        {
            SetStatus(lb.ErrorCode == "NO_ACTIVE_SEASON"
                ? "シーズンが開始されていません"
                : $"ランキングを取得できませんでした ({lb.ErrorCode ?? "接続エラー"})");
            return;
        }

        var entries = lb.Data.Entries ?? new RhythmGame.Network.Api.LeaderboardEntryDto[0];
        if (entries.Length == 0)
        {
            SetStatus("まだ記録がありません — 最初の記録を作ろう!");
        }
        else
        {
            SetStatus("");
            string myId = RhythmGame.Network.Api.AuthManager.UserId;
            for (int i = 0; i < (_rows?.Length ?? 0); i++)
            {
                if (_rows[i] == null) continue;
                if (i < entries.Length)
                {
                    var e = entries[i];
                    // サーバーは判定内訳/コンボをランキング一覧に含めない (spec §2-a) —
                    // COMBO 列は 0 表示、FC/AP+ バッジ無しで表示する。
                    _rows[i].SetEntry((int)e.RankPosition, e.DisplayName, e.Score,
                        ScoreCalculator.DisplayRank((int)e.Score, e.Rank),
                                      maxCombo: 0, isFullCombo: false, isAllPerfectPlus: false,
                                      isSelf: e.UserId == myId);
                }
                else _rows[i].Hide();
            }
        }

        // パーソナルベスト (フッター YOUR RANK)
        var pb = pbTask.Result;
        if (_personalText != null)
        {
            if (pb.Ok && pb.Data?.PersonalBest != null)
            {
                var b = pb.Data.PersonalBest;
                _personalText.text = $"YOUR RANK  #{b.RankPosition}   {b.Score:N0}  ({ScoreCalculator.DisplayRank((int)b.Score, b.Rank)})";
            }
            else
            {
                _personalText.text = "YOUR RANK  —";
            }
        }
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
