using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲームプレイ中の HUD を管理するコンポーネント。
/// 左上の楽曲情報ボックス、上中央の判定カウント、左の縦スコアゲージ＋総合 RATE、
/// および S1..S5 のセクター達成率(菱形マーカー)を表示する。PVP 用の次曲インジケーターも担当する。
///
/// PVP モードでは <see cref="SetPvpContext"/> で右上の相手情報ボックス・上中央の VS スコアバー・
/// セクター勝敗タグを有効化する。相手名と静的レートは試合前に確定済みなので実データを表示するが、
/// 相手の「ライブスコア」(VS バー右側・リード・セクター勝敗) は現バックエンド (リプレイ提出 →
/// 両者提出後に開示) では取得できないため、プレースホルダー (------ / +--- / --) のまま。
/// 相手ライブ化 (ゴースト or ライブ同期) が入ったら SetVsSplit / sector 勝敗を実データで駆動する。
/// </summary>
public class GameHud : MonoBehaviour
{
    [Header("Song Info Box (top-left)")]
    [SerializeField] RawImage        _jacket;
    [SerializeField] TextMeshProUGUI _songTitle;
    [SerializeField] TextMeshProUGUI _songArtist;
    [SerializeField] TextMeshProUGUI _difficulty;

    [Header("Judgment Counts (top-center)")]
    [SerializeField] TextMeshProUGUI _ppCount;
    [SerializeField] TextMeshProUGUI _pCount;
    [SerializeField] TextMeshProUGUI _grCount;
    [SerializeField] TextMeshProUGUI _gdCount;
    [SerializeField] TextMeshProUGUI _mCount;

    [Header("Score Panel (left)")]
    [SerializeField] Image           _scoreGauge;   // filled vertical, fillAmount = score/1,000,000
    [SerializeField] TextMeshProUGUI _scoreValue;
    [SerializeField] TextMeshProUGUI _rateValue;
    [SerializeField] TextMeshProUGUI _maxComboValue; // プレイフィールド モック: MAX COMBO (ゼロ埋め3桁)
    [SerializeField] TextMeshProUGUI _sectorCounter; // プレイフィールド モック: セクター表示 "03/05"
    [SerializeField] Image           _sectorHalo;    // 現在セクターのハロー (P-Glow、現在ひし形の背後)

    [Header("Sector Diamonds — index 0 = S1 (bottom) .. 4 = S5 (top)")]
    [SerializeField] Image[]           _sectorDiamonds = new Image[5];
    [SerializeField] TextMeshProUGUI[] _sectorPercents = new TextMeshProUGUI[5];

    [Header("Opponent Info Box (top-right, PVP)")]
    [SerializeField] GameObject      _opponentBox;
    [SerializeField] TextMeshProUGUI _opponentName;
    [SerializeField] TextMeshProUGUI _opponentRate;

    [Header("VS Score Bar (top-center, PVP)")]
    [SerializeField] GameObject      _vsBar;
    [SerializeField] Image           _vsSelfFill;   // blue, fills from left
    [SerializeField] Image           _vsOppFill;    // red, fills from right
    [SerializeField] TextMeshProUGUI _vsSelfScore;
    [SerializeField] TextMeshProUGUI _vsOppScore;
    [SerializeField] TextMeshProUGUI _vsLead;       // "+590" lead margin (self − opp)

    [Header("Sector Win/Lose Tags (PVP) — index 0 = S1")]
    [SerializeField] TextMeshProUGUI[] _sectorOutcomes = new TextMeshProUGUI[5];

    [Header("Next Song Indicator (PVP Only)")]
    [SerializeField] GameObject      _nextIndicator;
    [SerializeField] RawImage        _nextJacket;
    [SerializeField] TextMeshProUGUI _nextSongTitle;

    [Header("Refs")]
    [SerializeField] JudgmentSystem  _judgment;
    [SerializeField] AudioConductor  _conductor;

    static readonly Color ColIdle   = new Color(0.35f, 0.35f, 0.35f, 1f);  // future sector
    static readonly Color ColJacketFallback = new Color(0.12f, 0.13f, 0.18f, 1f);

    SongMetadata _meta;
    ChartData    _chart;
    JacketLoader _jacketLoader;
    int          _shownSectorIdx = -1;   // highest sector index whose final value is locked in
    bool         _isPvp;                  // SetPvpContext turns on the opponent box / VS bar / outcome tags

