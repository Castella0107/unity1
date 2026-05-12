using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Carousel-style Title menu.
// ← / → (A/D / Arrow keys) flips between menu items with a Y-axis card-flip animation.
// Enter / Space: decide.  Esc: cancel / exit.
public class TitleController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] RectTransform   _menuItemContainer;
    [SerializeField] TextMeshProUGUI _menuItemText;
    [SerializeField] TextMeshProUGUI _arrowLeft;
    [SerializeField] TextMeshProUGUI _arrowRight;

    [Header("Animation")]
    [SerializeField] float           _flipDuration = 0.35f;
    [SerializeField] AnimationCurve  _flipCurve    = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Input")]
    [SerializeField] InputActionAsset _inputAsset;

    // ── Menu items ─────────────────────────────────────────────────────────
    private enum MenuId { FreePlay, Online, Config, History, Exit }

    private static readonly (MenuId id, string label)[] _menus =
    {
        (MenuId.FreePlay, "FREE PLAY"),
        (MenuId.Online,   "ONLINE"),
        (MenuId.Config,   "CONFIG"),
        (MenuId.History,  "HISTORY"),
        (MenuId.Exit,     "EXIT"),
    };

    // ── State ──────────────────────────────────────────────────────────────
    private int  _currentIndex;
    private bool _isFlipping;

    // ── Input Actions ──────────────────────────────────────────────────────
    private InputAction _navigateAction;
    private InputAction _submitAction;
    private InputAction _cancelAction;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        Application.runInBackground = true;

        var map = _inputAsset.FindActionMap("UI", throwIfNotFound: true);
        _navigateAction = map.FindAction("Navigate", throwIfNotFound: true);
        _submitAction   = map.FindAction("Submit",   throwIfNotFound: true);
        _cancelAction   = map.FindAction("Cancel",   throwIfNotFound: true);
    }

    private void OnEnable()
    {
        _navigateAction.Enable();
        _submitAction  .Enable();
        _cancelAction  .Enable();
        _navigateAction.performed += OnNavigate;
        _submitAction  .performed += OnSubmit;
        _cancelAction  .performed += OnCancel;
    }

    private void OnDisable()
    {
        _navigateAction.performed -= OnNavigate;
        _submitAction  .performed -= OnSubmit;
        _cancelAction  .performed -= OnCancel;
        _navigateAction.Disable();
        _submitAction  .Disable();
        _cancelAction  .Disable();
    }

    private void Start()
    {
        JacketBackgroundController.Instance?.SetFallback();
        _currentIndex = 0;
        _menuItemText.text = _menus[_currentIndex].label;
        _menuItemContainer.localRotation = Quaternion.identity;
        StartCoroutine(PulseArrows());
    }

    // ── Input callbacks ────────────────────────────────────────────────────

    private void OnNavigate(InputAction.CallbackContext ctx)
    {
        if (_isFlipping) return;
        var v = ctx.ReadValue<Vector2>();
        if      (v.x >  0.5f) Flip(+1);
        else if (v.x < -0.5f) Flip(-1);
    }

    private void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (_isFlipping) return;
        Decide();
    }

    private void OnCancel(InputAction.CallbackContext ctx)
    {
        if (_isFlipping) return;
        ConfirmExit();
    }

    // ── Flip animation ─────────────────────────────────────────────────────

    private void Flip(int direction)
    {
        StartCoroutine(FlipRoutine(direction));
    }

    private IEnumerator FlipRoutine(int direction)
    {
        _isFlipping = true;
        float half = _flipDuration * 0.5f;

        // First half: rotate 0 → ±90°
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k     = _flipCurve.Evaluate(Mathf.Clamp01(t / half));
            float angle = direction > 0 ? Mathf.Lerp(0f, -90f, k) : Mathf.Lerp(0f, 90f, k);
            _menuItemContainer.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        // Swap text at the folded-away point
        _currentIndex = (_currentIndex + direction + _menus.Length) % _menus.Length;
        _menuItemText.text = _menus[_currentIndex].label;

        // Second half: rotate ∓90° → 0 (unfold from the other side)
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k     = _flipCurve.Evaluate(Mathf.Clamp01(t / half));
            float angle = direction > 0 ? Mathf.Lerp(90f, 0f, k) : Mathf.Lerp(-90f, 0f, k);
            _menuItemContainer.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }

        _menuItemContainer.localRotation = Quaternion.identity;
        _isFlipping = false;
    }

    // ── Decision ───────────────────────────────────────────────────────────

    private void Decide()
    {
        if (SceneRouter.Instance == null)
        {
            Debug.LogError("[TitleController] SceneRouter.Instance is null — Bootstrap not loaded?");
            return;
        }

        switch (_menus[_currentIndex].id)
        {
            case MenuId.FreePlay:
                SceneRouter.Instance.GoTo(SceneId.SongSelect);
                break;
            case MenuId.Online:
                Debug.Log("[Title] ONLINE — 未実装 (Phase 4-5)");
                break;
            case MenuId.Config:
                SceneRouter.Instance.GoTo(SceneId.Config);
                break;
            case MenuId.History:
                SceneRouter.Instance.GoTo(SceneId.History);
                break;
            case MenuId.Exit:
                ConfirmExit();
                break;
        }
    }

    private void ConfirmExit()
    {
        Debug.Log("[Title] EXIT");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Arrow pulse animation ──────────────────────────────────────────────

    private IEnumerator PulseArrows()
    {
        while (true)
        {
            float alpha = Mathf.Lerp(0.4f, 1.0f, (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f);
            Color c = _arrowLeft.color;
            c.a = alpha;
            _arrowLeft.color  = c;
            _arrowRight.color = c;
            yield return null;
        }
    }
}
