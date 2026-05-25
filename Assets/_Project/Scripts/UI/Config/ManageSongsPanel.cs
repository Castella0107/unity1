using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// StreamingAssets/Songs 配下の楽曲一覧表示と削除を行うモーダルパネル。
/// </summary>
/// <remarks>
/// 楽曲行は <see cref="BuildListItem"/> で動的に組み立て、プレハブは要求しない。
/// 削除確認は行内 Delete ボタンの 2 段階方式(1回目で「Confirm」表示、5秒以内の2回目で実行)。
/// </remarks>
public class ManageSongsPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] GameObject       _root;
    [SerializeField] Button           _closeButton;
    [SerializeField] Button           _refreshButton;

    [Header("List")]
    [SerializeField] RectTransform    _listContent;     // VerticalLayoutGroup を持つ Content
    [SerializeField] TextMeshProUGUI  _emptyMessage;    // 楽曲ゼロ時の文言

    const float DeleteConfirmWindowSec = 5f;

    readonly List<GameObject> _itemViews = new List<GameObject>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Start()
    {
        if (_closeButton   != null) _closeButton.onClick.AddListener(Close);
        if (_refreshButton != null) _refreshButton.onClick.AddListener(() => _ = RefreshAsync());
    }

    /// <summary>楽曲管理パネルを開き、一覧を更新する。</summary>
    public void Open()
    {
        if (_root != null) _root.SetActive(true);
        _ = RefreshAsync();
    }

    /// <summary>楽曲管理パネルを閉じる。</summary>
    public void Close()
    {
        if (_root != null) _root.SetActive(false);
    }

    // ── List build ────────────────────────────────────────────────────────────

    async Task RefreshAsync()
    {
        ClearList();

        string songsRoot = Path.Combine(Application.streamingAssetsPath, "Songs");
        if (!Directory.Exists(songsRoot))
        {
            ShowEmpty("Songs ディレクトリが見つかりません: " + songsRoot);
            return;
        }

        var dirs = Directory.GetDirectories(songsRoot);
        if (dirs.Length == 0)
        {
            ShowEmpty("楽曲がありません");
            return;
        }

        if (_emptyMessage != null) _emptyMessage.gameObject.SetActive(false);

        foreach (var dir in dirs)
        {
            string songId = Path.GetFileName(dir);
            SongMetadata meta = null;
            try
            {
                meta = await ChartLoader.LoadMetaAsync(songId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ManageSongs] Skip " + songId + " — meta error: " + e.Message);
            }

            long sizeBytes = GetDirSize(dir);
            BuildListItem(songId, meta, sizeBytes, dir);
        }
    }

    void ClearList()
    {
        foreach (var go in _itemViews) if (go != null) Destroy(go);
        _itemViews.Clear();
    }

    void ShowEmpty(string message)
    {
        if (_emptyMessage == null) return;
        _emptyMessage.text = message;
        _emptyMessage.gameObject.SetActive(true);
    }

    void BuildListItem(string songId, SongMetadata meta, long sizeBytes, string dirPath)
    {
        var row = new GameObject("SongItem_" + songId);
        row.transform.SetParent(_listContent, worldPositionStays: false);

        var rowRt = row.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0, 1);
        rowRt.anchorMax = new Vector2(1, 1);
        rowRt.pivot     = new Vector2(0.5f, 1f);

        // 背景(薄い枠で行を可視化)
        var bgImg = row.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.04f);

        var le = row.AddComponent<LayoutElement>();
        le.minHeight       = 48;
        le.preferredHeight = 48;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(12, 12, 4, 4);
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        string title  = meta?.Title  ?? "(no meta) " + songId;
        string artist = meta?.Artist ?? "";

        CreateLabel(row.transform, title,  flex: 3f);
        CreateLabel(row.transform, artist, flex: 2f);
        CreateLabel(row.transform, FormatBytes(sizeBytes), flex: 1f, alignment: TextAlignmentOptions.Right);

        CreateDeleteButton(row.transform, songId, dirPath);

        _itemViews.Add(row);
    }

    static void CreateLabel(Transform parent, string text, float flex, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 18;
        tmp.color     = Color.white;
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;

        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flex;
        le.minWidth      = 50;
    }

    void CreateDeleteButton(Transform parent, string songId, string dirPath)
    {
        var go = new GameObject("DeleteButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var btnImg = go.AddComponent<Image>();
        btnImg.color = new Color(0.7f, 0.2f, 0.2f, 0.7f);

        var btn = go.AddComponent<Button>();

        var le = go.AddComponent<LayoutElement>();
        le.minWidth       = 120;
        le.preferredWidth = 120;

        // ラベル子要素
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.sizeDelta = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Delete";
        tmp.fontSize  = 18;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        // 2 段階確認
        float confirmDeadline = -1f;
        btn.onClick.AddListener(() =>
        {
            if (Time.unscaledTime <= confirmDeadline)
            {
                btn.interactable = false;
                tmp.text = "Deleting…";
                try { DeleteSongDirectory(dirPath); }
                catch (System.Exception e)
                {
                    Debug.LogError("[ManageSongs] Delete failed for " + songId + ": " + e.Message);
                    tmp.text = "Failed";
                    return;
                }
                Debug.Log("[ManageSongs] Deleted " + songId);
                _ = RefreshAsync();
                return;
            }

            confirmDeadline = Time.unscaledTime + DeleteConfirmWindowSec;
            tmp.text = "Confirm?";
            // 5秒後にラベル復元
            StartCoroutine(ResetLabelAfter(tmp, "Delete", DeleteConfirmWindowSec, () => Time.unscaledTime > confirmDeadline));
        });
    }

    System.Collections.IEnumerator ResetLabelAfter(TextMeshProUGUI label, string original, float seconds, System.Func<bool> stillExpiredPredicate)
    {
        yield return new WaitForSecondsRealtime(seconds + 0.1f);
        if (label != null && stillExpiredPredicate()) label.text = original;
    }

    // ── File operations ──────────────────────────────────────────────────────

    static void DeleteSongDirectory(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;
        Directory.Delete(dirPath, recursive: true);

        // Unity Editor では .meta ファイルも消去して AssetDatabase 整合性を保つ
#if UNITY_EDITOR
        string metaPath = dirPath + ".meta";
        if (File.Exists(metaPath)) File.Delete(metaPath);
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    static long GetDirSize(string path)
    {
        try
        {
            long total = 0;
            foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                total += new FileInfo(f).Length;
            return total;
        }
        catch { return 0; }
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024)                  return bytes + " B";
        if (bytes < 1024 * 1024)           return (bytes / 1024.0).ToString("F1") + " KB";
        if (bytes < 1024L * 1024 * 1024)   return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
        return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
    }
}
