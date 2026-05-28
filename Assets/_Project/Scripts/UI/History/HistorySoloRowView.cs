using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Free play(ソロ) 1行のビュー。サマリ(ジャケット/曲名/スコア/FC/AP/日付)と、
/// 展開時の詳細(判定内訳 + S1〜S5 セクター[score を red→green グラデで色付け] + 精度%)を制御する。
/// 子要素はシーンビルダーで baked-in 済み。ランタイムでは UI を生成せず値の設定のみ行う。
/// </summary>
public class HistorySoloRowView
{
    /// <summary>行のルート GameObject。</summary>
    public GameObject Root   { get; }
    /// <summary>行クリック用ボタン。</summary>
    public Button     Button { get; }
    /// <summary>この行が表すプレイ記録(ベスト)。</summary>
    public PlayRecord Record { get; }

    readonly GameObject _detail;
    readonly Image      _bg;

    static readonly Color IdleColor     = new Color(1f, 1f, 1f, 0.04f);
    static readonly Color SelectedColor = new Color(0.17f, 0.35f, 0.63f, 0.5f);

    public HistorySoloRowView(GameObject go, PlayRecord rec, JacketLoader jackets)
    {
        Root   = go;
        Record = rec;
        Button = go.GetComponent<Button>();
        _bg     = Find<Image>(go, "Background");
        _detail = go.transform.Find("Detail")?.gameObject;

        // ── Summary ──
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(rec.PlayedAtUnixMs).LocalDateTime;
        SetText(go, "Summary/DateText",  dt.ToString("yyyy/MM/dd"));
        SetText(go, "Summary/ScoreText", rec.EffectiveScore.ToString("N0"));
        SetText(go, "Summary/TitleText", rec.SongId);   // controller が SetTitle で曲名に差し替え

        SetActive(go, "Summary/FCBadge", rec.IsFullCombo);
        bool ap = rec.IsAllPerfect || rec.IsAllPerfectPlus;
        SetActive(go, "Summary/APBadge", ap);
        if (ap) SetText(go, "Summary/APBadge/Label", rec.IsAllPerfectPlus ? "AP+" : "AP");

        LoadJacket(jackets, rec.SongId, Find<RawImage>(go, "Summary/Jacket"));

        // ── Detail: judgment breakdown ──
        SetText(go, "Detail/PpText", "perfect+   " + rec.PerfectPlusCount);
        SetText(go, "Detail/PText",  "perfect    " + rec.PerfectCount);
        SetText(go, "Detail/GrText", "great      " + rec.GreatCount);
        SetText(go, "Detail/GdText", "good       " + rec.GoodCount);
        SetText(go, "Detail/MText",  "miss       " + rec.MissCount);

        // ── Detail: sectors (既存 Result と同じ red→green グラデ) ──
        var sectors = rec.SectorScores ?? new int[5];
        for (int i = 0; i < 5; i++)
        {
            int sc = i < sectors.Length ? sectors[i] : 0;
            var dia = Find<Image>(go, $"Detail/Sectors/S{i + 1}/Diamond");
            if (dia != null) dia.color = SectorColor(sc);
            SetText(go, $"Detail/Sectors/S{i + 1}/Score", sc.ToString("N0"));
        }

        // ── Detail: accuracy (raw 0〜1,000,000 → %) ──
        SetText(go, "Detail/AccuracyText", (rec.RawScore / 10000.0).ToString("F2") + "%");

        SetExpanded(false);
        SetSelected(false);
    }

    /// <summary>詳細セクションの表示/非表示を切り替える。</summary>
    public void SetExpanded(bool on) { if (_detail != null) _detail.SetActive(on); }

    /// <summary>選択状態に応じて背景色を切り替える。</summary>
    public void SetSelected(bool on) { if (_bg != null) _bg.color = on ? SelectedColor : IdleColor; }

    /// <summary>曲名(meta.json 解決後)をサマリに設定する。</summary>
    public void SetTitle(string title) => SetText(Root, "Summary/TitleText", title);

    // セクター満点 200,000 を 1.0 とした red(低)→green(高) グラデ (UI/Result/ResultController と同一)
    static Color SectorColor(int score)
    {
        float ratio = Mathf.Clamp01(score / 200_000f);
        return Color.Lerp(new Color(1f, .3f, .3f, 1f), new Color(.3f, .9f, .3f, 1f), ratio);
    }

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
