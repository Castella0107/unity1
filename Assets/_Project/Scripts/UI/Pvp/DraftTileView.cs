using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// ドラフト(PICK/BAN)の 1 タイル。楽曲ジャケット + ラベル + 選択枠 + タグ + ディマー を持つ。
    /// レイアウト(子の構成・座標)は <c>BuildPvpScenes</c> が baked-in 生成し、各 SerializeField を結線する。
    /// 実行時は <c>PvpDraftScreenController</c> が songId / sprite / 選択状態 / タグ を流し込む。
    /// ([[feedback_unityRuntimeUiInLayoutGroup]] によりタイルはランタイム生成せず baked-in。)
    /// </summary>
    public class DraftTileView : MonoBehaviour
    {
        [SerializeField] Button          _button;
        [SerializeField] Image           _jacket;         // ジャケット画像 (無ければ非表示でフォールバック)
        [SerializeField] TextMeshProUGUI _label;          // 曲名 / songId
        [SerializeField] Image           _selectionFrame; // 選択ハイライト枠
        [SerializeField] Image           _dim;            // 暗転オーバーレイ (BAN/非選択)
        [SerializeField] TextMeshProUGUI _tag;            // "YOU" / "OPP" / "BANNED" 等

        /// <summary>このタイルが表す楽曲 ID。</summary>
        public string SongId { get; private set; }
        /// <summary>クリック判定用ボタン(コントローラがリスナを張る)。</summary>
        public Button Button => _button;

        /// <summary>このタイルに楽曲を割り当て、ラベルを設定して初期状態へ戻す。</summary>
        public void SetSong(string songId, string label)
        {
            SongId = songId;
            if (_label != null) _label.text = label;
            SetSelected(false);
            SetDimmed(false);
            ClearTag();
            if (_jacket != null) _jacket.enabled = false;   // sprite が来るまで非表示
            gameObject.SetActive(true);
        }

        /// <summary>ラベル文字列だけ更新する(選択枠/タグ/ジャケット等の状態は変えない)。</summary>
        public void SetLabel(string label)
        {
            if (_label != null) _label.text = label;
        }

        /// <summary>ジャケット sprite を設定(null ならフォールバックでラベルのみ)。</summary>
        public void SetJacket(Sprite sprite)
        {
            if (_jacket == null) return;
            if (sprite != null)
            {
                _jacket.sprite  = sprite;
                _jacket.enabled = true;
            }
            else
            {
                _jacket.enabled = false;
            }
        }

        /// <summary>選択ハイライトの ON/OFF。</summary>
        public void SetSelected(bool on)
        {
            if (_selectionFrame != null) _selectionFrame.enabled = on;
        }

        /// <summary>暗転オーバーレイの ON/OFF(BAN/非選択の表現)。</summary>
        public void SetDimmed(bool on)
        {
            if (_dim != null) _dim.enabled = on;
        }

        /// <summary>タグ(YOU/OPP/BANNED 等)を表示。</summary>
        public void SetTag(string text, Color color)
        {
            if (_tag == null) return;
            _tag.text    = text;
            _tag.color   = color;
            _tag.enabled = true;
        }

        /// <summary>タグを非表示にする。</summary>
        public void ClearTag()
        {
            if (_tag != null) _tag.enabled = false;
        }

        /// <summary>ボタンの押下可否。</summary>
        public void SetInteractable(bool on)
        {
            if (_button != null) _button.interactable = on;
        }

        /// <summary>タイルごと表示/非表示。</summary>
        public void SetVisible(bool on)
        {
            gameObject.SetActive(on);
        }
    }
}