    // ── Update の変化検出キャッシュ ──────────────────────────────────────────────
    // TMP の .text セッターはメッシュ再構築を強制する。毎フレーム同じ値を代入すると
    // 60fps 中ずっと ~7 要素を再構築し続けるため、前回表示値を保持して変化時のみ更新する。
    // -1 = 未初期化(初回 Update で必ず一度反映される)。
    readonly int[] _shownCounts = { -1, -1, -1, -1, -1 };
    int  _shownScore    = -1;
    int _shownMaxCombo = -1;
    int _shownSectorNo = -1;
    int  _shownRateKey  = -1;   // Rate をキー化(score,max 依存)して比較する
    int  _shownVsSelf   = -1;
    int  _shownVsOpp    = -1;
    readonly int[] _shownDiamondKey = { -1, -1, -1, -1, -1 };  // セクター菱形の前回表示率(×100 整数キー)

    // 判定カウントを変化時のみ更新するヘルパー(TMP 再構築を抑制)。
    void SetCount(TextMeshProUGUI label, int slot, int value)
    {
        if (label == null || _shownCounts[slot] == value) return;
        _shownCounts[slot] = value;
        label.text = value.ToString();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// 曲名・アーティスト名に日本語フォントを直接割り当てる。
    ///
    /// シーンのテキストは既定フォント (LiberationSans) で、日本語は TMP Settings の
    /// グローバルフォールバック (NotoSansJP) 経由で描画される。タイトル画面や曲リストでは
    /// これで出るが、GamePlay の HUD だけ CJK が一切描画されず、ASCII だけが残っていた
    /// (K 報告 2026-08-01:「天神楽」が丸ごと消え、エチュードは先頭の "2" だけ残る)。
    /// フォールバックは字形ごとに子のサブメッシュを作る仕組みで、この HUD では
    /// それが表示されない。曲名は日本語が入る前提の箇所なので、フォールバックに頼らず
    /// 日本語フォントを主フォントとして直接指定する (NotoSansJP は ASCII も持つ)。
    ///
    /// 参照はフォールバック登録済みの TMP Settings から取るので、
    /// シーンやプレハブに新しい参照を足す必要がない。
    /// </summary>
    static void ApplyJapaneseFont(TMP_Text t)
    {
        if (t == null) return;
        var list = TMP_Settings.fallbackFontAssets;
        if (list == null || list.Count == 0 || list[0] == null) return;
        if (t.font == list[0]) return;
        t.font = list[0];   // マテリアルも追従する
    }

    /// <summary>楽曲メタ・譜面・PvP フラグから HUD(曲情報・スコア・セクターパネル等)を初期化する。</summary>
    public void Initialize(SongMetadata meta, ChartData chart, bool isPvP)
    {
        _meta  = meta;
        _chart = chart;

        // 日本語が入りうるテキストはまとめて主フォントを差し替えておく
        // (次曲は PVP の曲間、相手名は表示名に日本語が使える)
        ApplyJapaneseFont(_songTitle);
        ApplyJapaneseFont(_songArtist);
        ApplyJapaneseFont(_nextSongTitle);
        ApplyJapaneseFont(_opponentName);

        if (_songTitle  != null) _songTitle.text  = meta.Title;
        if (_songArtist != null) _songArtist.text = meta.Artist;
        if (_difficulty != null)
        {
            _difficulty.text  = $"{DifficultyLabel(chart.Difficulty)} {chart.Level}";
            _difficulty.color = RankColors.GetDifficultyColor(chart.Difficulty);
        }

        LoadJacket(meta.SongId);

        if (_scoreValue != null) _scoreValue.text = "0";
        if (_rateValue  != null) _rateValue.text  = "000.00<color=#E5C06C>%</color>"; // % は P-Text
        if (_maxComboValue != null) _maxComboValue.text = "000";
        if (_sectorCounter != null) _sectorCounter.text = "01/05";
        if (_scoreGauge != null) _scoreGauge.fillAmount = 0f;

        // 変化検出キャッシュをリセット(曲を跨いだ HUD 再利用でも初回 Update で必ず反映されるように)。
        for (int c = 0; c < _shownCounts.Length; c++)    _shownCounts[c] = -1;
        for (int c = 0; c < _shownDiamondKey.Length; c++) _shownDiamondKey[c] = -1;
        _shownScore = _shownRateKey = _shownVsSelf = _shownVsOpp = -1;

        // コンボ表示: 位置設定を読み直し、前回プレイの表示をリセット
        _shownCombo = -1;
        if (_comboText != null)
        {
            ApplyComboLayout((RectTransform)_comboText.transform);
            _comboText.gameObject.SetActive(false);
        }

        // 最大到達可能ランク (理論値): コンボ数の下・PLAY OPTIONS で ON/OFF
        _showMaxScore   = PlayOptionsController.ShowMaxScore;
        _shownMaxRemain = -1;
        _shownMaxRank   = null;
        if (_maxScoreText == null && _showMaxScore) EnsureMaxScoreText();
        if (_maxScoreText != null)
        {
            ApplyMaxScoreLayout((RectTransform)_maxScoreText.transform);
            _maxScoreText.gameObject.SetActive(_showMaxScore);
            SetMaxRank(1_000_000);
        }

        _shownSectorIdx = -1;
        for (int i = 0; i < _sectorDiamonds.Length; i++)
        {
            bool exists = _meta.Sectors != null && i < _meta.Sectors.Count;
            if (_sectorDiamonds[i] != null)
            {
                _sectorDiamonds[i].color = ColIdle;
                _sectorDiamonds[i].enabled = exists;
            }
            if (_sectorPercents[i] != null)
                _sectorPercents[i].text = exists ? "--" : "";
        }

        if (_nextIndicator != null) _nextIndicator.SetActive(isPvP);

        // PVP-only elements default off; SetPvpContext (called by GamePlayController in PVP)
        // turns them on. BindStageVisuals always passes isPvP:false, so the opponent box /
        // VS bar stay hidden in solo play.
        _isPvp = false;
        if (_opponentBox != null) _opponentBox.SetActive(false);
        if (_vsBar       != null) _vsBar.SetActive(false);
        for (int i = 0; i < _sectorOutcomes.Length; i++)
            if (_sectorOutcomes[i] != null) _sectorOutcomes[i].text = "";
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (_judgment == null || _conductor == null || _meta == null) return;

        var agg = _judgment.Aggregator;
        if (agg == null) return;

        // 判定カウント: 各要素は変化時のみ再構築。
        SetCount(_ppCount, 0, agg.Counts[(int)Judgment.PerfectPlus]);
        SetCount(_pCount,  1, agg.Counts[(int)Judgment.Perfect]);
        SetCount(_grCount, 2, agg.Counts[(int)Judgment.Great]);
        SetCount(_gdCount, 3, agg.Counts[(int)Judgment.Good]);
        SetCount(_mCount,  4, agg.Counts[(int)Judgment.Miss]);

        int score = agg.CurrentScore;
        if (score != _shownScore)
        {
            _shownScore = score;
            if (_scoreValue != null) _scoreValue.text = score.ToString("N0"); // カンマ区切り (K 指示 2026-08-01)
            if (_scoreGauge != null) _scoreGauge.fillAmount = score / 1_000_000f;
        }

        UpdateMaxScoreCounter(agg);

        // MAX COMBO (モック: ゼロ埋め 3 桁)
        if (_maxComboValue != null && agg.MaxCombo != _shownMaxCombo)
        {
            _shownMaxCombo = agg.MaxCombo;
            _maxComboValue.text = Mathf.Min(agg.MaxCombo, 999).ToString("D3");
        }

        UpdateComboCounter(agg.CurrentCombo);

        // セクターカウンター (モック: "03/05")
        if (_sectorCounter != null)
        {
            int cur = Mathf.Clamp(agg.CurrentSectorIdx + 1, 1, 5);
            if (cur != _shownSectorNo)
            {
                _shownSectorNo = cur;
                _sectorCounter.text = cur.ToString("D2") + "/05";
            }
        }

        // Overall RATE = accuracy = score / max-so-far. At song end == score/10000.
        // score/max のどちらかが変わったときだけ再計算・再構築(小数第2位までの整数キーで比較)。
        if (_rateValue != null)
        {
            float ratePct = Rate(score, agg.CurrentMaxScore);
            int   rateKey = Mathf.RoundToInt(ratePct * 100f);
            if (rateKey != _shownRateKey)
            {
                _shownRateKey = rateKey;
                _rateValue.text = ratePct.ToString("000.00") + "<color=#E5C06C>%</color>"; // % は P-Text
            }
        }

        // PVP: VS バー。自分側=ライブスコア、相手側=WS opponent_progress 受信値 (M5 でライブ化)。
        if (_isPvp)
        {
            if (_vsSelfScore != null && score != _shownVsSelf)
            {
                _shownVsSelf = score;
                _vsSelfScore.text = score.ToString("N0");
            }
            if (_oppLiveScore >= 0)
            {
                if (_vsOppScore != null && _oppLiveScore != _shownVsOpp)
                {
                    _shownVsOpp = _oppLiveScore;
                    _vsOppScore.text = _oppLiveScore.ToString("N0");
                }
                SetLead(score, _oppLiveScore);
                long sum = (long)score + _oppLiveScore;
                SetVsSplit(sum > 0 ? (float)score / sum : 0.5f);
            }
        }

        UpdateSectorDiamonds(agg);
    }

    // ── コンボ表示 (K 指示 2026-07-30: 位置と表示はコンフィグで変更可) ──
    // 位置プリセット: 0=中央 / 1=上部中央 / 2=手元。
    // 手元のみチュウニズム参考の「レーン溶け込み」スタイル (K 指示 2026-08-01):
    // HUD キャンバスは Screen Space Camera なので X 軸回転で本物の遠近がつく。
    // 寝かせ+縁取りなし+低アルファで路面ペイント風に見せる (ワールド空間 TMP は
    // URP での描画が安定しなかったため不採用)。
    // どのプリセットでも「コンボが下・最大到達ランクが上」で互いに重ねない。

    TMP_Text _comboText;   // 実行時生成 (シーン変更なしで導入するため)
    int _shownCombo = -1;

    /// <summary>溶け込みプリセットの寝かせ角 (X 軸回転、度)。</summary>
    const float LaneTiltDeg = 55f;

    // コンボのポップ演出は localScale を使うため、基準スケールに乗算して戻す
    float _comboBaseScale = 1f;

    /// <summary>現在コンボの表示を更新する。3 コンボ以上で表示し、増加時に軽くポップする。</summary>
    void UpdateComboCounter(int combo)
    {
        bool enabled = PlayerPrefs.GetInt("ComboShow", 1) == 1;
        bool show    = enabled && combo >= 3;
        if (_comboText == null)
        {
            if (!show) { _shownCombo = combo; return; }
            EnsureComboText();
        }
        if (_comboText.gameObject.activeSelf != show) _comboText.gameObject.SetActive(show);
        if (!show) { _shownCombo = combo; return; }

        if (combo != _shownCombo)
        {
            bool increased = combo > _shownCombo;
            _shownCombo = combo;
            _comboText.text = "<size=40%><color=#9AB4C2>COMBO</color></size>\n" + combo;
            if (increased) _comboText.transform.localScale = Vector3.one * (_comboBaseScale * 1.12f);   // ポップ
        }
        float s = _comboText.transform.localScale.x;   // ポップの減衰 (基準スケールまで戻す)
        if (s > _comboBaseScale)
            _comboText.transform.localScale = Vector3.one * Mathf.Max(_comboBaseScale, s - Time.deltaTime * 0.8f);
    }

    void EnsureComboText()
    {
        var go = new GameObject("ComboCounter", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        _comboText = tmp;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.fontSize      = 87f;   // K 指示 2026-08-01: 従来 58 の 1.5 倍
        tmp.raycastTarget = false;
        tmp.rectTransform.sizeDelta = new Vector2(560f, 180f);
        ApplyComboLayout(tmp.rectTransform);
    }

    // ── 最大到達可能ランク表示 (K 指示 2026-08-01: チュウニズムの MAX 表示風) ──────────
    // 1,000,000 から PERFECT 以外で失った分を引いた「今から全部 PERFECT ならここまで届く」
    // スコアのランクと数値 (例: SS 0997800) をレーン床面に表示する。
    // 表示位置は常にコンボの奥 (画面上で上)、ON/OFF は PLAY OPTIONS ("ShowMaxScore")。

    TMP_Text _maxScoreText;   // 実行時生成 (シーン変更なしで導入するため)
    int    _shownMaxRemain = -1;
    string _shownMaxRank;
    bool   _showMaxScore;

    void UpdateMaxScoreCounter(PlayProgressAggregator agg)
    {
        if (!_showMaxScore || _maxScoreText == null) return;
        // maxSoFar − score = ここまでに失った点。表示丸め (micro→表示int) は両者同方向なので誤差は±1に収まる
        int remain = Mathf.Clamp(1_000_000 - (agg.CurrentMaxScore - agg.CurrentScore), 0, 1_000_000);
        if (remain == _shownMaxRemain) return;
        _shownMaxRemain = remain;
        SetMaxRank(remain);
    }

    // 「ランク + 理論値スコア」の並びで表示 (K 指示 2026-08-01: ランクだけにしない)。
    // ランク文字はランク色、数字はやや小さく薄い白。
    void SetMaxRank(int remain)
    {
        if (_maxScoreText == null) return;
        string rank = ScoreCalculator.ComputeRank(remain);
        if (rank != _shownMaxRank)
        {
            _shownMaxRank = rank;
            var c = RankColors.GetRankColor(rank);
            c.a = 0.55f;   // 薄く・プレイの邪魔にならない
            _maxScoreText.color = c;
        }
        _maxScoreText.text =
            rank + " <size=66%><color=#FFFFFF99>" + remain.ToString("N0") + "</color></size>";
    }

    void EnsureMaxScoreText()
    {
        var go = new GameObject("MaxRankCounter", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        _maxScoreText = tmp;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.fontSize      = 34f;   // K 指示 2026-08-01: ランクとスコアはもう少し大きく (コンボはそのまま)
        tmp.raycastTarget = false;
        tmp.rectTransform.sizeDelta = new Vector2(420f, 48f);
        ApplyMaxScoreLayout(tmp.rectTransform);
    }

    // コンボ表示と同じ 3 プリセット (MaxScorePosIdx) を独立に選べる。
    // どのプリセットでもコンボの上に置く (0: +5→+85 / 1: 230→330 / 2: -140→-60)。
    void ApplyMaxScoreLayout(RectTransform rt)
    {
        int  preset = PlayerPrefs.GetInt("MaxScorePosIdx", 0);
        bool lane   = preset != 1;   // 上部中央のみレーン外
        switch (preset)
        {
            case 1:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 330f);
                break;
            case 2:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -60f);
                break;
            default:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 85f);   // コンボ (+5) の上をキープ
                break;
        }
        rt.localRotation = lane ? Quaternion.Euler(LaneTiltDeg, 0f, 0f) : Quaternion.identity;
        rt.localScale    = Vector3.one;

        var tmp = rt.GetComponent<TMP_Text>();
        if (tmp != null) tmp.outlineWidth = lane ? 0f : 0.2f;
    }

