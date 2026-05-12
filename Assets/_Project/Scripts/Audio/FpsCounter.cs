using TMPro;
using UnityEngine;

// Attach to a Canvas in _Persistent scene (top-right overlay, default inactive).
// DisplayTabController.ApplyShowFps() activates/deactivates this GameObject.
public class FpsCounter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;

    float _accumulator;
    int   _frames;

    void Update()
    {
        _accumulator += Time.deltaTime;
        _frames++;

        if (_accumulator >= 0.5f)
        {
            if (_text != null)
                _text.text = (_frames / _accumulator).ToString("F0") + " fps";
            _accumulator = 0f;
            _frames      = 0;
        }
    }
}
