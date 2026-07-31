using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RhythmGame.UI.Common
{
    /// <summary>
    /// ScrollRect に付けると、キーボード/パッドで選択が移動したとき
    /// 選択中の要素が必ず表示範囲に入るよう自動スクロールする。
    ///
    /// スクロールできる UI で「↓を押すと選択は進むが画面外のままで見えない」のは
    /// 操作不能に等しいので、スクロール領域を作るなら必ずセットで入れること
    /// (K 指摘 2026-07-31)。
    ///
    /// 実装方針: 「選択が変わった瞬間」を捉えにいくと取りこぼす条件が多いので、
    /// 毎フレーム『いま選択されているものが見えているか』だけを見て、はみ出していれば
    /// 寄せる。状態を持たないので選択の変わり方 (キー/マウス/コード) に依存しない。
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollToSelection : MonoBehaviour
    {
        [SerializeField] float _padding   = 24f;   // 上下に残す余白 (px)
        [SerializeField] float _lerpSpeed = 16f;   // 0 以下で即座に移動

        ScrollRect _scroll;
        bool       _wasContentActive;

        void Awake() => _scroll = GetComponent<ScrollRect>();

        void LateUpdate()
        {
            if (_scroll == null || _scroll.content == null || _scroll.viewport == null) return;

            // タブ切替は Content の SetActive で行われる。表示された瞬間は必ず先頭に戻す。
            // (前回の位置が残っていると「タブに移った瞬間にスクロールされている」ように見える)
            bool active = _scroll.content.gameObject.activeInHierarchy;
            if (active && !_wasContentActive)
                _scroll.content.anchoredPosition =
                    new Vector2(_scroll.content.anchoredPosition.x, 0f);
            _wasContentActive = active;
            if (!active) return;

            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;

            var target = sel.transform as RectTransform;
            if (target == null || !target.IsChildOf(_scroll.content)) return;

            float delta = ComputeScrollDelta(target);
            if (Mathf.Abs(delta) < 0.5f) return;    // 既に見えている

            var pos  = _scroll.content.anchoredPosition;
            float goal = pos.y + delta;
            pos.y = _lerpSpeed > 0f
                ? Mathf.Lerp(pos.y, goal, 1f - Mathf.Exp(-_lerpSpeed * Time.unscaledDeltaTime))
                : goal;
            _scroll.content.anchoredPosition = pos;
        }

        /// <summary>選択要素を表示範囲へ入れるために content.y をいくら動かすか。</summary>
        float ComputeScrollDelta(RectTransform target)
        {
            var vp = _scroll.viewport;

            var tc = new Vector3[4];
            target.GetWorldCorners(tc);
            float top    = vp.InverseTransformPoint(tc[1]).y;   // 左上
            float bottom = vp.InverseTransformPoint(tc[0]).y;   // 左下

            var   r        = vp.rect;
            float vpTop    = r.yMax - _padding;
            float vpBottom = r.yMin + _padding;

            // ⚠️ 符号に注意: content は上端 pivot・上端アンカーなので、
            //    「下の内容を見せる」= content.anchoredPosition.y を *増やす*。
            //    逆にすると 0 でクランプされて一切動かない (実際にこれで動かなかった)。

            // 選択要素が viewport より高い場合は上端合わせ (下端合わせだと行き過ぎる)
            if (top - bottom > r.height - _padding * 2f) return vpTop - top;

            if (top > vpTop)       return vpTop - top;          // 上へはみ出し → 上の内容へ戻す
            if (bottom < vpBottom) return vpBottom - bottom;    // 下へはみ出し → 下の内容を見せる
            return 0f;
        }
    }
}
