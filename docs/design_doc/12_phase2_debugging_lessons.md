# 12. Phase 2 デバッグの教訓

Phase 2 後半で発生した問題の時系列記録と重要な知見をまとめる。  
詳細は `docs/troubleshooting.md` を参照。

---

## 12.1 問題の時系列

| 順序 | 症状 | 原因 | 対処 |
|---|---|---|---|
| 1 | 起動直後に PERFECT+ が表示 | JudgmentTextPopup の Coroutine 順序ミス | Awake で SetActive(false)、Show で先に SetActive(true) |
| 2 | scenes=3 [GamePlay, GamePlay] | Unload 順序が誤り (Load-then-Unload) | Pre-evict ステップを追加 |
| 3 | レーンが見えない | JacketBackgroundCanvas が 3D を覆う | GamePlay 中は SetCanvasEnabled(false) |
| 4 | 23秒後に FormatException | chart.json の chartHash が非 hex | HexStringToBytes に防御処理、ChartParser で SHA-256 フォールバック |
| 5 | Audio not found | StreamingAssets に音源がない | Tools > Generate Test Audio を実行 |
| 6 | chart JSON parse 失敗 | PowerShell が BOM 付き UTF-8 で書き込み | ChartParser で BOM 除去処理を追加 |
| 7 | SCORE テキストが画面外左に切れる | RectTransform の Anchor/Pivot 不整合 | AnchorMax、Pivot、SizeDelta を修正 |
| 8 | コンボ数字が左下に切れる | Transform.LocalPosition が (-1184,-437)、Scale 3.24 | LocalPosition と Scale を直接修正 |
| 9 | 判定文字が二重表示 | JudgmentTextPopup と JudgmentDisplay が両方有効 | HandleJudged から _textPopup.Show を削除 |
| 10 | EventSystem 2個警告 | 各シーンに EventSystem が存在 | _Persistent に1個だけ残して他を削除 |

---

## 12.2 特に重要な知見

### 知見 1: SceneRouter の Unload-then-Load 順序

**旧 (誤)**: 新シーンを Load した後で旧シーンを Unload。  
→ 同名シーンが2つ共存し、`GetSceneByName` が古い方を返してバグる。

**正**: 旧シーンを先に全 Unload (_Persistent 以外) → 新シーンを Load → SetActiveScene。

さらに **Pre-evict ステップ**を追加:  
Load より前に同名シーンを Unload することで再遷移時の二重ロードを防ぐ。

---

### 知見 2: JacketBackground の SetCanvasEnabled

JacketBackgroundCanvas (Sort -1000) は全シーンで常に有効。  
GamePlay 中に 3D レーン・ノーツが表示されない問題の根本原因となった。

**対処**:
```csharp
// GamePlayController.Start
JacketBackgroundController.Instance?.SetCanvasEnabled(false);

// GamePlayController.TriggerResultAsync (Result遷移前)
JacketBackgroundController.Instance?.SetCanvasEnabled(true);
```

教訓: Canvas Sort Order だけでは 3D 空間と Overlay Canvas の重なりを制御しきれない。

---

### 知見 3: HexStringToBytes の防御処理

chart.json の `chartHash` フィールドに開発テスト用の値 (`"abc123test"` 等) が  
入っていた場合、楽曲完走時の `Convert.ToByte` で FormatException が発生する。

**防御層 2重**:
1. `ChartParser.ParseChart`: 非 hex なら SHA-256 で自動計算
2. `GamePlayController.HexStringToBytes`: 非 hex 文字検出なら警告 + ゼロバイト返却

---

### 知見 4: SceneAutoLoader の必要性

Unity Editor の ▶ ボタンは現在開いているシーンを直接起動する。  
Bootstrap → _Persistent のロードが省略され、SceneRouter.Instance が null になる。

**解決**: `Editor/SceneAutoLoader.cs` が ▶ 押下時に Bootstrap.unity を強制 Open。  
起動後に元のシーンに戻る (EditorPrefs でパスを記憶)。

---

### 知見 5: async void Start の例外は握り潰される

```csharp
// ❌ 例外が Console に出ないことがある
async void Start() {
    await SomeAsyncMethod(); // 例外 → サイレント死
}

// ✅ try-catch で囲む
async void Start() {
    try { await SomeAsyncMethod(); }
    catch (Exception e) { Debug.LogError("..." + e.StackTrace); }
}
```

`GamePlayController.Start` / `TriggerResultAsync` 等で実装済み。

---

### 知見 6: AudioMixer は手動セットアップが必要

`UnityEditor.Audio.AudioMixerController` は `ExtensionOfNativeClass` 属性のネイティブクラス。  
`ScriptableObject.CreateInstance` では作成不可。

手順:
1. Assets > Create > Audio > Audio Mixer
2. グループ (Music / SFX) を手動追加
3. Volume を Expose
4. Inspector でアサイン

**フォールバック設計**: Mixer 未割当でも `AudioSource.volume` 直接制御で動作する。

---

### 知見 7: Coroutine は GameObject が Active でないと起動しない

```csharp
// ❌ 起動失敗 (gameObject が inactive)
public void Show() {
    StartCoroutine(Animate()); // active でないと黙って失敗
}

// ✅ SetActive してから StartCoroutine
public void Show() {
    gameObject.SetActive(true); // 先に active にする
    StartCoroutine(Animate());
}
```

---

### 知見 8: UTF-8 BOM が JSON パースを壊す

PowerShell 5.1 の `-Encoding utf8` は BOM 付き UTF-8 で書き出す。  
Newtonsoft.Json が先頭の BOM 文字 `﻿` を不正 JSON と判断して例外。

**対処 A**: ChartParser の先頭で BOM を除去:
```csharp
if (json != null && json.Length > 0 && (int)json[0] == 0xFEFF)
    json = json.Substring(1);
```

**対処 B**: PowerShell では BOM なし UTF-8 で書き出す:
```powershell
$enc = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($path, $content, $enc)
```

---

## 12.3 Phase 2 デバッグで学んだ設計原則

1. **外部入力を信頼しない**: chart.json の値は必ず検証・防御処理を入れる
2. **async void は必ず try-catch**: サイレント例外は発見が遅れる
3. **バックアップを取ってから修正**: 1つの修正が別の症状を引き起こすことがある
4. **テストが回帰防止に効いた**: 169 テストが Phase 2 修正でも既存機能を守った
5. **フォールバック設計**: 外部ライブラリ (AudioMixer) が未設定でも動くよう設計する
6. **Stage ログ**: 長い処理は段階的にログ出力して停止箇所を特定する
