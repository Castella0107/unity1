using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// フルスクリーンのブラー加工されたジャケット背景を管理する永続シングルトン。
/// _Persistent.unity 内の GameObject にアタッチし、Canvas Sort Order -1000 で他の UI の背面に配置する。
/// </summary>
// Persistent singleton that manages the full-screen blurred jacket background.
/// <summary>
/// 楽曲ジャケットをぼかし背景として表示するシングルトン。_Persistent.unity に配置し、
/// Canvas Sort Order -1000 で全 UI の背後に描画する。
/// </summary>
public class JacketBackgroundController : MonoBehaviour
{
    /// <summary>シングルトンインスタンス。</summary>
    public static JacketBackgroundController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] RawImage _jacketImage;
    [SerializeField] Material _blurMaterial;

    [Header("Effects")]
    [SerializeField] float _zoomScale    = 1.2f;
    [SerializeField] float _fadeDuration = 0.5f;
    [SerializeField] float _brightness   = 0.5f;
    [SerializeField] float _blurSize     = 4f;

    [Header("Fallback")]
    [SerializeField] Color _fallbackColor = new Color(0.02f, 0.03f, 0.06f, 1f);

    JacketLoader _loader;
    string       _currentSongId;
    Coroutine    _fadeRoutine;
    Texture2D    _fallbackTexture;
    Canvas       _canvas;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _loader  = new JacketLoader();
        _canvas  = GetComponentInParent<Canvas>(true);
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        BuildFallbackTexture();
        ApplyMaterialSettings();

        if (_jacketImage != null)
        {
            _jacketImage.texture         = _fallbackTexture;
            _jacketImage.color           = Color.white;
            _jacketImage.transform.localScale = Vector3.one * _zoomScale;
        }
    }

    void BuildFallbackTexture()
    {
        _fallbackTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        _fallbackTexture.SetPixels(new Color[]
        {
            _fallbackColor, _fallbackColor,
            _fallbackColor, _fallbackColor,
        });
        _fallbackTexture.Apply();
    }

    void ApplyMaterialSettings()
    {
        if (_blurMaterial == null) return;
        _blurMaterial.SetFloat("_Brightness", _brightness);
        _blurMaterial.SetFloat("_BlurSize",   _blurSize);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_fallbackTexture != null) Destroy(_fallbackTexture);
        _loader?.ClearCache();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>指定楽曲のジャケットへ切り替える(null はフォールバック色)。同一 songId の連続呼び出しは無視。</summary>
    public async void SetJacket(string songId)
    {
        if (_currentSongId == songId) return;
        _currentSongId = songId;

        Texture2D newTex;
        if (string.IsNullOrEmpty(songId))
        {
            newTex = _fallbackTexture;
        }
        else
        {
            newTex = await _loader.LoadAsync(songId);
            if (newTex == null) newTex = _fallbackTexture;
        }

        // Guard: another SetJacket call overtook this one while we awaited
        if (_currentSongId != songId) return;

        if (_jacketImage == null) return;

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeToTexture(newTex));
    }

    /// <summary>フォールバック色に切り替える(Title/Config 等で使用)。</summary>
    public void SetFallback() => SetJacket(null);

    /// <summary>ジャケット Canvas の表示/非表示を切り替える(3D シーンではカメラを遮らないよう無効化)。</summary>
    public void SetCanvasEnabled(bool enabled)
    {
        if (_canvas != null) _canvas.enabled = enabled;
    }

    /// <summary>ぼかしの強さをランタイムで調整する(0〜10)。</summary>
    public void SetBlurSize(float size)
    {
        _blurSize = Mathf.Clamp(size, 0, 10);
        if (_blurMaterial != null) _blurMaterial.SetFloat("_BlurSize", _blurSize);
    }

    /// <summary>背景の明るさをランタイムで調整する(0〜1)。Config の背景エフェクトスライダーから呼ばれる。</summary>
    public void SetBrightness(float brightness)
    {
        _brightness = Mathf.Clamp01(brightness);
        if (_blurMaterial != null) _blurMaterial.SetFloat("_Brightness", _brightness);
    }

    // ── Fade coroutine ────────────────────────────────────────────────────────

    IEnumerator FadeToTexture(Texture2D target)
    {
        float half  = _fadeDuration * 0.5f;
        Color start = _jacketImage.color;
        Color clear = new Color(start.r, start.g, start.b, 0f);

        // Fade out
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _jacketImage.color = Color.Lerp(start, clear, t / half);
            yield return null;
        }

        _jacketImage.texture = target;

        // Fade in
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _jacketImage.color = Color.Lerp(clear, start, t / half);
            yield return null;
        }
        _jacketImage.color = start;
        _fadeRoutine = null;
    }
}