    // 位置プリセット (PlayerPrefs "ComboPosIdx"、K 指示 2026-08-01):
    //   0 = レーン中央 — 手元より高い位置で溶け込み (寝かせ)
    //   1 = 上部中央 — 消失点 (KALPA 仕様 VP=640,256/720p → 1080 基準で中心 +156) の上、
    //       レーンと被らないので通常表示 (寝かせ・減光なし)
    //   2 = 手元 — 判定ポップアップ (上端 -183) のすぐ上で溶け込み
    void ApplyComboLayout(RectTransform rt)
    {
        int  preset = PlayerPrefs.GetInt("ComboPosIdx", 0);
        bool lane   = preset != 1;   // 上部中央のみレーン外
        switch (preset)
        {
            case 1:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 230f);
                break;
            case 2:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -140f);
                break;
            default:
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 5f);   // K 指示 2026-08-01: 若干上へ (-25→5)
                break;
        }
        rt.localRotation = lane ? Quaternion.Euler(LaneTiltDeg, 0f, 0f) : Quaternion.identity;
        _comboBaseScale  = 1f;
        rt.localScale    = Vector3.one;

        var tmp = rt.GetComponent<TMP_Text>();
        if (tmp != null)
        {
            // 溶け込み時は縁取りを消し、アルファを下げて路面ペイント風にする
            tmp.color        = new Color(0.933f, 0.976f, 0.992f, lane ? 0.50f : 0.92f);
            tmp.outlineWidth = lane ? 0f : 0.2f;
            tmp.outlineColor = new Color(0.04f, 0.04f, 0.07f, 0.92f);
        }
    }

    // ── Sector diamonds ─────────────────────────────────────────────────────────

    // Driven off the aggregator's CurrentSectorIdx so the display matches the exact
    // moment each sector's score delta is finalized (idx > i ⇒ sector i is locked in).
    void UpdateSectorDiamonds(PlayProgressAggregator agg)
    {
        int curIdx     = agg.CurrentSectorIdx;
        int sectorCount = _meta.Sectors != null ? _meta.Sectors.Count : 0;

        // Lock in newly-completed sectors with their final accuracy + rank color.
        while (_shownSectorIdx < curIdx - 1 && _shownSectorIdx < 4)
        {
            _shownSectorIdx++;
            int i = _shownSectorIdx;
            if (i < sectorCount)
                SetDiamond(i, Rate(agg.SectorScores[i], agg.SectorMaxScores[i]), final: true);
        }

        // Live update for the in-progress sector — climbs 0% → final achievement as its
        // notes are played, using the sector's full theoretical max (all notes) as 100%.
        if (curIdx < 5 && curIdx < sectorCount && agg.CurrentSectorFullMaxScore > 0)
            SetDiamond(curIdx, Rate(agg.CurrentSectorScore, agg.CurrentSectorFullMaxScore), final: false);
    }

    void SetDiamond(int i, float ratePct, bool final)
    {
        if (i < 0 || i >= _sectorDiamonds.Length) return;
        // 進行中セクターは毎フレーム呼ばれるため、表示率(×100 整数キー)が変わったときだけ再構築する。
        int key = Mathf.RoundToInt(ratePct * 100f);
        if (!final && _shownDiamondKey[i] == key) return;
        _shownDiamondKey[i] = key;
        string rank = ScoreCalculator.ComputeRank(Mathf.RoundToInt(ratePct * 10000f));
        if (_sectorDiamonds[i] != null)
        {
            // COLOR_SPEC: 進行中 (現在) のひし形は P-Core、確定はランク色。
            _sectorDiamonds[i].color = final
                ? RankColors.GetRankColor(rank)
                : new Color(251 / 255f, 238 / 255f, 208 / 255f, 1f); // P-Core #FBEED0
            _sectorDiamonds[i].rectTransform.localScale = final ? Vector3.one : Vector3.one * 1.4f;
            // ハロー (P-Glow) を現在ひし形の背後へ移動
            if (!final && _sectorHalo != null)
            {
                _sectorHalo.enabled = true;
                _sectorHalo.rectTransform.anchoredPosition = _sectorDiamonds[i].rectTransform.anchoredPosition;
            }
        }
        if (_sectorPercents[i] != null) _sectorPercents[i].text  = ratePct.ToString("F2") + "%";
    }

    // ── PVP helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// PVP モードの HUD 要素 (右上の相手情報ボックス・上中央の VS スコアバー・セクター勝敗タグ) を
    /// 有効化する。GamePlayController が IsPvp 時に Initialize の後で呼ぶ。相手名と静的レートは実データ、
    /// 相手ライブスコア依存 (VS バー右側・リード・セクター勝敗) はプレースホルダー。
    /// </summary>
    public void SetPvpContext(string opponentId)
    {
        _isPvp = true;
        if (_opponentBox != null) _opponentBox.SetActive(true);
        if (_vsBar       != null) _vsBar.SetActive(true);

        if (_opponentName != null)
        {
            // 表示名がコンテキストで解決済みならそれを使う (user_id の生表示を避ける)
            var ctx = RhythmGame.Network.Api.PvpMatchContext.OpponentId == opponentId
                ? RhythmGame.Network.Api.PvpMatchContext.OpponentDisplayName : null;
            _opponentName.text = !string.IsNullOrEmpty(ctx) ? ctx
                : string.IsNullOrEmpty(opponentId) ? "OPPONENT" : opponentId;
        }
        if (_opponentRate != null) _opponentRate.text = "Rate ----";   // 取得まではプレースホルダー

        // 相手ライブスコア未供給: VS バー右側 / リード / セクター勝敗はプレースホルダー。
        if (_vsOppScore != null) _vsOppScore.text = "------";
        if (_vsLead     != null) _vsLead.text     = "+---";
        SetVsSplit(0.5f);   // 相手データが無い間は中立 (50/50)

        int sectorCount = _meta != null && _meta.Sectors != null ? _meta.Sectors.Count : 0;
        for (int i = 0; i < _sectorOutcomes.Length; i++)
            if (_sectorOutcomes[i] != null) _sectorOutcomes[i].text = i < sectorCount ? "--" : "";

        FetchOpponentRate(opponentId);
    }

    int _oppLiveScore = -1;   // WS opponent_progress の最新スコア (-1=未受信でプレースホルダー維持)

    /// <summary>相手のライブスコアを供給する (WS opponent_progress → GamePlayController 経由)。</summary>
    public void UpdateOpponentLive(long score)
    {
        _oppLiveScore = (int)System.Math.Min(score, int.MaxValue);
    }

    /// <summary>VS バーの綱引き比率を設定する(self の取り分 0..1)。相手ライブ化時に実スコア比で駆動。</summary>
    void SetVsSplit(float selfShare)
    {
        selfShare = Mathf.Clamp01(selfShare);
        if (_vsSelfFill != null) _vsSelfFill.fillAmount = selfShare;
        if (_vsOppFill  != null) _vsOppFill.fillAmount  = 1f - selfShare;
    }

    static readonly Color ColLeadPos = new Color(0.20f, 0.85f, 0.95f, 1f);  // cyan: リード(self が上)
    static readonly Color ColLeadNeg = new Color(0.95f, 0.25f, 0.25f, 1f);  // red: ビハインド(self が下)

    /// <summary>
    /// VS バーのリード差 (self − opp) を符号規則で表示する: 0 は色を付けず「0」、負は赤字、正はシアンで「+n」。
    /// 相手ライブスコアが供給されたとき (ゴースト/ライブ同期) に Update から呼ぶ。現状は相手スコア不在の
    /// ためプレースホルダー「+---」のまま (SetPvpContext)。
    /// </summary>
    void SetLead(int selfScore, int oppScore)
    {
        if (_vsLead == null) return;
        int diff = selfScore - oppScore;
        if (diff == 0)     { _vsLead.text = "0";             _vsLead.color = Color.white; }
        else if (diff < 0) { _vsLead.text = diff.ToString(); _vsLead.color = ColLeadNeg; }   // "-590"
        else               { _vsLead.text = "+" + diff;      _vsLead.color = ColLeadPos; }   // "+590"
    }

    // 相手の静的レート (試合前に確定) を取得して右上ボックスに表示する。取得失敗時はプレースホルダー据置。
    async void FetchOpponentRate(string opponentId)
    {
        if (string.IsNullOrEmpty(opponentId)) return;
        try
        {
            var r = await RhythmGame.Network.Api.PvpApi.GetUserAsync(opponentId);
            if (_opponentRate == null) return;   // destroyed while awaiting
            if (r != null && r.Ok && r.Data != null)
            {
                _opponentRate.text = "Rate " + Mathf.RoundToInt((float)r.Data.Rating);
                if (_opponentName != null && !string.IsNullOrEmpty(r.Data.DisplayName))
                    _opponentName.text = r.Data.DisplayName;
            }
        }
        catch { /* プレースホルダーのまま */ }
    }

    /// <summary>次曲のタイトルとジャケットを表示する(PVP の曲間表示用)。</summary>
    public void SetNextSong(string title, Texture2D jacket)
    {
        if (_nextSongTitle != null) _nextSongTitle.text = title;
        if (_nextJacket    != null) _nextJacket.texture = jacket;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    static float Rate(int score, int maxScore)
        => maxScore > 0 ? score / (float)maxScore * 100f : 0f;

    static string DifficultyLabel(string diff)
    {
        switch (diff?.ToLower())
        {
            case "normal": return "NML";
            case "hard":   return "HRD";
            case "extra":  return "EX";
            default:       return string.IsNullOrEmpty(diff) ? "??" : diff.ToUpper();
        }
    }

    async void LoadJacket(string songId)
    {
        if (_jacket == null) return;
        _jacket.color = Color.white;
        _jacketLoader ??= new JacketLoader();
        var tex = await LoadJacketTexture(songId);
        if (_jacket == null) return;   // destroyed while awaiting
        if (tex != null) _jacket.texture = tex;
        else             _jacket.color   = ColJacketFallback;
    }

    System.Threading.Tasks.Task<Texture2D> LoadJacketTexture(string songId)
        => _jacketLoader.LoadAsync(songId);
}
