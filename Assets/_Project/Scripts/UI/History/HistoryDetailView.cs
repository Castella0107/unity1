using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 選択された1件の PlayRecord の詳細情報（スコア・判定内訳・セクタースコア・リプレイ情報など）を右パネルに表示するビュー。
/// </summary>
// Displays full details of a single PlayRecord in the right panel.
public class HistoryDetailView : MonoBehaviour
{
    [Header("Song Info")]
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _difficultyText;
    [SerializeField] TextMeshProUGUI _dateText;

    [Header("Score")]
    [SerializeField] TextMeshProUGUI _effectiveScoreText;
    [SerializeField] TextMeshProUGUI _rawScoreText;
    [SerializeField] TextMeshProUGUI _rankText;

    [Header("Badges")]
    [SerializeField] GameObject _fullComboBadge;
    [SerializeField] GameObject _allPerfectBadge;
    [SerializeField] GameObject _allPerfectPlusBadge;

    [Header("Judgments")]
    [SerializeField] TextMeshProUGUI _ppCountText;
    [SerializeField] TextMeshProUGUI _pCountText;
    [SerializeField] TextMeshProUGUI _grCountText;
    [SerializeField] TextMeshProUGUI _gdCountText;
    [SerializeField] TextMeshProUGUI _mCountText;
    [SerializeField] TextMeshProUGUI _maxComboText;
    [SerializeField] TextMeshProUGUI _fastCountText;
    [SerializeField] TextMeshProUGUI _lateCountText;

    [Header("Sectors")]
    [SerializeField] RectTransform _sectorListContent;
    [SerializeField] GameObject    _sectorItemPrefab;

    [Header("Other")]
    [SerializeField] TextMeshProUGUI _modifiersText;
    [SerializeField] TextMeshProUGUI _replayInfoText;

    [Header("Replay")]
    [SerializeField] Button _replayButton;

    PlayRecord _current;

    void Start()
    {
        if (_replayButton != null)
            _replayButton.onClick.AddListener(OnReplayClicked);
    }

    public void Show(PlayRecord r)
    {
        _current = r;

        bool hasReplay = !string.IsNullOrEmpty(r?.ReplayPath)
                      && File.Exists(r.ReplayPath);
        if (_replayButton != null) _replayButton.interactable = hasReplay;

        var dt = DateTimeOffset.FromUnixTimeMilliseconds(r.PlayedAtUnixMs).LocalDateTime;

        if (_titleText      != null) _titleText.text      = r.SongId;
        if (_difficultyText != null) _difficultyText.text = r.Difficulty.ToUpper();
        if (_dateText       != null) _dateText.text       = dt.ToString("yyyy/MM/dd  HH:mm");

        if (_effectiveScoreText != null) _effectiveScoreText.text = r.EffectiveScore.ToString("N0");
        if (_rawScoreText       != null) _rawScoreText.text       = "Raw: " + r.RawScore.ToString("N0");
        if (_rankText           != null) _rankText.text           = r.Rank;

        if (_fullComboBadge        != null) _fullComboBadge.SetActive(r.IsFullCombo);
        if (_allPerfectBadge       != null) _allPerfectBadge.SetActive(r.IsAllPerfect && !r.IsAllPerfectPlus);
        if (_allPerfectPlusBadge   != null) _allPerfectPlusBadge.SetActive(r.IsAllPerfectPlus);

        if (_ppCountText  != null) _ppCountText.text  = r.PerfectPlusCount.ToString();
        if (_pCountText   != null) _pCountText.text   = r.PerfectCount.ToString();
        if (_grCountText  != null) _grCountText.text  = r.GreatCount.ToString();
        if (_gdCountText  != null) _gdCountText.text  = r.GoodCount.ToString();
        if (_mCountText   != null) _mCountText.text   = r.MissCount.ToString();
        if (_maxComboText != null) _maxComboText.text = r.MaxCombo.ToString();
        if (_fastCountText!= null) _fastCountText.text= r.FastCount.ToString();
        if (_lateCountText!= null) _lateCountText.text= r.LateCount.ToString();

        if (_sectorListContent != null && _sectorItemPrefab != null && r.SectorScores != null)
        {
            foreach (Transform t in _sectorListContent) Destroy(t.gameObject);
            for (int i = 0; i < r.SectorScores.Length; i++)
            {
                var go    = Instantiate(_sectorItemPrefab, _sectorListContent);
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length >= 1) texts[0].text = "S" + (i + 1);
                if (texts.Length >= 2) texts[1].text = r.SectorScores[i].ToString("N0");
            }
        }

        if (_modifiersText != null)
            _modifiersText.text = (r.Modifiers != null && r.Modifiers.Length > 0)
                ? "Modifiers: " + string.Join(", ", r.Modifiers)
                : "Modifiers: none";

        if (_replayInfoText != null)
        {
            if (!string.IsNullOrEmpty(r.ReplayPath))
            {
                string fileName = Path.GetFileName(r.ReplayPath);
                long   size     = 0;
                try { size = new FileInfo(r.ReplayPath).Length; } catch { }
                _replayInfoText.text = string.Format("Replay: {0}  ({1:F1} KB)", fileName, size / 1024.0);
            }
            else
            {
                _replayInfoText.text = "Replay: not saved";
            }
        }
    }

    // ── Replay navigation ─────────────────────────────────────────────────────

    void OnReplayClicked()
    {
        if (_current == null || string.IsNullOrEmpty(_current.ReplayPath)) return;
        if (!File.Exists(_current.ReplayPath))
        {
            Debug.LogWarning("[History] Replay file not found: " + _current.ReplayPath);
            return;
        }

        var prm = new GamePlayParameters
        {
            SongId               = _current.SongId,
            Difficulty           = _current.Difficulty,
            IsReplay             = true,
            ReplayPath           = _current.ReplayPath,
            InitialPlaybackSpeed = 1.0,
        };

        if (SceneRouter.Instance != null)
            SceneRouter.Instance.GoTo(SceneId.GamePlay, prm);
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("GamePlay");
    }
}
