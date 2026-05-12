# トラブルシューティング集

Unity リズムゲームプロジェクト開発で発生した問題と対処を症状別に索引化。
Phase 1+2 のデバッグ経験から得た教訓。
「同じ症状で詰まった時」に5分で解決するためのリファレンス。

**最終更新: 2026-05-11 (Phase 4c 完成時点)**

関連ドキュメント:
- アーキテクチャ図: `docs/architecture/architecture.md`
- 開発者ハンドブック: `docs/handbook.md`

---

## 使い方

1. **症状から探す**: 章タイトルや項目名で「何が見えるか / 何が起きるか」から探す
2. **ログから探す**: Ctrl+F で Console エラーメッセージの一部を検索
3. **対処を実行**: 対処欄の手順に従う
4. **予防も読む**: 同じ問題の再発防止

---

## 索引

### 起動・遷移系
- [2.1 ▶ で起動すると SceneRouter / RepositoryService が null](#21-で起動すると-scenerouter--repositoryservice-が-null)
- [2.2 NullReferenceException at TitleController.Decide](#22-nullreferenceexception-at-titlecontrollerdecide)
- [2.3 シーンが2個ロードされる (scenes=3)](#23-シーンが2個ロードされる-scenes3)
- [2.4 GamePlay 遷移後 Title 画面が重なって見える](#24-gameplay-遷移後-title-画面が重なって見える)
- [2.5 FadeBlack が消えない (画面が黒のまま)](#25-fadeblack-が消えない-画面が黒のまま)

### GamePlay 系
- [3.1 レーンが見えない (3D空間が見えない)](#31-レーンが見えない-3d空間が見えない)
- [3.2 ノーツが流れてこない](#32-ノーツが流れてこない)
- [3.3 起動直後に PERFECT+ が画面上に表示される](#33-起動直後に-perfect-が画面上に表示される)
- [3.4 楽曲完走付近でクラッシュする (FormatException)](#34-楽曲完走付近でクラッシュする-formatexception)
- [3.5 Result 画面に遷移しない](#35-result-画面に遷移しない)

### UI レイアウト系
- [4.1 SCORE / COMBO 等が画面外にはみ出す](#41-score--combo-等が画面外にはみ出す)
- [4.2 判定文字が二重表示される](#42-判定文字が二重表示される)
- [4.3 大型コンボ表示が画面左下に切れる](#43-大型コンボ表示が画面左下に切れる)

### 音系
- [5.1 Audio not found / 楽曲ファイルが見つからない](#51-audio-not-found--楽曲ファイルが見つからない)
- [5.2 楽曲が無音](#52-楽曲が無音)
- [5.3 Hold 維持中の判定音がうるさい (連打音)](#53-hold-維持中の判定音がうるさい-連打音)
- [5.4 Volume スライダーが効かない](#54-volume-スライダーが効かない)

### JSON / データ系
- [6.1 "Failed to parse chart JSON" エラー](#61-failed-to-parse-chart-json-エラー)
- [6.2 ChartHash の FormatException](#62-charthash-の-formatexception)
- [6.3 SQLite WAL/SHM ファイルが消えない](#63-sqlite-walshm-ファイルが消えない)
- [6.4 PlayerPrefs の値が次回起動時に反映されない](#64-playerprefs-の値が次回起動時に反映されない)

### テスト系
- [7.1 EditMode テストが CS0246 でコンパイル失敗](#71-editmode-テストが-cs0246-でコンパイル失敗)
- [7.2 全 Perfect+ で 1,000,000 にならない](#72-全-perfect-で-1000000-にならない)

### ビルド・コンパイル系
- [8.1 Missing Script 警告 (? マーク)](#81-missing-script-警告--マーク)
- [8.2 NAudio / SQLite 参照解決失敗](#82-naudio--sqlite-参照解決失敗)

### Editor / MCP 系
- [9.1 RawImage.material が SerializedProperty で見つからない](#91-rawimagemat​erial-が-serializedproperty-で見つからない)
- [9.2 ParticleSystem.Burst.count への int 代入が無視される](#92-particlesystemburstcount-への-int-代入が無視される)
- [9.3 AudioMixer がプログラムから作成できない](#93-audiomixer-がプログラムから作成できない)

### パフォーマンス系
- [10.1 FPS が60を切る / ゲームプレイがカクつく](#101-fps-が60を切る--ゲームプレイがカクつく)
- [10.2 Material の値が Play 終了後も残る](#102-material-の値が-play-終了後も残る)

### その他警告
- [11.1 "There can be only one active Event System"](#111-there-can-be-only-one-active-event-system)
- [11.2 "There are 2 audio listeners in the scene"](#112-there-are-2-audio-listeners-in-the-scene)
- [11.3 Input System "Map must be contained in state"](#113-input-system-map-must-be-contained-in-state)
- [11.4 "Can't remove AudioListener because AudioListenerGuard depends on it"](#114-cant-remove-audiolistener-because-audiolistenerguard-depends-on-it)
- [11.5 TMP フォント警告 (日本語文字が □ で表示)](#115-tmp-フォント警告-日本語文字が--で表示)

### Phase 4 教訓 (IInputSource / StageInitializer)
- [12.1 IInputSource 導入時の SerializeField 配線忘れ](#121-iinputsource-導入時の-serializefield-配線忘れ)
- [12.2 Domain 層の namespace と Unity 依存型の混在](#122-domain-層の-namespace-と-unity-依存型の混在)
- [12.3 モード固有の初期化が共通モードにコピーされていない (3D が真っ黒)](#123-モード固有の初期化が共通モードにコピーされていない-3d-が真っ黒)
- [12.4 シーン GameObject 追加とスクリプト実装の乖離 (Replay ボタン)](#124-シーン-gameobject-追加とスクリプト実装の乖離-replay-ボタン)
- [12.5 フォント Atlas に存在しない Unicode 文字 (▶/❚❚ が □)](#125-フォント-atlas-に存在しない-unicode-文字-▶❚❚-が-)

---

## 2. 起動・遷移系

---

### 2.1 ▶ で起動すると SceneRouter / RepositoryService が null

**症状**

Unity Editor で ▶ ボタンを押すと現在開いているシーン (Title 等) が直接起動。
_Persistent や Bootstrap がロードされず、SceneRouter.Instance 等が null。

**ログ**
```
NullReferenceException: Object reference not set to an instance of an object
TitleController.Decide () (at Assets/_Project/Scripts/UI/Title/TitleController.cs:160)
```
または
```
[TitleController] SceneRouter.Instance is null — Bootstrap not loaded?
```

**原因**

Editor の ▶ は「現在開いているシーン」を直接起動する。
本番ビルドは Build Settings index 0 (Bootstrap) から始まるが、
Editor 開発時はその挙動にならない。

**対処**

`SceneAutoLoader.cs` (Editor 拡張) が対処済み:
1. `Tools > Scene AutoLoader` メニューで ON (デフォルト ON) を確認
2. どのシーンを開いていても ▶ を押すと自動的に Bootstrap.unity で起動

SceneAutoLoader が無い場合は Editor で手動で Bootstrap.unity を開いてから ▶。

**予防**

- SceneAutoLoader を Editor フォルダに配置 (Assets/_Project/Editor/SceneAutoLoader.cs)
- 各 Controller の null チェック (TitleController.Decide に実装済み)

**関連**: [2.2 TitleController.Decide NRE](#22-nullreferenceexception-at-titlecontrollerdecide)

---

### 2.2 NullReferenceException at TitleController.Decide

**症状**

Title 画面で Enter を押すと SongSelect に遷移せず NullReferenceException が出る。

**ログ**
```
NullReferenceException: Object reference not set to an instance of an object
TitleController.Decide () (at Assets/_Project/Scripts/UI/Title/TitleController.cs:160)
NullReferenceException while executing 'performed' callbacks of 'UI/Submit'
```

**原因**

`Decide()` が `SceneRouter.Instance.GoTo(...)` を呼ぼうとして Instance が null。
Bootstrap/\_Persistent がロードされていない (2.1 と同じ根本原因)。

**対処**

`TitleController.Decide()` の冒頭に防御処理が実装済み:
```csharp
if (SceneRouter.Instance == null) {
    Debug.LogError("[TitleController] SceneRouter.Instance is null — Bootstrap not loaded?");
    return;
}
```
それでも発生するなら SceneAutoLoader が OFF になっている (2.1)。

**予防**

全 Controller で `?.` 演算子 または null チェックを使う。

**関連**: [2.1 SceneAutoLoader](#21-で起動すると-scenerouter--repositoryservice-が-null)

---

### 2.3 シーンが2個ロードされる (scenes=3)

**症状**

SceneRouter のログで同名シーンが2個。派生症状として:
- 3Dレーンが見えない
- Title の UI が GamePlay に重なる

**ログ**
```
[SceneRouter] After GoTo GamePlay | scenes=3 [_Persistent, GamePlay*, GamePlay]
```

**原因**

SceneRouter の旧実装が「新シーンを Load してから旧シーンを Unload」する順序だった。
`GetSceneByName("GamePlay")` が古い方を返し、新シーンが SetActive にならない。
さらに Unload ループが `s.name == targetLabel` で両方をスキップして旧シーンが残る。

**対処**

SceneRouter.GoToRoutine に **Pre-evict ステップ**を追加済み:
```csharp
// Load より前に既存の同名シーンを Unload
for (int i = SceneManager.sceneCount - 1; i >= 0; i--) {
    var s = SceneManager.GetSceneAt(i);
    if (s.name == targetLabel && s.isLoaded) {
        var evict = SceneManager.UnloadSceneAsync(s);
        while (evict != null && !evict.isDone) yield return null;
    }
}
// その後 LoadSceneAsync
```

**予防**

シーン遷移は必ず「旧シーンを先に全 Unload → 新シーンを Load」の順序。

**関連**: [2.4 シーン重なり](#24-gameplay-遷移後-title-画面が重なって見える), [3.1 レーン消失](#31-レーンが見えない-3d空間が見えない)

---

### 2.4 GamePlay 遷移後 Title 画面が重なって見える

**症状**

GamePlay に遷移したはずなのに Title の UI が透けて見える。
または 3D レーンが見えない。

**原因**

2つの可能性:
1. **シーン Unload 漏れ** (2.3): Title がまだロードされたまま
2. **JacketBackgroundCanvas が 3D 空間を覆っている** (3.1)

**対処**

1. まず 2.3 の Unload 順序を確認・修正
2. 改善しない場合は 3.1 (SetCanvasEnabled) も確認

**関連**: [2.3 scenes=3](#23-シーンが2個ロードされる-scenes3), [3.1 レーン消失](#31-レーンが見えない-3d空間が見えない)

---

### 2.5 FadeBlack が消えない (画面が黒のまま)

**症状**

シーン遷移後、画面が真っ黒のまま。FadeOut コルーチンが完了しない。

**原因**

SceneRouter.GoToRoutine 内で例外が発生し FadeOut まで到達しなかった。
または `_isTransitioning = true` のまま止まっている。

**対処**

GoToRoutine に try-finally を追加してリカバリー:
```csharp
try {
    // 遷移処理 ...
} catch (Exception e) {
    Debug.LogError("[SceneRouter] GoTo failed: " + e);
} finally {
    if (_transitionFx != null)
        yield return _transitionFx.FadeIn(TransitionStyle.FadeBlack);
    _isTransitioning = false;
}
```

**予防**

例外時のリカバリーを必ず実装する。黒画面は致命的。

---

## 3. GamePlay 系

---

### 3.1 レーンが見えない (3D空間が見えない)

**症状**

GamePlay でレーンが見えない。HUD は表示される。画面がジャケット画像の全面表示または黒。

**ログ**

特になし (視覚的な問題)。

**原因**

`JacketBackgroundCanvas` (Sort Order -1000) が GamePlay 中も有効のまま。
3D 空間の描画が Canvas の後ろに隠れる。

**対処**

`GamePlayController.Start()` で Canvas を無効化:
```csharp
JacketBackgroundController.Instance?.SetCanvasEnabled(false);
```

`TriggerResultAsync()` で Result 遷移前に再有効化:
```csharp
JacketBackgroundController.Instance?.SetCanvasEnabled(true);
JacketBackgroundController.Instance?.SetJacket(SongId);
```

**予防**

3D 空間と Canvas Overlay を組み合わせる場合、Canvas の Sort Order 設定だけでは不十分なことがある。Canvas Enable/Disable で明示的に制御する。

**関連**: [2.4 シーン重なり](#24-gameplay-遷移後-title-画面が重なって見える)

---

### 3.2 ノーツが流れてこない

**症状**

GamePlay でノーツが表示されない。スコアもコンボも 0 のまま。Console は静か。

**原因 (複数)**

1. `ChartLoader.LoadChartAsync` で例外 (chart.json が無い / JSON 不正)
2. `AudioConductor.StartSong` が呼ばれていない (楽曲ロード失敗)
3. `async void Start` で例外が握り潰されている
4. Hierarchy 上の NoteScroller が Inspector 未配線

**対処**

`GamePlayController.Start` の try-catch でステージ毎にログを仕込む:
```csharp
Debug.Log("[GamePlay] Stage 1: LoadMetaAsync");
_meta = await ChartLoader.LoadMetaAsync(SongId);

Debug.Log("[GamePlay] Stage 2: LoadChartAsync");
_chart = await ChartLoader.LoadChartAsync(SongId, Difficulty);

// ... 段階的に確認
Debug.Log("[GamePlay] Stage 6: NoteScroller.Initialize");
_scroller.Initialize(_chart);
```

**予防**

`async void Start` は必ず try-catch でラップ。サイレント例外は発見が遅れる。

**関連**: [6.1 chart JSON parse](#61-failed-to-parse-chart-json-エラー)

---

### 3.3 起動直後に PERFECT+ が画面上に表示される

**症状**

GamePlay 入場直後 (ノーツを叩く前) に PERFECT+ 等の大きな判定文字が表示される。

**ログ**
```
Coroutine couldn't be started because the the game object 'JudgmentTextPopup' is inactive!
```

**原因**

`JudgmentTextPopup` が Inspector 上で初期テキスト "PERFECT+" を持ったまま Active だった。
さらに `Show()` 内で `StartCoroutine` の前に `gameObject.SetActive(false)` が来ていた古い実装でコルーチンが失敗。

**対処 (実装済み)**

1. `JudgmentTextPopup.Awake()` で `gameObject.SetActive(false)` を強制実行
2. `Show()` 内で `gameObject.SetActive(true)` → その後 `StartCoroutine()` の順序を守る
3. `JudgmentEffectsController.HandleJudged()` から `_textPopup.Show(j)` の呼び出しを削除
   → `JudgmentDisplay.cs` が中央に白フェードアウトで判定を表示する方式に統一

**予防**

Coroutine を使う非表示/表示トグルは「Active にしてから StartCoroutine」の順序を守る。

**関連**: [4.2 判定文字二重](#42-判定文字が二重表示される)

---

### 3.4 楽曲完走付近でクラッシュする (FormatException)

**症状**

楽曲完走付近 (約23秒後などサイン波クリップ終了タイミング) に Editor がクラッシュ or 例外。

**ログ**
```
FormatException: Could not find any recognizable digits.
System.ParseNumbers.StringToInt (System.String s, ...)
System.Convert.ToByte (System.String value, System.Int32 fromBase)
GamePlayController.HexStringToBytes () (at ...GamePlayController.cs:254)
GamePlayController+<TriggerResultAsync>d__... (at ...GamePlayController.cs:157)
```

**原因**

`TriggerResultAsync()` が `_chart.ChartHash` を `HexStringToBytes()` で変換しようとするが、
chart.json の `"chartHash"` フィールドが `"abc123test"` など非 hex 文字列になっている。
`"te"` `"st"` などは `Convert.ToByte(..., 16)` に渡せない。

**対処 (実装済み)**

**Fix 1**: `GamePlayController.HexStringToBytes` に防御処理:
```csharp
static byte[] HexStringToBytes(string hex) {
    var bytes = new byte[32];
    if (string.IsNullOrEmpty(hex)) return bytes;
    if (hex.Length % 2 != 0 || !IsValidHex(hex)) {
        Debug.LogWarning("[GamePlay] ChartHash '" + hex + "' is not valid hex — using zero bytes");
        return bytes;
    }
    // 通常変換
}
```

**Fix 2**: `ChartParser.ParseChart` で自動 SHA-256 フォールバック:
```csharp
if (!IsValidHexHash(dto.ChartHash))
    chartHash = ComputeSha256Hex(json);  // JSON本文からSHA-256を計算
```

**Fix 3**: chart.json を正しい 64文字 hex に修正:
```json
"chartHash": "0000000000000000000000000000000000000000000000000000000000000001"
```

**予防**

外部 JSON の内容を無条件に信頼しない。ChartParser に防御層を持つ。

**関連**: [6.2 ChartHash FormatException](#62-charthash-の-formatexception)

---

### 3.5 Result 画面に遷移しない

**症状**

楽曲完走後に Result 画面が表示されない。GamePlay のままフリーズ。

**原因**

1. 完走検知 (`SongTimeMs >= DurationMs + 1000`) が動いていない (DurationMs が 0)
2. `TriggerResultAsync` 内で例外 (3.4 等) が発生して GoTo まで到達しない
3. `SceneRouter.Instance` が null
4. Result.unity が Build Settings に登録されていない

**対処**

`TriggerResultAsync` を try-catch で囲み段階的にログ出力:
```csharp
async void TriggerResultAsync() {
    try {
        Debug.Log("[GamePlay] TriggerResult: Step 1 - Stop conductor");
        // ...
        Debug.Log("[GamePlay] TriggerResult: Step 5 - GoTo Result");
        SceneRouter.Instance.GoTo(SceneId.Result, resultParams);
    } catch (Exception e) {
        Debug.LogError("[GamePlay] TriggerResultAsync failed: " + e.Message + "\n" + e.StackTrace);
    }
}
```

**予防**

重要な遷移処理は段階ログで状態を可視化する。

---

## 4. UI レイアウト系

---

### 4.1 SCORE / COMBO 等が画面外にはみ出す

**症状**

"SCORE" テキストの "S" が画面左外に切れる。または "COMBO" 数字が右外に出る。

**原因**

RectTransform の Anchor / Pivot の不整合。
例: Pivot が (0.5, 0.5) のまま Anchor を左下に設定すると、要素の中心が左下基準になり
  半分が画面外に出る。

または TextMeshProUGUI の TextWrappingMode が有効で折り返した2行目が画面外に出る。

**対処**

位置の意図に合わせた設定:

| 配置 | AnchorMin | AnchorMax | Pivot | AnchoredPosition |
|---|---|---|---|---|
| 左下固定 | (0, 0) | (0, 0) | (0, 0) | (40, 30) |
| 中央固定 | (0.5, 0.5) | (0.5, 0.5) | (0.5, 0.5) | (0, 100) |
| 右下固定 | (1, 0) | (1, 0) | (1, 0) | (-40, 30) |

TextMeshProUGUI:
```
TextWrappingMode: NoWrap (0)
OverflowMode:    Ellipsis (3)
```

**予防**

1920×1080 以外の解像度 (1280×720, 自由アスペクト) でも確認する。

---

### 4.2 判定文字が二重表示される

**症状**

画面上部に大きな "MISS" (赤)、画面中央に小さな "MISS / LATE" (白) の2箇所表示。

**原因**

`JudgmentTextPopup` と `JudgmentDisplay` の両方が `OnJudged` を受けて表示していた。

**対処 (実装済み)**

`JudgmentEffectsController.HandleJudged()` から `_textPopup.Show(j)` を削除:
```csharp
void HandleJudged(Judgment j, double deltaMs) {
    // JudgmentTextPopup disabled — JudgmentDisplay handles text display.
    // (削除: if (_textPopup != null) _textPopup.Show(j);)
    ...
}
```
`JudgmentDisplay.cs` が画面中央にフェードアウト表示する方式に統一。

**予防**

判定演出のパイプラインは1系統に絞る。複数コンポーネントが同じイベントを購読して表示処理をする場合は役割分担を明確にする。

**関連**: [3.3 起動時 PERFECT+](#33-起動直後に-perfect-が画面上に表示される)

---

### 4.3 大型コンボ表示が画面左下に切れる

**症状**

コンボ数字 ("59" 等) が画面左半分にはみ出して切れている。Scale が異常に大きい。

**原因**

`ComboDisplay` GameObject が Canvas の子として通常の `Transform` (RectTransform でない) を持ち、
`LocalPosition = (-1184, -437, 0)`、`LocalScale = (3.24, 3.24, 3.24)` という異常値になっていた。
エディタで Canvas 子 GameObject を作成した際に座標系が不整合だったと推定。

**対処**

GamePlay.unity の ComboDisplay Transform を直接修正:
```yaml
m_LocalPosition: {x: 960, y: 670, z: 0}  # Canvas中央水平、判定テキストより100px上
m_LocalScale:    {x: 1, y: 1, z: 1}
```

ComboLabel (COMBO文字):
```yaml
m_AnchoredPosition: {x: 0, y: 50}  # 数字の50px上
```

ComboText (数字):
```yaml
m_AnchoredPosition: {x: 0, y: -10}  # 中心より10px下
```

**予防**

Canvas 子 GameObject の Transform を変更した場合は必ず LocalPosition と LocalScale を確認する。
特に非 RectTransform の GO を Canvas 子に置く場合は Canvas ローカル座標系を意識する。

---

## 5. 音系

---

### 5.1 Audio not found / 楽曲ファイルが見つからない

**症状**

GamePlay で音が出ない (無音30秒クリップにフォールバック)。

**ログ**
```
[GamePlay] Audio not found: [ChartLoader] No loadable audio found for songId='test_song'. 
Tried audio.ogg, audio.mp3, audio.wav in Songs/test_song/ → using 30-second silent clip
```

**原因**

`StreamingAssets/Songs/{songId}/` に `audio.ogg` / `audio.mp3` / `audio.wav` のいずれも存在しない。
または UTF-8 BOM 付き JSON などによりルートパスの解決が失敗 (稀)。

**対処**

サンプル楽曲を生成:
```
Unity Editor メニュー: Tools > Generate Test Audio
```
→ `StreamingAssets/Songs/test_song/audio.wav` (65秒サイン波) が生成。

または実際の音源ファイルを配置:
```
StreamingAssets/Songs/my_song/audio.ogg
```

ChartLoader は `.ogg` → `.mp3` → `.wav` の順に試行する。

**予防**

GamePlayController.Start でオーディオロード失敗時のフォールバック処理が実装済み:
無音クリップで継続し「Audio not found」警告ログのみ出す。

---

### 5.2 楽曲が無音 (ファイルはある)

**症状**

楽曲ファイルは存在するのに Play しても音が出ない。

**原因**

1. AudioMixer 未セットアップで Volume が 0 になっている
2. PlayerPrefs の保存値が `MasterVolumeDb = -80` 等の無音値
3. AudioListener が存在しない

**対処**

```csharp
// PlayerPrefs リセット (一時的なデバッグ用)
PlayerPrefs.DeleteAll();
PlayerPrefs.Save();
```

AudioMixer 未設定の場合は handbook.md の「2.3 AudioMixer 手動セットアップ」を参照。
AudioListener は `_Persistent.unity` の SceneRouter に1個配置されていることを確認。

**予防**

起動時のデフォルト Volume を 80% 程度に設定しておく。
AudioMixer 未割当でも `AudioSource.volume` 直接制御でフォールバック動作するよう設計。

---

### 5.3 Hold 維持中の判定音がうるさい (連打音)

**症状**

Hold ノーツを押し続けると判定音がとても速い間隔で連打される。

**原因**

`HoldJudgmentTracker` が Hold 維持中に毎拍 PerfectPlus tick を `OnJudged` で発火する。
`JudgmentEffectsController` がそのたびに判定音を再生する設計。

**対処 (実装済み)**

`JudgmentEffectsController` に時間間隔ベースのスロットリング:
```csharp
const double MIN_SOUND_INTERVAL_MS = 32.0;  // prevents Hold-tick spam

if (nowMs - _lastJudgmentSoundMs >= MIN_SOUND_INTERVAL_MS) {
    HitSoundPlayer.Instance?.PlayJudgment(j);
    _lastJudgmentSoundMs = nowMs;
}
```

**予防**

Hold tick を通常判定と区別する専用イベントを設計段階で決める (Phase 3 でリファクタ予定)。

---

### 5.4 Volume スライダーが効かない

**症状**

Config Audio タブで Master / Music / SFX スライダーを動かしても音量が変わらない。

**原因**

`AudioVolumeBinder._mainMixer` が未割当 (AudioMixer 未セットアップ) の場合、
`AudioMixer.SetFloat("MasterVolumeDb", value)` が機能しない。

**対処**

AudioMixer 手動セットアップ (handbook.md 2.3) を実施してアサインする。
未割当時は `HitSoundPlayer` が `AudioSource.volume` を直接制御するフォールバック動作で
SFX のみ制御可能。

---

## 6. JSON / データ系

---

### 6.1 "Failed to parse chart JSON" エラー

**症状**

GamePlay 起動時に chart.json のパースが失敗してノーツが表示されない。

**ログ**
```
[GamePlay] Start failed: Failed to parse chart JSON: Unexpected character encountered while parsing value: . Path '', line 1, position 1. | first 200 chars: {...}
```

**原因**

**最頻出: UTF-8 BOM (Byte Order Mark)** が JSON ファイル先頭に付いている。

PowerShell 5.1 の `-Encoding utf8` で書き出すと BOM 付き UTF-8 になる。
Newtonsoft.Json がファイル先頭の `﻿` 文字を不正 JSON と判断して `JsonException`。

```powershell
# ❌ BOM付きで書き出す (PS 5.1のデフォルト)
Set-Content 'file.json' $content -Encoding utf8

# ✅ BOMなしで書き出す
$enc = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText('file.json', $content, $enc)
```

**対処**

1. **既存ファイルの BOM 除去**:
```powershell
$enc = New-Object System.Text.UTF8Encoding $false
$files = Get-ChildItem 'StreamingAssets' -Recurse -Filter '*.json'
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $content = [System.IO.File]::ReadAllText($f.FullName).TrimStart([char]0xFEFF)
        [System.IO.File]::WriteAllText($f.FullName, $content, $enc)
    }
}
```

2. **ChartParser に防御層** (実装済み):
```csharp
// ParseMeta / ParseChart の先頭で BOM を除去
if (json != null && json.Length > 0 && (int)json[0] == 0xFEFF)
    json = json.Substring(1);
```

**予防**

JSON ファイルを PowerShell で書き出す場合は必ず `New-Object System.Text.UTF8Encoding $false` を使う。

---

### 6.2 ChartHash の FormatException

**症状**

楽曲完走時に `Convert.ToByte` で FormatException (3.4 参照)。

**原因**

`chart.json` の `"chartHash"` フィールドが `"abc123test"` `"ts1ex"` 等の非 hex 文字列。
`"te"` を `Convert.ToByte(..., 16)` に渡せない。

**対処**

chart.json の chartHash を有効な 64文字 hex に変更:
```json
"chartHash": "0000000000000000000000000000000000000000000000000000000000000001"
```

または省略して ChartParser の自動 SHA-256 計算に任せる:
```json
{
  "version": 1,
  "songId": "my_song"
  // "chartHash" フィールドなし → ChartParser が JSON 本文から SHA-256 を計算
}
```

`HexStringToBytes` の防御処理 (実装済み):
- 非 hex 文字を検出 → warning ログ + 32 バイトゼロを返す (クラッシュしない)

**関連**: [3.4 楽曲完走クラッシュ](#34-楽曲完走付近でクラッシュする-formatexception)

---

### 6.3 SQLite WAL/SHM ファイルが消えない

**症状**

テスト後に `%TEMP%` 以下に `.db-wal` `.db-shm` ファイルが残る。次回テストで DB ロックエラー。

**原因**

SQLite WAL モードでは `.db` 本体のほかに `-wal` / `-shm` 補助ファイルが生成される。
Repository を `Dispose` せずに終了するとロックが残る。

**対処**

`TempSqliteDb.Dispose()` で3ファイルを全て削除 (実装済み):
```csharp
public void Dispose() {
    foreach (var ext in new[] { "", "-wal", "-shm" })
        File.Delete(FilePath + ext);
}
```

テストは必ず `[TearDown]` で `_db.Dispose()` を呼ぶ。

**予防**

`using var db = new TempSqliteDb()` でスコープを明示する。

---

### 6.4 PlayerPrefs の値が次回起動時に反映されない

**症状**

Config で値を変更したのに次回起動時に元に戻っている。

**原因**

`PlayerPrefs.Set*` の後に `PlayerPrefs.Save()` を呼んでいない。
または読み込み側と保存側でキー名が不一致 (typo)。

**対処**

```csharp
PlayerPrefs.SetFloat("Audio_MasterVolume", value);
PlayerPrefs.Save();  // ← 必須。なければ終了時に破棄されることがある
```

キー名は定数化して typo を防ぐ:
```csharp
const string KEY_VOL_MASTER = "Audio_MasterVolume";
```

---

## 7. テスト系

---

### 7.1 EditMode テストが CS0246 でコンパイル失敗

**症状**

Test Runner でコンパイルエラー。SQLite 等の型が見つからない。

**ログ**
```
error CS0246: The type or namespace name 'SQLite' could not be found
```

**原因**

`SQLITE_NET_PCL` または `NAUDIO` のスクリプト定義シンボルが未設定。

**対処**

```
Project Settings > Player > Scripting Define Symbols
```
に以下を追加:
- `SQLITE_NET_PCL` (SQLite)
- `NAUDIO` (NAudio、未使用環境では不要)

または `asmdef` の References に不足しているアセンブリを追加。

---

### 7.2 全 Perfect+ で 1,000,000 にならない

**症状**

`ReplayBuilder.AllPerfectPlus(chart)` で全 PerfectPlus にしても `CurrentScore` が 1,000,000 ぴったりにならない。

**原因**

`TotalNotes` の計算が ChartBuilder / ChartParser / JudgmentRunner で不一致。
スコアの分母 (`TotalNotes`) が「ノーツ数」になっていて Hold の tick が含まれていない。

**対処**

3箇所全てで `ScoringEventCounter.Count(notes, bpmTimeline)` を使用 (実装済み):
- `ChartBuilder.ComputeTotalScoringEvents()`
- `ChartParser.ParseChart()` 内
- `JudgmentRunner.Run()` 内

独自に `TotalNotes = notes.Count` と書くと Hold tick 分が抜けてズレる。

**予防**

`ScoringEventCounter.Count` を共通ヘルパーとして使い、独自実装しない。

---

## 8. ビルド・コンパイル系

---

### 8.1 Missing Script 警告 (? マーク)

**症状**

Hierarchy で GameObject に黄色の "?" マーク。Inspector に "Missing Script"。

**ログ**
```
The referenced script (Unknown) on this Behaviour is missing!
```

**原因**

スクリプトファイルが削除・リネームされ、シーンが古い GUID を保持している。

**対処**

1. 該当 GameObject を選択
2. Inspector で "?" の行を右クリック → Remove Component
3. シーン保存

または `manage_scene(action="validate", auto_repair=True)` (MCP 経由で自動修正)。

**予防**

スクリプトのリネームは Unity Editor の Project ウィンドウで行う (GUID が保持される)。
ファイルシステムで直接リネームすると GUID が失われてシーン参照が壊れる。

---

### 8.2 NAudio / SQLite 参照解決失敗

**症状**

コンパイルエラー: `The type or namespace 'NAudio' (or 'SQLite') could not be found`

**対処**

| ライブラリ | 対処 |
|---|---|
| SQLite | NuGetForUnity で `sqlite-net-pcl` インストール + `SQLITE_NET_PCL` シンボル追加 |
| NAudio | NuGetForUnity で `NAudio` インストール + `NAUDIO` シンボル追加 |

NAudio 未インストール環境向けに関連コードを `#if NAUDIO` で囲む:
```csharp
#if NAUDIO
using NAudio.CoreAudioApi;
#endif
```

---

## 9. Editor / MCP 系

---

### 9.1 RawImage.material が SerializedProperty で見つからない

**症状**

MCP の `manage_components(set_property, property="material")` が失敗。

**原因**

Unity の SerializedProperty 内部名は `m_Material` であり `material` ではない。

**対処**

```python
manage_components(
    action="set_property",
    target=go_id,
    component_type="RawImage",
    property="m_Material",  # ← m_ プレフィックスが必要
    value={"path": "Assets/My/Material.mat"}
)
```

**予防**

迷ったら Unity Editor を Debug モードで開いて SerializedProperty 名を確認する。
一般則: シリアライズフィールドは `m_FieldName` 形式が多い。

---

### 9.2 ParticleSystem.Burst.count への int 代入が無視される

**症状**

`emission.GetBurst(0).count = 18` のような代入が反映されない。

**原因**

`ParticleSystem.Burst` は構造体。`GetBurst` はコピーを返すため直接代入が無効。
`count` は `MinMaxCurve` 型であり `int` を直接代入できない。

**対処**

```csharp
var burst = emission.GetBurst(0);
burst.count = new ParticleSystem.MinMaxCurve(18);  // MinMaxCurve でラップ
emission.SetBurst(0, burst);                        // 書き戻す
```

---

### 9.3 AudioMixer がプログラムから作成できない

**症状**

`ScriptableObject.CreateInstance<AudioMixerController>()` で例外。AudioMixer の自動作成が失敗。

**原因**

`UnityEditor.Audio.AudioMixerController` は `ExtensionOfNativeClass` 属性を持つネイティブクラス。
`ScriptableObject.CreateInstance` では作成できない。

**対処**

手動で作成: `Assets > Create > Audio > Audio Mixer` メニュー。
詳細は handbook.md「2.3 AudioMixer 手動セットアップ」を参照。

AudioMixer 未割当でも AudioSource.volume 直接制御でフォールバック動作するよう設計済み。

---

## 10. パフォーマンス系

---

### 10.1 FPS が60を切る / ゲームプレイがカクつく

**症状**

GamePlay で FPS が不安定。ノーツのコマ落ち。

**主な原因と対処**

| 原因 | 対処 |
|---|---|
| ノーツの毎フレーム Instantiate/Destroy | NotePool を使用 (実装済み) |
| パーティクルのプール枯渇で Expand ループ | JudgmentParticlePool のサイズを増やす |
| Update 内の SQLite 書き込み | TriggerResultAsync (完走時のみ) で非同期書き込み |
| BeatGrid / JacketBlur のシェーダーが重い | `_GridDensity` / `_BlurSize` を縮小 |

Profiler で `CPU Usage` を確認して最大消費処理を特定する。

---

### 10.2 Material の値が Play 終了後も残る

**症状**

Play モード終了後も Editor 上で Material アセットの `_PulseIntensity` 等が変わったまま。

**原因**

`Material.SetFloat` はアセット直接変更。Play 中に変更した値がアセットに書き込まれる。

**対処**

`OnDestroy` (または `Unbind`) で初期値に戻す (実装済み):
```csharp
void OnDestroy() {
    if (_gridMaterial != null) {
        _gridMaterial.SetFloat("_PulseIntensity", 1.0f);
        _gridMaterial.SetFloat("_GridScale", 1.0f);
    }
}
```

**予防**

`MaterialPropertyBlock` を使えばアセット直接変更を避けられる:
```csharp
var block = new MaterialPropertyBlock();
_renderer.GetPropertyBlock(block);
block.SetFloat("_PulseIntensity", value);
_renderer.SetPropertyBlock(block);
```

---

## 11. その他の警告

---

### 11.1 "There can be only one active Event System"

**症状**
```
There can be only one active Event System.
There are 2 event systems in the scene.
```

**原因**

各シーンに EventSystem GameObject があり、additive load の間に2つが共存する。

**対処 (実装済み)**

`_Persistent.unity` に EventSystem を1つ配置。
Title / GamePlay / SongSelect / Result / Config / History の EventSystem GO を全て削除。

---

### 11.2 "There are 2 audio listeners in the scene"

**症状**
```
There are 2 audio listeners in the scene. Please ensure there is always exactly one audio listener in the scene.
```

**原因**

複数の Camera または GameObject に AudioListener がアタッチされている。

**対処 (実装済み)**

`_Persistent.unity` の SceneRouter に AudioListener を1個配置。
各シーンのカメラには `AudioListenerGuard.cs` をアタッチ:
```csharp
void Awake() {
    var listener = GetComponent<AudioListener>();
    if (listener == null) return;
    var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    if (all.Length > 1) listener.enabled = false;
}
```

---

### 11.3 Input System "Map must be contained in state"

**症状**
```
InvalidOperationException: Map must be contained in state to enable it.
```

**原因**

`InputActionMap.Enable()` を二重呼び出しした、または既に Disable 済みのマップに対して Disable を呼んだ。

**対処 (実装済み)**

`GameInputController` に `_mapEnabled` フラグ:
```csharp
bool _mapEnabled;
void OnEnable() {
    if (_gameplay != null && !_mapEnabled) { _gameplay.Enable(); _mapEnabled = true; }
}
void OnDisable() {
    if (_gameplay != null && _mapEnabled) { _gameplay.Disable(); _mapEnabled = false; }
}
```

---

### 11.4 "Can't remove AudioListener because AudioListenerGuard depends on it"

**症状**

Inspector から AudioListener を Remove しようとすると拒否される。

**原因**

`AudioListenerGuard.cs` に `[RequireComponent(typeof(AudioListener))]` が付いていた旧実装。

**対処 (実装済み)**

`AudioListenerGuard.cs` から `[RequireComponent]` を削除。
AudioListener を Destroy する代わりに `enabled = false` で無効化する方式に変更。

---

### 11.5 TMP フォント警告 (日本語文字が □ で表示)

**症状**
```
The character with Unicode value 楽 was not found in the [LiberationSans SDF] font asset
```
日本語のテキストが全て □ (代替文字) で表示される。

**原因**

デフォルトの `LiberationSans SDF` フォントアセットが日本語グリフを含まない。

**対処**

日本語対応フォントアセットを用意してテキストコンポーネントにアサイン:
1. 日本語対応フォント (Noto Sans JP 等) をプロジェクトに追加
2. TMP Font Asset Creator でフォントアセット作成
3. TMP_Text / TextMeshProUGUI の `Font Asset` を変更
4. または TMP Settings の `Default Font Asset` を変更

**Phase 2 の対応状況**: 未対応 (□ で表示されるが機能は正常)。Phase 3 でフォント整備予定。

---

## 12. Phase 4 教訓 (IInputSource / StageInitializer)

---

### 12.1 IInputSource 導入時の SerializeField 配線忘れ

**症状**

`JudgmentSystem.Initialize` で `NullReferenceException`。
`GamePlayController.Start` は正常に見えるのに判定が全く動かない。

**経緯**

Phase 4-pre で `JudgmentSystem` から `[SerializeField] GameInputController _input` を削除し、
`GamePlayController` に移動した。Inspector の配線が追いつかず NRE が発生した。

**対処 (実装済み)**

`GamePlayController.Start()` 先頭に明示的な null チェックを追加:
```csharp
if (_input == null)
{
    Debug.LogError("[GamePlay] _input is not assigned in Inspector. " +
                   "Drag GameInputController GameObject to GamePlayController._input.");
    return;
}
```

`JudgmentSystem.Initialize` の引数受け取り側でも `ArgumentNullException` でチェック。

**予防**

- `[SerializeField]` を別クラスに移動した後は Inspector 配線のリセットが起きていないか確認する
- 新しい `[SerializeField]` フィールドを追加したら、その Controller を持つシーンの GO を必ず開いて配線する
- `Start()` 冒頭に null チェック + `Debug.LogError` を入れる習慣

---

### 12.2 Domain 層の namespace と Unity 依存型の混在

**症状**

コンパイルエラー:
```
error CS0246: The type or namespace name 'LaneId' could not be found
```

**経緯**

`ReplayInputSource` を Domain 層 (`Domain/Input/`) に置いたが、
元の設計では `LaneId` (Unity 依存の enum) を使っていた。
さらにテスト asmdef が `overrideReferences: true` で `Assembly-CSharp` が見えず、
テスト側のコンパイルも連鎖して失敗した。

**対処 (実装済み)**

1. Domain 層専用の純粋型 `LaneRef` を導入:
   ```csharp
   // Domain/Input/LaneRef.cs
   public readonly struct LaneRef { ... }
   ```
2. `ReplayInputSource` は `LaneRef` のみ使用し Unity 非依存に
3. `GameInputController` (Unity 境界) が `LaneId` → `LaneRef` のキャストを担う:
   ```csharp
   OnLaneDown?.Invoke((LaneRef)(int)lane, timeMs);
   ```
4. テスト asmdef の `references` に `Domain.Tests` を追加

**予防**

- Domain 層に配置するクラスは `UnityEngine` の型を一切 using しない
- `LaneId` のような Unity enum を Domain に持ち込まず、`LaneRef` のような Pure C# 型を設計する
- 新しい Domain クラスを作ったら必ず asmdef の参照ルールに沿っているか確認する

**関連**: [7.1 CS0246 コンパイル失敗](#71-editmode-テストが-cs0246-でコンパイル失敗)

---

### 12.3 モード固有の初期化が共通モードにコピーされていない (3D が真っ黒)

**症状**

リプレイ再生中、UI (判定カウント・スコア・コンボ・ReplayHUD) は表示されるが、
3D レーン・ノーツ・JudgmentLine・BeatGrid が一切表示されない。

**ログ**

特になし (視覚的な問題)。

**経緯**

`GamePlayController.Start()` に散在していた4つの初期化呼び出しのうち、特に
`JacketBackgroundController.Instance?.SetCanvasEnabled(false)` が
`ReplayPlaybackController` に移植されていなかった。

- `JacketBackgroundCanvas` (Sort Order -1000, Screen Space Overlay) が有効なまま残る
- Screen Space Overlay は 3D カメラ出力の上に重なって描画される
- → 3D シーン全体が Canvas Overlay で隠れる
- → Sort Order が高い GameHud Canvas (Sort 0) だけが見える

**対処 (実装済み)**

`StageInitializer.cs` を新規作成し、Live / Replay 共通の初期化を集約:
```csharp
public static class StageInitializer
{
    public static void BindStageVisuals(conductor, chart, meta, scroller, hud)
    {
        JacketBackgroundController.Instance?.SetCanvasEnabled(false); // ← 主因修正
        BeatGridController.Instance?.BindGamePlay(conductor, bpmTimeline);
        scroller?.Initialize(chart);
        hud?.Initialize(meta, chart, isPvP: false);
    }

    public static void UnbindStageVisuals()
    {
        BeatGridController.Instance?.Unbind();
        JacketBackgroundController.Instance?.SetCanvasEnabled(true);
    }
}
```

両 Controller が `StageInitializer.BindStageVisuals()` を呼ぶように変更。

**予防**

- 新しいゲームモード Controller を作る際は `StageInitializer.BindStageVisuals()` の呼び出しを必ず含める
- Phase 完了チェックに「Live と同等の視覚要素が表示されるか」を必ず含める

**関連**: [3.1 レーンが見えない](#31-レーンが見えない-3d空間が見えない), 6.5 (handbook.md)

---

### 12.4 シーン GameObject 追加とスクリプト実装の乖離 (Replay ボタン)

**症状**

HISTORY 詳細パネルに Replay ボタンが表示されない。
コードを確認すると `[SerializeField] Button _replayButton` と `OnReplayClicked()` は実装済み。

**経緯**

`HistoryDetailView.cs` の実装は完了していたが、
シーン上に `ReplayButton` の UI GameObject を作成して Inspector 配線する作業が未実施だった。

さらに `DetailContent` が `DetailPanel` の高さ (609px) をオーバーしており、
`ReplayButton` (y=-668) が 59px パネル外にはみ出していた。

**対処 (実装済み)**

1. `DetailPanel` に `ScrollRect` を追加し `Viewport`(Mask) / `DetailContent` の階層を構築
2. `DetailContent` を固定高さ 720px に変更 (全要素が表示可能に)
3. `ReplayButton` GameObject を `DetailContent` 下に作成して `_replayButton` フィールドに配線

**予防**

Phase 完了チェックを以下の **3点セット**で確認する:

| チェック | 内容 |
|---|---|
| コード実装 | スクリプトのロジックが書かれているか |
| シーン配置 | UI GameObject・3D Mesh が Hierarchy に存在するか |
| Inspector 配線 | SerializeField が Inspector で null でないか |

1つ欠けても機能しない。コード実装だけ確認して「完了」にしないこと。

---

### 12.5 フォント Atlas に存在しない Unicode 文字 (▶/❚❚ が □)

**症状**

TextMeshPro 警告:
```
The character with Unicode value ▷ (▶) was not found in the [LiberationSans SDF] font asset.
```
`ReplayHud` の再生/一時停止ボタン文字が □ で表示される。

**経緯**

`ReplayHud.cs` で `▶` (U+25B7) と `❚❚` (U+275A U+275A) を直接文字列リテラルとして使用した。
プロジェクトのデフォルト TMP フォント (`LiberationSans SDF`) はこれらのグリフを持たない。

**対処**

**暫定** (即時対応):
```csharp
// ❌ 記号リテラル
_statusText.text = _controller.IsPlaying ? "▶" : "❚❚";

// ✅ ASCII 代替テキスト
_statusText.text = _controller.IsPlaying ? "Play" : "Pause";
```

**本格対応** (フォント Atlas に範囲追加):
1. `Window > TextMeshPro > Font Asset Creator` を開く
2. Source Font File に使用フォントを指定
3. Character Set: "Unicode Range (Hex)" で以下の範囲を追加:
   - `25A0-25FF` (幾何学的図形)
   - `2700-27BF` (装飾記号)
4. Generate Font Atlas → Save → TextMeshProUGUI の Font Asset を更新

**予防**

特殊記号を使う場合は Font Asset Creator で事前にグリフ包含を確認する。
使用する Unicode 文字を `Window > TextMeshPro > Glyph Viewer` でフォントに存在するか検索できる。

**関連**: [11.5 TMP フォント警告 (日本語)](#115-tmp-フォント警告-日本語文字が--で表示)

---

## 13. Phase 4b/4c 教訓 (サーバー系)

---

### 13.1 Nullable タプル `(T1, T2)?` の戻り値で CS8625/CS8620 警告

**症状**

`IChartRepository.TryGetByHashAsync` が「見つからない」を表現するために
`(null, null)` を返そうとして警告:
warning CS8625: null リテラルを null 非許容参照型に変換できません。
warning CS8620: 型 '(ChartData?, SongMetadata?)' の引数は、参照型の NULL 値の許容の違いにより、
'Task<(ChartData, SongMetadata)>' のパラメーター 'result' には使用できません。

**経緯**

interface の戻り値を `Task<(ChartData chart, SongMetadata meta)>` (非 nullable タプル) で定義したのに、
実装側で「見つからないケース」を `(null, null)` で表現していた。
タプルの各要素が non-nullable なので片方だけ null にもできず、設計と実装が噛み合わない。

**対処 (実装済み)**

interface を **nullable タプル**に変更し、「見つからない = `null`」を型レベルで明示:

```csharp
// IChartRepository.cs
public interface IChartRepository
{
    Task<(ChartData chart, SongMetadata meta)?> TryGetByHashAsync(string chartHashHex);
}
```

実装側:
```csharp
// 見つからない場合
return Task.FromResult<(ChartData, SongMetadata)?>(null);

// 成功した場合
return Task.FromResult<(ChartData, SongMetadata)?>((chart, meta));
```

呼び出し側:
```csharp
// ❌ destructure してから null チェック (型エラー)
var (chart, meta) = await _chartRepo.TryGetByHashAsync(hashHex);
if (chart == null || meta == null) { ... }

// ✅ Nullable のまま is null でチェック → .Value で展開
var lookup = await _chartRepo.TryGetByHashAsync(hashHex);
if (lookup is null) { return /* not found */; }
var (chart, meta) = lookup.Value;
```

**予防**

- 「見つからない / 失敗した」を表現したいときは `T?` (`Nullable<T>`) や `Result<T>` 型を使う
- タプルの要素を個別に nullable にする (`(T1?, T2?)`) のは「片方だけ null」という意味になるので避ける
- 戻り値が「あるか / ないか」の 2 値のときは `T?` 一択

---

### 13.2 `dotnet test` で実譜面ファイルが見つからない

**症状**

`RealChart_EmptyReplay_RecomputesAndMatches` テストで `FileNotFoundException`、
あるいは `Assert.True(true, "skipping")` で常にスキップされる。

**経緯**

テストの実行カレントディレクトリは `bin/Debug/net10.0` なので、
`Assets/StreamingAssets/Songs/` を相対パスで指す場合の階層数を間違えると見つからない。
PVP/                                              ← 5 階層上
└── Server/
└── RhythmGame.Server.Tests/
└── bin/
└── Debug/
└── net10.0/                       ← cwd

**対処 (実装済み)**

```csharp
var songsRoot = Path.GetFullPath(Path.Combine(
    Directory.GetCurrentDirectory(),
    "..", "..", "..", "..", "..",          // ← 5 階層上で PVP ルート
    "Assets", "StreamingAssets", "Songs"));
```

譜面が見つからない場合は graceful skip で環境差を吸収:

```csharp
if (!File.Exists(chartFile))
{
    Assert.True(true, "test_song chart not found, skipping");
    return;
}
```

**予防**

- テスト内で物理ファイルを参照する場合は `Path.GetFullPath(Path.Combine(...))` で絶対パス化してから使う
- ファイルがない環境 (CI、別マシン) でテストが落ちないよう graceful skip を入れる
- 本来は `[CallerFilePath]` などでテストファイル自身の場所を起点にするのが理想 (今後の改善案)

---

### 13.3 `async void` テストが xUnit1048 警告

**症状**
warning xUnit1048: Support for 'async void' unit tests is being removed from xUnit.net v3.
To simplify upgrading, convert the test to 'async Task' instead.

**経緯**

`[Fact] public async void XxxTest()` と書いたテストが、xUnit v3 で削除予定の構文。
v2 では動くが v3 にアップグレードした瞬間に全テストが認識されなくなる。

**対処 (実装済み)**

```csharp
// ❌ async void
[Fact]
public async void EmptyReplayData_ReturnsInvalid() { ... }

// ✅ async Task
[Fact]
public async Task EmptyReplayData_ReturnsInvalid() { ... }
```

`using System.Threading.Tasks;` の追加が必要。

**予防**

- xUnit のテストは最初から `async Task` で書く
- `async void` は「イベントハンドラ」や「Fire-and-Forget」専用と覚える
- 通常の async メソッドは `async Task` (戻り値あり / なしに関わらず)

---

### 13.4 サーバー起動時に IChartRepository がインデックスを作らない

**症状**

サーバー起動直後にリプレイ検証リクエストを送ると `Chart not registered on server`。
ログを見ても `[ChartRepo] Indexed: ...` の行が一切出ていない。

**経緯**

DI コンテナは singleton をデフォルトで **lazy init** する。
最初のリクエストが来た瞬間に `FileSystemChartRepository` のコンストラクタが走るため、
起動ログでは譜面が登録済みかどうか確認できない。

**対処 (実装済み)**

`Program.cs` で `IChartRepository` を起動時に強制初期化:

```csharp
// Program.cs (app.Build() の後)
var app = builder.Build();

// ↓ 追加: 起動時に IChartRepository のコンストラクタを走らせる
_ = app.Services.GetRequiredService<IChartRepository>();

app.MapGrpcService<ReplayValidationService>();
app.Run();
```

これで起動ログに以下が出るようになる:
[ChartRepo] Indexed: test_song/extra   hash=ED4A1F29FA43C58D...
[ChartRepo] Total indexed: 4 charts from C:\Users\CaSte\PVP\Assets\StreamingAssets\Songs
Now listening on: http://localhost:5246

**予防**

- 「起動時に何かを読み込む」サービスは DI 登録後に `GetRequiredService` で強制初期化する
- もしくは `IHostedService` を実装して `StartAsync` で初期化する (より正統)
- 起動ログに「準備完了」のメッセージを必ず仕込む

---

### 13.5 サーバー側でクライアントの不正な Claim を検知できているか

**症状 (これは「症状」ではなく検証手順)**

「サーバーで本当にチートを検知できているか」を確認したい。

**確認方法**

リプレイ検証は以下のフローで動く:
[Client]                           [Server]
リプレイ送信 ──────────────────→  ReplayDecoder.Decode
ChartHash 整合性チェック
IChartRepository から chart/meta 取得
JudgmentRunner.Run で再判定
PlayProgressSnapshot → PlayResultClaim 生成
CompareClaims でクライアント主張と比較
←───  ValidateResponse (IsValid + MismatchReason)

クライアントが Score を盛った場合 (空リプレイ + Claim.Score=1000000):
- サーバー側: `JudgmentRunner` で再計算 → 全 Miss → score=0
- 比較: client=1000000 ≠ server=0 → `MismatchReason="Score: client=1000000, server=0"` を返す

**テストで担保**

`RhythmGame.Server.Tests/ReplayValidationServiceTests.cs` の以下のテストが
不正検知の動作を CI で担保している:

| テスト | 内容 |
|---|---|
| `ScoreClaim_TooHigh_ReturnsMismatch` | 空リプレイで Score=1,000,000 を主張 → mismatch |
| `MaxComboClaim_TooHigh_ReturnsMismatch` | 空リプレイで MaxCombo=100 を主張 → mismatch |
| `ChartHashMismatch_ReturnsInvalid` | request.ChartHash と replay.ChartHash の不一致 |
| `ChartNotRegistered_ReturnsInvalid` | サーバーに登録されていない譜面のハッシュ |

**予防**

- 新しい改竄パターンを思いついたら必ずテストを書く (例: PerfectPlus を盛る、Rank を S+ に書き換える等)
- `CompareClaims` のフィールドを増やしたら対応するテストも追加する

---
## 付録 A: デバッグの基本手順

1. **Console のエラーログを最初に確認する**
   赤エラーが根本原因、黄警告は副次情報。Stack Trace を全展開して本当の死亡箇所を特定。

2. **async void の例外は握り潰される**
   `async void Start` / `async void TriggerResultAsync` は必ず try-catch でラップ。
   例外が出てもサイレントで止まる。

3. **Stage Debug.Log を仕込む**
   長い処理はどこで止まるか確認するため段階的にログ出力。

4. **Inspector の SerializeField 参照を疑う**
   Phase 2 で何度も発生。null チェックの警告ログを必ず入れる。

5. **Coroutine は GameObject が active でないと起動しない**
   `gameObject.SetActive(true)` → `StartCoroutine()` の順序を守る。

6. **大規模修正前は必ずバックアップ**
   `git commit` または フォルダコピー。

7. **169 テスト全 Pass を維持する**
   修正後は Test Runner で Run All。回帰したらすぐ調査。

---

## 付録 B: Phase 2 デバッグの時系列記録

Phase 2 後半で発生した一連の問題を時系列で記録。類似症状の参考に。

| 順序 | 症状 | 原因 | 対処 | 参照 |
|---|---|---|---|---|
| 1 | 起動直後に PERFECT+ 表示 | JudgmentTextPopup Coroutine + 起動状態 | Awake で SetActive(false) + Show 順序修正 | [3.3](#33-起動直後に-perfect-が画面上に表示される) |
| 2 | scenes=3 [_Persistent, GamePlay, GamePlay] | Unload 順序が Load-then-Unload だった | Pre-evict ステップを追加 | [2.3](#23-シーンが2個ロードされる-scenes3) |
| 3 | レーンが見えない (JacketBG が覆う) | Canvas Sort の設計ミス | SetCanvasEnabled(false) を GamePlay 開始時に呼ぶ | [3.1](#31-レーンが見えない-3d空間が見えない) |
| 4 | 23秒後に FormatException クラッシュ | chart.json の chartHash が非 hex | HexStringToBytes 防御処理 + ChartParser SHA-256 フォールバック | [3.4](#34-楽曲完走付近でクラッシュする-formatexception) |
| 5 | Audio not found (無音30秒クリップ) | StreamingAssets に audio.wav が無い | Tools > Generate Test Audio で生成 | [5.1](#51-audio-not-found--楽曲ファイルが見つからない) |
| 6 | chart JSON parse 失敗 | PowerShell が BOM 付き UTF-8 で書いた | ChartParser に BOM 除去処理追加 | [6.1](#61-failed-to-parse-chart-json-エラー) |
| 7 | SCORE テキストが画面外左にはみ出す | AnchorMax と Pivot の不整合 | RectTransform の Anchor/Pivot/SizeDelta を修正 | [4.1](#41-score--combo-等が画面外にはみ出す) |
| 8 | 大型コンボ数字が左下に切れる | Transform.LocalPosition が (-1184, -437) / Scale 3.24 | LocalPosition/Scale を直接修正 | [4.3](#43-大型コンボ表示が画面左下に切れる) |
| 9 | 判定文字が二重表示 | JudgmentTextPopup + JudgmentDisplay 両方が有効 | HandleJudged から _textPopup.Show を削除 | [4.2](#42-判定文字が二重表示される) |
| 10 | EventSystem 2個警告 | 各シーンに EventSystem が存在 | _Persistent に1個だけ残して他を削除 | [11.1](#111-there-can-be-only-one-active-event-system) |

**教訓**:
- 1つの修正が別の症状を引き起こすことがある (特に Canvas の重なり)
- `async void` の例外はサイレントなので最初に発見しにくい
- JSON ファイルの文字コード (BOM) は見えないので気付きにくい
- バックアップを取ってから修正する

---

## 付録 C: Phase 4 デバッグの時系列記録

Phase 4-pre / 4a で発生した問題の記録。

| 順序 | 症状 | 原因 | 対処 | 参照 |
|---|---|---|---|---|
| 1 | CS0246: LaneId が Domain 層で見つからない | ReplayInputSource が Unity 依存型を参照 | LaneRef 型を導入、境界でキャスト | [12.2](#122-domain-層の-namespace-と-unity-依存型の混在) |
| 2 | JudgmentSystem.Initialize で NRE | IInputSource 移動後に Inspector 配線が抜けた | Start() 冒頭の null チェック + LogError | [12.1](#121-iinputsource-導入時の-serializefield-配線忘れ) |
| 3 | Replay 中に 3D が真っ黒 (UI は出る) | SetCanvasEnabled(false) が Replay で呼ばれない | StageInitializer 新設、両 Controller から呼ぶ | [12.3](#123-モード固有の初期化が共通モードにコピーされていない-3d-が真っ黒) |
| 4 | History の Replay ボタンが見えない | UI GO の作成・配線が未実施 + ScrollRect なし | GO 作成・配線 + ScrollRect + Viewport 追加 | [12.4](#124-シーン-gameobject-追加とスクリプト実装の乖離-replay-ボタン) |
| 5 | ReplayHud の ▶/❚❚ が □ で表示 | フォント Atlas にグリフが存在しない | "Play"/"Pause" の ASCII 代替テキストに変更 | [12.5](#125-フォント-atlas-に存在しない-unicode-文字-▶❚❚-が-) |

**Phase 4 の追加教訓**:
- 新モード Controller を追加したら「Live と同等の視覚要素が全部出るか」を必ず確認する
- コード実装・シーン配置・Inspector 配線の **3点セット**を Phase 完了条件にする
- Domain 層に Unity 型を持ち込まない原則は徹底する (LaneRef の教訓)
- `StageInitializer.BindStageVisuals()` が新モードの「忘れ防止チェックリスト」になった

---

*このドキュメントは Phase 1+2+4 の実際の開発経験から作成。Phase 3 で同様の問題が発生したら追記する。*
