using UnityEditor;
using UnityEngine;

/// <summary>
/// FX ノーツ円弧の見た目をエディタで確認する一時プレビュー。
/// PLAYFIELD_SPEC.md §3 の R(z)=465·155/(155+z) に沿って複数の z でタップ円弧と
/// ホールド円弧を静的に生成する。確認後は Clear で撤去する。
/// </summary>
public static class FxArcNotePreview
{
    const string RootName = "FxArcNotePreview";

    [MenuItem("Tools/Playfield/Preview FX Arc Notes")]
    public static void Spawn()
    {
        Clear();
        var root = new GameObject(RootName);

        // 左レーン: タップを等時間間隔で (z px: 判定 0 → 奥)
        foreach (float zPx in new[] { 0f, 90f, 220f, 400f, 620f })
        {
            var n = FxArcNote.Create(root.transform);
            n.gameObject.name = $"TapL_z{zPx:F0}";
            n.SetColor(Color.white);
            n.UpdateTap(right: false, zPx, 0.035f);
        }

        // 右レーン: ホールド (頭 z=60 → 尾 z=330) とタップ 1 つ
        var hold = FxArcNote.Create(root.transform);
        hold.gameObject.name = "HoldR";
        hold.SetColor(Color.white);
        hold.UpdateHold(right: true, 60f, 330f, consumed: false, 0.035f);

        var tap = FxArcNote.Create(root.transform);
        tap.gameObject.name = "TapR_z500";
        tap.SetColor(Color.white);
        tap.UpdateTap(right: true, 500f, 0.035f);

        Debug.Log("[FxArcNotePreview] spawned");
    }

    [MenuItem("Tools/Playfield/Preview FX Arc Notes - Clear")]
    public static void Clear()
    {
        var old = GameObject.Find("/" + RootName);
        if (old != null) Object.DestroyImmediate(old);
    }
}
