using System;
using System.IO;
using System.Threading.Tasks;
using RhythmGame.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmGame.UI.Pvp
{
    /// <summary>
    /// PVP 本戦の各曲前に表示する「難易度選択 & プレイ設定」画面 (フロー ⑦)。
    /// 1 試合 3 曲、各曲この画面 → GamePlay。
    ///
    ///   - 難易度: easy/normal/hard/extra から選択。存在しない譜面のボタンは無効化。
    ///     倍率 (easy0.75 / normal0.80 / hard0.90 / extra1.00) を併記。
    ///   - プレイ設定: ノートスピード / 判定オフセット / 表示オフセット。PlayerPrefs に永続化。
    ///   - 相手の難易度: フロー仕様では「見える」。現状サーバー(完全同期版)が各自の難易度を
    ///     公開する API を持たないため、ここでは "—" を表示し K のサーバー同期実装後に結線する。
    ///
    /// READY で <see cref="PvpFlowController.LaunchCurrentSongWith"/> を呼び、選んだ難易度/設定で
    /// GamePlay を起動する。UI は BuildPvpScenes が baked-in 結線し、未結線なら OnGUI フォールバック。
    /// </summary>
    public class PvpSongSetupController : MonoBehaviour
    {
        [Header("Optional UI (OnGUI fallback if header is null)")]
        [SerializeField] TextMeshProUGUI _headerText;     // "SONG 1 / 3"
        [SerializeField] TextMeshProUGUI _songTitleText;  // 曲名
        [SerializeField] Image           _jacket;
        [SerializeField] TextMeshProUGUI _difficultyLabel;// 選択中の難易度 + 倍率
        [SerializeField] TextMeshProUGUI _noteSpeedText;
        [SerializeField] TextMeshProUGUI _judgeOffsetText;
        [SerializeField] TextMeshProUGUI _visualOffsetText;
        [SerializeField] TextMeshProUGUI _oppDiffText;    // 相手の難易度 (同期待ち)
        [SerializeField] TextMeshProUGUI _statusText;

        [Header("Difficulty buttons (easy/normal/hard/extra order)")]
        [SerializeField] Button[] _difficultyButtons = new Button[0];

        [Header("Stepper buttons")]
        [SerializeField] Button _noteSpeedDown;
        [SerializeField] Button _noteSpeedUp;
        [SerializeField] Button _judgeOffsetDown;
        [SerializeField] Button _judgeOffsetUp;
        [SerializeField] Button _visualOffsetDown;
        [SerializeField] Button _visualOffsetUp;

        [SerializeField] Button _readyButton;
        [SerializeField] Button _cancelButton;

        static readonly string[] Difficulties = { "easy", "normal", "hard", "extra" };

        // 自分=シアン / 相手=レッド (他 PVP 画面と統一)
        static readonly Color Cyan   = new Color(0.17f, 0.85f, 0.90f, 1f);
        static readonly Color White  = Color.white;
        static readonly Color DimCol = new Color(1f, 1f, 1f, 0.35f);

        // PlayerPrefs キー (PVP 専用。グローバルなキャリブレーションを汚さない)
        const string KeyHiSpeed     = "HiSpeed";          // ソロと共有 (既存)
        const string KeyJudgeOffset = "PvpJudgeOffset";
        const string KeyVisualOffset= "PvpVisualOffset";

        bool   _alive;
        string _songId;
        int    _diffIndex = 3;        // 既定 extra
        bool[] _diffAvailable = { false, false, false, false };
        float  _hiSpeed      = 4.5f;
        int    _judgeOffset  = 0;
        int    _visualOffset = 0;

        readonly JacketLoader _jackets = new JacketLoader();

        PvpFlowController Pvp => PvpFlowController.Instance;

        void Start()
        {
            Application.runInBackground = true;
            JacketBackgroundController.Instance?.SetCanvasEnabled(true);
            JacketBackgroundController.Instance?.SetFallback();
            _alive = true;

            _hiSpeed      = PlayerPrefs.GetFloat(KeyHiSpeed, 4.5f);
            _judgeOffset  = PlayerPrefs.GetInt(KeyJudgeOffset, 0);
            _visualOffset = PlayerPrefs.GetInt(KeyVisualOffset, 0);

            WireButtons();

            if (_oppDiffText != null) _oppDiffText.text = "OPP   —";

            _ = InitAsync();
        }

        void OnDisable() => _alive = false;

        void Update()
        {
            if (RhythmGame.UI.Common.ConfirmDialog.IsOpen) return;

            var kb  = UnityEngine.InputSystem.Keyboard.current;
            var pad = UnityEngine.InputSystem.Gamepad.current;

            bool confirm = (kb != null && (kb.spaceKey.wasPressedThisFrame
                                        || kb.enterKey.wasPressedThisFrame
                                        || kb.numpadEnterKey.wasPressedThisFrame))
                        || (pad != null && RhythmGame.Input.GamepadLayout.ConfirmPressed(pad));
            bool back    = (kb != null && kb.escapeKey.wasPressedThisFrame)
                        || (pad != null && RhythmGame.Input.GamepadLayout.BackPressed(pad));

            if (back)    { RequestCancel(); return; }
            if (confirm) { OnReady();       return; }

            if (kb != null)
            {
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                bool ctrl  = kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed;
                bool alt    = kb.leftAltKey.isPressed   || kb.rightAltKey.isPressed;
                int  dir   = kb.rightArrowKey.wasPressedThisFrame ?  1
                           : kb.leftArrowKey.wasPressedThisFrame  ? -1 : 0;

                if (dir != 0)
                {
                    if (shift)      StepNoteSpeed(dir);     // Shift+←→: ノーツ速度
                    else if (ctrl)  StepJudgeOffset(dir);   // Ctrl+←→ : 判定オフセット
                    else if (alt)   StepVisualOffset(dir);  // Alt+←→  : 表示オフセット
                    else            StepDifficulty(dir);    // ←→      : 難易度
                }
            }
        }

        // ←→ で存在する難易度のみを循環して切り替える。
        void StepDifficulty(int dir)
        {
            int i = _diffIndex;
            for (int step = 0; step < Difficulties.Length; step++)
            {
                i = (i + dir + Difficulties.Length) % Difficulties.Length;
                if (_diffAvailable[i]) { OnDifficultyClicked(i); return; }
            }
        }

        void WireButtons()
        {
            if (_difficultyButtons != null)
            {
                for (int i = 0; i < _difficultyButtons.Length; i++)
                {
                    if (_difficultyButtons[i] == null) continue;
                    int idx = i;
                    _difficultyButtons[i].onClick.AddListener(() => OnDifficultyClicked(idx));
                }
            }
            if (_noteSpeedDown    != null) _noteSpeedDown.onClick.AddListener(() => StepNoteSpeed(-1));
            if (_noteSpeedUp      != null) _noteSpeedUp.onClick.AddListener(() => StepNoteSpeed(+1));
            if (_judgeOffsetDown  != null) _judgeOffsetDown.onClick.AddListener(() => StepJudgeOffset(-1));
            if (_judgeOffsetUp    != null) _judgeOffsetUp.onClick.AddListener(() => StepJudgeOffset(+1));
            if (_visualOffsetDown != null) _visualOffsetDown.onClick.AddListener(() => StepVisualOffset(-1));
            if (_visualOffsetUp   != null) _visualOffsetUp.onClick.AddListener(() => StepVisualOffset(+1));
            if (_readyButton      != null) _readyButton.onClick.AddListener(OnReady);
            if (_cancelButton     != null) _cancelButton.onClick.AddListener(RequestCancel);

            RhythmGame.UI.Common.ShortcutHintOverlay.Set(
                "←→: 難易度   Shift+←→: 速度   Ctrl+←→: 判定   Alt+←→: 表示   Space: READY   ESC: 辞退");
        }

        // 試合中の離脱は不戦敗。確認ダイアログを挟む。
        void RequestCancel()
        {
            var pvp = Pvp;
            if (pvp == null || !pvp.IsActive) { OnCancel(); return; }
            RhythmGame.UI.Common.ConfirmDialog.Show(
                "対戦を辞退しますか？（不戦敗扱い）", "辞退する", "もどる",
                onConfirm: OnCancel);
        }

        async Task InitAsync()
        {
            var pvp = Pvp;
            if (pvp == null || !pvp.IsActive || pvp.Songs == null || pvp.CurrentSongIndex >= pvp.Songs.Count)
            {
                SetStatus("(no active match)");
                if (_headerText != null) _headerText.text = "SONG SETUP";
                return;
            }

            int idx   = pvp.CurrentSongIndex;
            var song  = pvp.Songs[idx];
            _songId   = song.songId;

            if (_headerText != null) _headerText.text = $"SONG {idx + 1} / {pvp.Songs.Count}";

            // ドラフト確定難易度を初期選択に。未指定は extra。
            int drafted = IndexOfDifficulty(song.difficulty);
            _diffIndex  = drafted >= 0 ? drafted : 3;

            await DetectAvailableDifficultiesAsync();
            if (!_alive) return;

            // 選択中の難易度が存在しなければ、存在する最高難度へフォールバック。
            if (!_diffAvailable[_diffIndex])
            {
                for (int i = Difficulties.Length - 1; i >= 0; i--)
                    if (_diffAvailable[i]) { _diffIndex = i; break; }
            }

            RefreshDifficultyUi();
            RefreshSettingsUi();
            SetStatus("Choose difficulty & settings, then READY.");

            _ = LoadVisualsAsync();
        }

        async Task DetectAvailableDifficultiesAsync()
        {
            // 譜面ファイルの存在で判定 (譜面ロードはせず軽量に)。ChartLoader と同じ規約:
            // <base>/charts/<diff>.json (本体配置) または <base>/<diff>.json (ChartEditor 配置)。
            // base は OverrideBasePath があればそれ、無ければ StreamingAssets/Songs/<songId>。
            string baseDir = !string.IsNullOrEmpty(ChartLoader.OverrideBasePath)
                ? ChartLoader.OverrideBasePath
                : Path.Combine(Application.streamingAssetsPath, "Songs", _songId);

            for (int i = 0; i < Difficulties.Length; i++)
            {
                bool exists;
                try
                {
                    string nested = Path.Combine(baseDir, "charts", Difficulties[i] + ".json");
                    string flat   = Path.Combine(baseDir, Difficulties[i] + ".json");
                    exists = File.Exists(nested) || File.Exists(flat);
                }
                catch { exists = false; }
                _diffAvailable[i] = exists;
            }
            // 何も見つからなければ全部許可 (Android 等 File.Exists 不可環境のフォールバック)。
            bool any = false;
            foreach (var b in _diffAvailable) any |= b;
            if (!any) for (int i = 0; i < _diffAvailable.Length; i++) _diffAvailable[i] = true;
            await Task.CompletedTask;
        }

        async Task LoadVisualsAsync()
        {
            if (string.IsNullOrEmpty(_songId)) return;
            try
            {
                var meta = await ChartLoader.LoadMetaAsync(_songId);
                if (!_alive) return;
                if (_songTitleText != null)
                    _songTitleText.text = (meta != null && !string.IsNullOrEmpty(meta.Title)) ? meta.Title : _songId;
            }
            catch { if (_songTitleText != null) _songTitleText.text = _songId; }

            try
            {
                var tex = await _jackets.LoadAsync(_songId);
                if (!_alive || _jacket == null) return;
                if (tex != null)
                {
                    _jacket.sprite  = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    _jacket.enabled = true;
                }
                else _jacket.enabled = false;
            }
            catch { if (_jacket != null) _jacket.enabled = false; }
        }

        // ── 入力ハンドラ ───────────────────────────────────────────────
        void OnDifficultyClicked(int index)
        {
            if (index < 0 || index >= Difficulties.Length) return;
            if (!_diffAvailable[index]) return;   // 譜面なしは選べない
            _diffIndex = index;
            RefreshDifficultyUi();
        }

        void StepNoteSpeed(int dir)
        {
            _hiSpeed = Mathf.Clamp(Mathf.Round((_hiSpeed + dir * 0.5f) * 2f) / 2f, 1f, 10f);
            RefreshSettingsUi();
        }

        void StepJudgeOffset(int dir)
        {
            _judgeOffset = Mathf.Clamp(_judgeOffset + dir * 1, -100, 100);
            RefreshSettingsUi();
        }

        void StepVisualOffset(int dir)
        {
            _visualOffset = Mathf.Clamp(_visualOffset + dir * 1, -100, 100);
            RefreshSettingsUi();
        }

        void OnReady()
        {
            var pvp = Pvp;
            if (pvp == null || !pvp.IsActive) { SetStatus("(no active match)"); return; }
            if (!_diffAvailable[_diffIndex]) { SetStatus("Selected difficulty unavailable."); return; }

            PlayerPrefs.SetFloat(KeyHiSpeed, _hiSpeed);
            PlayerPrefs.SetInt(KeyJudgeOffset, _judgeOffset);
            PlayerPrefs.SetInt(KeyVisualOffset, _visualOffset);
            PlayerPrefs.Save();

            SetStatus("Starting...");
            SetInteractable(false);
            pvp.LaunchCurrentSongWith(Difficulties[_diffIndex], _hiSpeed, _judgeOffset, _visualOffset);
        }

        void OnCancel()
        {
            var pvp = Pvp;
            if (pvp != null && pvp.IsActive) pvp.CancelMatch();
            else SceneRouter.Instance?.GoTo(SceneId.Title);
        }

        // ── UI 反映 ────────────────────────────────────────────────────
        void RefreshDifficultyUi()
        {
            if (_difficultyButtons != null)
            {
                for (int i = 0; i < _difficultyButtons.Length && i < Difficulties.Length; i++)
                {
                    var b = _difficultyButtons[i];
                    if (b == null) continue;
                    b.interactable = _diffAvailable[i];
                    var img = b.targetGraphic as Image;
                    if (img != null)
                    {
                        if (!_diffAvailable[i]) img.color = new Color(1, 1, 1, 0.06f);
                        else img.color = (i == _diffIndex) ? Cyan : new Color(1, 1, 1, 0.15f);
                    }
                    var txt = b.GetComponentInChildren<TextMeshProUGUI>();
                    if (txt != null) txt.color = _diffAvailable[i] ? (i == _diffIndex ? Color.black : White) : DimCol;
                }
            }
            if (_difficultyLabel != null)
            {
                string d = Difficulties[_diffIndex];
                _difficultyLabel.text = $"{d.ToUpperInvariant()}   x{MultiplierFor(d):0.00}";
            }
        }

        void RefreshSettingsUi()
        {
            if (_noteSpeedText   != null) _noteSpeedText.text   = $"{_hiSpeed:0.0}";
            if (_judgeOffsetText != null) _judgeOffsetText.text = $"{_judgeOffset:+0;-0;0} ms";
            if (_visualOffsetText!= null) _visualOffsetText.text= $"{_visualOffset:+0;-0;0} ms";
        }

        void SetStatus(string s) { if (_statusText != null) _statusText.text = s; }

        void SetInteractable(bool on)
        {
            if (_readyButton != null) _readyButton.interactable = on;
            if (_difficultyButtons != null)
                foreach (var b in _difficultyButtons) if (b != null) b.interactable = on && true;
        }

        static int IndexOfDifficulty(string d)
        {
            if (string.IsNullOrEmpty(d)) return -1;
            string key = d.Trim().ToLowerInvariant();
            for (int i = 0; i < Difficulties.Length; i++) if (Difficulties[i] == key) return i;
            return -1;
        }

        static double MultiplierFor(string d)
        {
            switch (d)
            {
                case "easy":   return 0.75;
                case "normal": return 0.80;
                case "hard":   return 0.90;
                default:       return 1.00;
            }
        }

        // ── OnGUI フォールバック ───────────────────────────────────────
        void OnGUI()
        {
            if (_headerText != null) return;
            var pvp = Pvp;

            const float w = 560f, h = 420f;
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(r, "PVP SONG SETUP");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 32, r.width - 32, r.height - 44));

            if (pvp == null || !pvp.IsActive || pvp.Songs == null || pvp.CurrentSongIndex >= pvp.Songs.Count)
            {
                GUILayout.Label("(no active PVP match)");
                if (GUILayout.Button("BACK TO TITLE")) SceneRouter.Instance?.GoTo(SceneId.Title);
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"SONG {pvp.CurrentSongIndex + 1} / {pvp.Songs.Count}   {_songId}");
            GUILayout.Space(6);

            GUILayout.Label("Difficulty:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Difficulties.Length; i++)
            {
                GUI.enabled = _diffAvailable[i];
                string mark = (i == _diffIndex) ? "> " : "   ";
                if (GUILayout.Button(mark + Difficulties[i])) OnDifficultyClicked(i);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Note Speed: {_hiSpeed:0.0}", GUILayout.Width(180));
            if (GUILayout.Button("-")) StepNoteSpeed(-1);
            if (GUILayout.Button("+")) StepNoteSpeed(+1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Judge Offset: {_judgeOffset} ms", GUILayout.Width(180));
            if (GUILayout.Button("-")) StepJudgeOffset(-1);
            if (GUILayout.Button("+")) StepJudgeOffset(+1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Visual Offset: {_visualOffset} ms", GUILayout.Width(180));
            if (GUILayout.Button("-")) StepVisualOffset(-1);
            if (GUILayout.Button("+")) StepVisualOffset(+1);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button("READY")) OnReady();
            if (GUILayout.Button("CANCEL")) OnCancel();
            GUILayout.EndArea();
        }
    }
}
