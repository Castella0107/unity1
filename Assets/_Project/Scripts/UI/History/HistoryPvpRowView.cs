using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ladder match(PVP) 1行のビュー。サマリ(3ジャケット/自分名/勝敗+スコア/相手名)と、
/// 展開時の詳細(曲ごとの S1〜S5 勝敗ダイヤ[cyan=自分勝ち/red=負け] + 精度% + レート変動)を制御する。
/// 各曲ジャケットはボタンで、押すと <see cref="OnSongReplayRequested"/>(曲index) を発火する。
/// 子要素はシーンビルダーで baked-in 済み。
/// </summary>
public class HistoryPvpRowView
{
    /// <summary>行のルート GameObject。</summary>
    public GameObject     Root   { get; }
    /// <summary>サマリ展開用ボタン。</summary>
    public Button         Button { get; }
    /// <summary>この行が表す PVP マッチ記録。</summary>
    public PvpMatchRecord Match  { get; }

    /// <summary>曲ジャケットが押されたとき発火(引数=曲index 0〜2)。</summary>
    public event Action<int> OnSongReplayRequested;

    readonly GameObject _detail;
    readonly Image      _bg;

    static readonly Color IdleColor     = new Color(1f, 1f, 1f, 0.04f);
    static readonly Color SelectedColor = new Color(0.17f, 0.35f, 0.63f, 0.5f);
    static readonly Color CyanWin       = new Color(0.30f, 0.80f, 0.95f, 1f);
    static readonly Color RedLoss       = new Color(0.92f, 0.30f, 0.30f, 1f);

    public HistoryPvpRowView(GameObject go, PvpMatchRecord m, JacketLoader jackets,
                             Func<string, string> titleResolver)
    {
        Root   = go;
        Match  = m;
        Button = go.GetComponent<Button>();
        _bg     = Find<Image>(go, "Background");
        _detail = go.transform.Find("Detail")?.gameObject;

        string self   = m.SelfUserId;
        string opp     = m.OpponentId;
        string result  = m.ResultKind == 1 ? "win" : (m.ResultKind == 2 ? "lose" : "draw");
        string score   = FormatScore(m.SelfPoints, m.OpponentPoints);

        // ── Summary ──
        SetText(go, "Summary/SelfName",   self);
        SetText(go, "Summary/OppName",    opp);
        SetText(go, "Summary/ResultText", result);
        SetText(go, "Summary/ScoreText",  score);
        for (int j = 0; j < 3; j++)
            LoadJacket(jackets, SongIdAt(m, j), Find<RawImage>(go, $"Summary/Jacket{j}"));

        // ── Detail header ──
        SetText(go, "Detail/HeaderSelf",   self);
        SetText(go, "Detail/HeaderOpp",    opp);
        SetText(go, "Detail/HeaderResult", result);
        SetText(go, "Detail/HeaderScore",  score);

        // ── Detail: 曲ごと(3曲) ──
        for (int j = 0; j < 3; j++)
        {
            string songId = SongIdAt(m, j);
            LoadJacket(jackets, songId, Find<RawImage>(go, $"Detail/Song{j}/Jacket"));
            SetText(go, $"Detail/Song{j}/Title", titleResolver != null ? titleResolver(songId) : songId);

            for (int s = 0; s < 5; s++)
            {
                int idx    = j * 5 + s;
                int selfSc = ArrAt(m.SelfSectorScores, idx);
                int oppSc  = ArrAt(m.OpponentSectorScores, idx);
                var dia    = Find<Image>(go, $"Detail/Song{j}/S{s + 1}/Diamond");
                if (dia != null) dia.color = selfSc >= oppSc ? CyanWin : RedLoss;
            }

            int sum = 0;
            for (int s = 0; s < 5; s++) sum += ArrAt(m.SelfSectorScores, j * 5 + s);
            SetText(go, $"Detail/Song{j}/Accuracy", (sum / 10000.0).ToString("F2") + "%");

            int songIndex = j;
            var jacketBtn = Find<Button>(go, $"Detail/Song{j}/Jacket");
            if (jacketBtn != null)
                jacketBtn.onClick.AddListener(() => OnSongReplayRequested?.Invoke(songIndex));
        }

        // ── Detail: レート変動 ──
        SetText(go, "Detail/RatingText", FormatRating(m.SelfRatingBefore, m.SelfRatingAfter));

        SetExpanded(false);
        SetSelected(false);
        SetSongCursor(-1);
    }

    /// <summary>詳細セクションの表示/非表示を切り替える。</summary>
    public void SetExpanded(bool on) { if (_detail != null) _detail.SetActive(on); }

    /// <summary>選択状態に応じて背景色を切り替える。</summary>
    public void SetSelected(bool on) { if (_bg != null) _bg.color = on ? SelectedColor : IdleColor; }

    /// <summary>キーボード操作時の曲カーソル(▷)を移動する。-1 で全て消灯。</summary>
    public void SetSongCursor(int songIndex)
    {
        for (int j = 0; j < 3; j++)
            SetActive(Root, $"Detail/Song{j}/Cursor", j == songIndex);
    }

    // 自分視点スコア "8-7"。.5 は "7.5" のように表示。
    static string FormatScore(double self, double opp) =>
        self.ToString("0.#") + "-" + opp.ToString("0.#");

    static string FormatRating(double before, double after)
    {
        int b = Mathf.RoundToInt((float)before);
        int a = Mathf.RoundToInt((float)after);
        int d = a - b;
        string sign = d >= 0 ? "+" : "";
        return $"R {b} → {a} ({sign}{d})";
    }

    static string SongIdAt(PvpMatchRecord m, int j) =>
        (m.SongIds != null && j < m.SongIds.Length) ? m.SongIds[j] : null;

    static int ArrAt(int[] arr, int idx) =>
        (arr != null && idx >= 0 && idx < arr.Length) ? arr[idx] : 0;

    static async void LoadJacket(JacketLoader loader, string songId, RawImage target)
    {
        if (loader == null || target == null) return;
        var tex = await loader.LoadAsync(songId);
        if (target != null && tex != null) target.texture = tex;
    }

    static T Find<T>(GameObject root, string path) where T : Component
    {
        var t = root.transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    static void SetText(GameObject root, string path, string text)
    {
        var tmp = Find<TextMeshProUGUI>(root, path);
        if (tmp != null) tmp.text = text;
    }

    static void SetActive(GameObject root, string path, bool active)
    {
        var t = root.transform.Find(path);
        if (t != null) t.gameObject.SetActive(active);
    }
}
