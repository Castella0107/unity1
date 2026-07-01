using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// <see cref="ColorSquarePicker"/> の SV 四角 / 色相バー上のポインタ入力(押下・ドラッグ)を拾い、
/// 所有ピッカーへ転送するだけの薄いハンドラ。raycastTarget=true の(透明)Image と同じ
/// GameObject に載せる。<c>_isHue</c> で SV 四角(false) / 色相バー(true) を区別する。
/// </summary>
public class ColorPickerArea : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] ColorSquarePicker _owner;
    [SerializeField] bool _isHue;

    public void OnPointerDown(PointerEventData e) => Forward(e);
    public void OnDrag(PointerEventData e)        => Forward(e);

    void Forward(PointerEventData e)
    {
        if (_owner != null) _owner.OnAreaPointer(_isHue, e.position, e.pressEventCamera);
    }
}
