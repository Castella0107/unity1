# 手動テスト仕様書 — 画面遷移

**対象バージョン**: master ブランチ  
**作成日**: 2026-05-13  
**対象スコープ**: シーン遷移・遷移後の入力動作  
**対象外**: UIの見た目（主観的判断）、パフォーマンス、プレイ感覚

---

## テスト凡例

```
手順     : テストの操作手順
期待結果 : 正しい動作の説明
確認方法 : Console ログ・Hierarchy・目視で確認する手段
```

チェックボックスの記号：
- `[ ]` 未実施
- `[x]` 合格
- `[!]` 不合格（バグ）

---

## 遷移マップ

```
Bootstrap ──auto──► Title
                     │
          ┌──────────┼──────────┬────────────┐
          ▼          ▼          ▼            ▼
      SongSelect   Config    History        (EXIT)
          │          │          │
     play │     back │     back │  replay
          ▼          ▼          ▼
       GamePlay ──── Title   GamePlay(replay)
          │                     │
    song end │            replay end │
          ▼                     ▼
        Result               History
     ┌────┼────┐
retry│ select│ title│
     ▼     ▼     ▼
GamePlay SongSelect Title
```

---

## 目次

1. [起動シーケンス（Bootstrap → Title）](#1-起動シーケンス)
2. [Title からの遷移](#2-title-からの遷移)
3. [SongSelect からの遷移](#3-songselect-からの遷移)
4. [GamePlay からの遷移（曲終了）](#4-gameplay-からの遷移曲終了)
5. [GamePlay からの遷移（ポーズメニュー）](#5-gameplay-からの遷移ポーズメニュー)
6. [Result からの遷移](#6-result-からの遷移)
7. [Config からの遷移](#7-config-からの遷移)
8. [History からの遷移](#8-history-からの遷移)
9. [リプレイ遷移（History → GamePlay → History）](#9-リプレイ遷移)
10. [SceneRouter 共通動作](#10-scenerouter-共通動作)
11. [遷移後キー入力 回帰テスト](#11-遷移後キー入力-回帰テスト)

---

## 1. 起動シーケンス

**遷移**: Bootstrap → (_Persistent ロード) → Title  
**関連コード**: `BootstrapController.Start()`, `SceneRouter.InitialBoot()`

### 正常系

- [ ] **TC-BS-01**: アプリ起動 → Bootstrap が自動的に Title へ遷移する  
  手順: アプリを起動する（Play ボタンまたはビルド済み exe 実行）  
  期待結果: Title 画面が表示される。フェードなし（`TransitionStyle.None`）で即座に表示  
  確認方法: Console に `[Bootstrap] Transition complete. Unloading Bootstrap.` が出力されることを確認

- [ ] **TC-BS-02**: 起動後に `_Persistent` シーンが維持されている  
  手順: TC-BS-01 完了後に Hierarchy を確認  
  期待結果: `_Persistent` シーンが Additive でロードされたまま、Bootstrap シーンはアンロード済み  
  確認方法: Hierarchy の Scene 一覧に `_Persistent` と `Title` のみが存在する

- [ ] **TC-BS-03**: 起動後すぐにキー入力を受け付ける  
  手順: TC-BS-01 完了直後に左右矢印キーを押す  
  期待結果: Title メニューが切り替わる  
  確認方法: 目視確認

### 異常系

- [ ] **TC-BS-04**: `_Persistent` シーンが Build Settings に含まれていない場合 → エラーログ  
  手順: Build Settings から `_Persistent` を除外してビルド / Play  
  期待結果: Console に `[Bootstrap] SceneRouter not found — verify _Persistent.unity` エラーが出力。その後のシーン遷移は行われない  
  確認方法: Console エラーを確認。アプリがフリーズしないことも確認

- [ ] **TC-BS-05**: `_Persistent` のロードが 300 フレーム(約5秒)以上かかる → タイムアウト処理が動く  
  手順: 意図的に `_Persistent` のロードを遅延させる（ネットワーク環境のシミュレーション等）  
  期待結果: Console に `[Bootstrap] _Persistent load timed out!` が出力され、yield break で安全に停止

---

## 2. Title からの遷移

**関連コード**: `TitleController.Decide()`

### 正常系

- [ ] **TC-TL-01**: Title → SongSelect（FREE PLAY）  
  手順: 矢印キーで `FREE PLAY` を選択 → Enter  
  期待結果: SongSelect 画面に遷移。LoadingOverlay が表示される（HeavyLoadScene のため）  
  確認方法: 遷移完了後 Console に `[SceneRouter] After GoTo SongSelect` ログ確認

- [ ] **TC-TL-02**: Title → Config  
  手順: 矢印キーで `CONFIG` を選択 → Enter  
  期待結果: Config 画面に遷移。LoadingOverlay は表示されない（軽量シーン）  
  確認方法: 目視確認（フェードのみ、ローディング画面なし）

- [ ] **TC-TL-03**: Title → History  
  手順: 矢印キーで `HISTORY` を選択 → Enter  
  期待結果: History 画面に遷移  
  確認方法: 目視確認

- [ ] **TC-TL-04**: Title → EXIT でアプリ終了  
  手順: 矢印キーで `EXIT` を選択 → Enter（または Esc キー）  
  期待結果: エディタでは `EditorApplication.isPlaying = false`、ビルドでは `Application.Quit()`  
  確認方法: エディタが Play モードを終了すること

### エッジケース

- [ ] **TC-TL-05**: カードフリップアニメーション中（`_isFlipping = true`）に Enter 連打  
  手順: 矢印キーでフリップ開始直後に Enter を連打  
  期待結果: フリップ完了まで遷移が発生しない（`_isFlipping` フラグで Guards）  
  確認方法: 二重遷移や予期しないシーン遷移が発生しないことを確認

---

## 3. SongSelect からの遷移

**関連コード**: `SongSelectController.OnPlay()`, `OnBack()`

### 正常系

- [ ] **TC-SS-01**: SongSelect → GamePlay（Play ボタン）  
  手順: 楽曲を選択 → Play ボタンをクリックまたは Enter  
  期待結果: GamePlay 画面に遷移。LoadingOverlay が表示される（HeavyLoadScene のため）  
  確認方法: Console に `[GamePlay] Started — song=...` ログ確認

- [ ] **TC-SS-02**: SongSelect → GamePlay（難易度・HiSpeed・Modifier がパラメータに反映される）  
  手順: Hard / HiSpeed=6.0 / Mirror を選択してプレイ  
  期待結果: GamePlay シーン内で `GamePlayParameters.Difficulty == "hard"`, `HiSpeed == 6.0`, `Modifier == "Mirror"` が設定されている  
  確認方法: Console の `[GamePlay] Started` ログや `ParameterStore.GetCurrent<GamePlayParameters>()` で確認

- [ ] **TC-SS-03**: SongSelect → Title（Back ボタン）  
  手順: Back ボタンをクリックまたは Esc  
  期待結果: Title 画面に遷移  
  確認方法: 目視確認

- [ ] **TC-SS-04**: SongSelect → Title（Back 後）キー入力が正常に動作する  
  手順: TC-SS-03 完了後、矢印キーを押す  
  期待結果: Title メニューが切り替わる（入力が死んでいないことを確認）  
  確認方法: 目視確認

### 異常系

- [ ] **TC-SS-05**: 楽曲が 0 件の状態で Play ボタンを押す  
  手順: `StreamingAssets/Songs/` を空にして SongSelect を開き、Play  
  期待結果: 遷移が発生しない（`if (_songs.Count == 0) return`）  
  確認方法: GamePlay シーンへの遷移がないことを確認

---

## 4. GamePlay からの遷移（曲終了）

**関連コード**: `GamePlayController.TriggerResultAsync()`

### 正常系

- [ ] **TC-GE-01**: 曲が終了する → Result 画面へ自動遷移  
  手順: 楽曲を最後まで再生する（または短い曲でテスト）  
  期待結果: `SongTimeMs >= DurationMs + 1000ms` になると自動的に Result へ遷移  
  確認方法: Console に `[GamePlay] TriggerResultAsync completed — score=...` を確認

- [ ] **TC-GE-02**: Result 遷移前にリプレイが保存される  
  手順: TC-GE-01 を実行し、`ReplayStorage` のディレクトリを確認  
  期待結果: `[GamePlay] Replay saved: <path>` ログが出力され、ファイルが存在する  
  確認方法: Console ログおよび `persistentDataPath/replays/` フォルダを確認

- [ ] **TC-GE-03**: Result 遷移前にスコアが SQLite に保存される  
  手順: TC-GE-01 を実行  
  期待結果: Console に `TriggerResultAsync completed` ログ後、History 画面で当該プレイが表示される  
  確認方法: History 画面を開いて最新エントリが追加されていることを確認

### 異常系

- [ ] **TC-GE-04**: 音声ファイルが存在しない楽曲で曲終了 → サイレントで Result 遷移  
  手順: audio ファイルがない楽曲をプレイし、30秒待機（サイレントクリップは30秒）  
  期待結果: 30秒後に自動で Result へ遷移。クラッシュなし  
  確認方法: Console に `Audio not found: ... → using 30-second silent clip` ログ確認

---

## 5. GamePlay からの遷移（ポーズメニュー）

**関連コード**: `PauseMenu.cs`

### 正常系

- [ ] **TC-PM-01**: プレイ中 Esc → ポーズパネルが開く（シーン遷移ではない）  
  手順: GamePlay 中に Esc キーを押す  
  期待結果: ポーズパネルが表示される。曲が一時停止。シーン遷移は発生しない  
  確認方法: `_panel.activeSelf == true`、音が止まることを確認

- [ ] **TC-PM-02**: ポーズ中 Esc（Resume）→ カウントダウン後に再開  
  手順: TC-PM-01 後、再度 Esc キーを押す  
  期待結果: パネルが閉じ、3→2→1→GO! のカウントダウン後に曲が再開される  
  確認方法: 曲が再生されることを確認

- [ ] **TC-PM-03**: ポーズ中 Restart → GamePlay シーンを同パラメータで再起動  
  手順: ポーズメニューで Restart を選択（↓キーで移動 → Enter）  
  期待結果: 同じ楽曲・難易度で GamePlay が最初から再起動  
  確認方法: Console に `[GamePlay] Started — song=<同じsongId>` を確認

- [ ] **TC-PM-04**: ポーズ中 Quit → SongSelect へ遷移  
  手順: ポーズメニューで Quit を選択  
  期待結果: SongSelect 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-PM-05**: ポーズ → Quit → SongSelect 後にキー入力が正常  
  手順: TC-PM-04 完了後、上下矢印キーで楽曲選択  
  期待結果: 楽曲リストが上下に移動する  
  確認方法: 目視確認

### 異常系

- [ ] **TC-PM-06**: 曲が再生中でない（ロード中）に Esc を押す → ポーズが開かない  
  手順: GamePlay 起動直後（曲ロード中）に Esc を押す  
  期待結果: `_conductor.IsPlaying == false` のため `OpenPause()` は呼ばれない  
  確認方法: パネルが開かないことを確認

### エッジケース

- [ ] **TC-PM-07**: カウントダウン中（Resume後）に再度 Esc を押す → カウントダウンが最初からやり直しにならない  
  手順: Resume 後のカウントダウン中に Esc を押す  
  期待結果: `_isPaused == false` のため `OpenPause` は呼ばれず、カウントダウンが正常に続行される  
  確認方法: カウントダウンが中断されないことを確認

---

## 6. Result からの遷移

**関連コード**: `ResultController.cs`

### 正常系

- [ ] **TC-RS-01**: Result → GamePlay（Retry / Enter キー）  
  手順: Result 画面で Enter キーまたは Retry ボタンをクリック  
  期待結果: 同じ楽曲・難易度で GamePlay が起動（`SourceGamePlayParameters` を再利用）  
  確認方法: Console に `[GamePlay] Started — song=<同じsongId>` を確認

- [ ] **TC-RS-02**: Result → SongSelect（S キーまたは ToSelect ボタン）  
  手順: S キーまたは「曲選択」ボタン  
  期待結果: SongSelect 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-RS-03**: Result → Title（T キーまたは ToTitle ボタン）  
  手順: T キーまたは「タイトル」ボタン  
  期待結果: Title 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-RS-04**: Result → Title 後にキー入力が正常  
  手順: TC-RS-03 完了後、矢印キーを押す  
  期待結果: Title メニューが切り替わる  
  確認方法: 目視確認

- [ ] **TC-RS-05**: Result → GamePlay（Retry）後にキー入力が正常  
  手順: TC-RS-01 完了後、プレイ中に Esc でポーズできるか確認  
  期待結果: ポーズメニューが開く  
  確認方法: 目視確認

### 異常系

- [ ] **TC-RS-06**: `SourceGamePlayParameters` が null の場合 → SongSelect へフォールバック  
  手順: ParameterStore に `ResultParameters` だが `SourceGamePlayParameters == null` な状態でRetry  
  期待結果: SongSelect へ遷移（`if (_params?.SourceGamePlayParameters != null)` 失敗のため）  
  確認方法: SongSelect が開くことを確認

---

## 7. Config からの遷移

**関連コード**: `ConfigController.OnBack()`

### 正常系

- [ ] **TC-CF-01**: Config → Title（Back ボタン）  
  手順: Config 画面で Back ボタンをクリックまたは Esc  
  期待結果: Title 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-CF-02**: Config → Title 後にキー入力が正常  
  手順: TC-CF-01 完了後、矢印キーを押す  
  期待結果: Title メニューが切り替わる  
  確認方法: 目視確認

- [ ] **TC-CF-03**: Config 内でタブを切り替えても遷移は発生しない  
  手順: Audio / Devices / Display など各タブボタンをクリック  
  期待結果: コンテンツパネルの表示が切り替わるのみ。シーン遷移は発生しない  
  確認方法: Hierarchy のシーン構成が変わらないことを確認

---

## 8. History からの遷移

**関連コード**: `HistoryController.OnCancel()`, `OnBack()`  
**注記**: 過去に「History → Title 遷移後にキー入力が死ぬ」バグが存在（修正済み）。本セクションはその回帰テストを含む。

### 正常系

- [ ] **TC-HS-01**: History → Title（Back ボタン）  
  手順: History 画面で Back ボタンをクリック  
  期待結果: Title 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-HS-02**: History → Title（Esc キー）  
  手順: History 画面で Esc キーを押す  
  期待結果: Title 画面へ遷移  
  確認方法: 目視確認

- [ ] **TC-HS-03**: **【回帰】History → Title 後にキー入力が正常に動作する**  
  手順: TC-HS-01 または TC-HS-02 完了後、矢印キー（左右）を押す  
  期待結果: Title メニューが切り替わる（左右矢印でカード flip）  
  確認方法: 目視確認。メニューが切り替わらない場合は `OnDisable` の `Disable()` 残留を疑う

- [ ] **TC-HS-04**: **【回帰】History → Title 後に Enter キーが正常に動作する**  
  手順: TC-HS-03 完了後、Enter キーを押す  
  期待結果: 現在選択中のメニュー項目の処理が実行される（例：FREE PLAY なら SongSelect へ遷移）  
  確認方法: 目視確認

- [ ] **TC-HS-05**: History → Title を 3 回繰り返してもキー入力が正常  
  手順: Title → History → Title → History → Title → History → Title  
  期待結果: 毎回 Title に戻るたびにキー入力が機能する  
  確認方法: 各 Title 到達後に矢印キーが動くことを確認

### 異常系

- [ ] **TC-HS-06**: リプレイデータが 0 件の状態で History を開く → 空状態の表示  
  手順: 初回起動（プレイ履歴なし）で History を開く  
  期待結果: `_emptyState` が表示される。クラッシュなし  
  確認方法: 「プレイ履歴がありません」相当の空状態 UI が表示されることを確認

---

## 9. リプレイ遷移

**遷移**: History → GamePlay（replay）→ History  
**関連コード**: `ReplayPlaybackController.cs`

### 正常系

- [ ] **TC-RP-01**: History → GamePlay（リプレイ再生）  
  手順: History でレコードを選択 → リプレイ再生ボタン  
  期待結果: GamePlay 画面が開き、ノートが自動で流れる。入力を受け付けない  
  確認方法: Console に `[Replay] Started — song=...` ログ確認

- [ ] **TC-RP-02**: リプレイ終了 → History 画面へ戻る  
  手順: TC-RP-01 後、リプレイが最後まで再生される  
  期待結果: History 画面へ自動遷移（`OnReplayFinished`）  
  確認方法: Console に `[SceneRouter] After GoTo History` ログ確認

- [ ] **TC-RP-03**: **【回帰】リプレイ → History 遷移後にキー入力が正常**  
  手順: TC-RP-02 完了後、上下矢印キーを押す  
  期待結果: History のリスト選択が動作する  
  確認方法: 目視確認

- [ ] **TC-RP-04**: リプレイ → History → Title のキー入力が正常  
  手順: TC-RP-02 完了後、Esc で Title へ → 矢印キーを押す  
  期待結果: Title メニューが切り替わる  
  確認方法: 目視確認

### 異常系

- [ ] **TC-RP-05**: ReplayPath が未設定でリプレイモード起動 → エラーログで停止  
  手順: `GamePlayParameters.IsReplay = true`, `ReplayPath = ""` で GamePlay へ遷移  
  期待結果: Console に `[Replay] ReplayPath is empty or ReplayStorage unavailable.` が出力。GamePlay 画面でフリーズしない  
  確認方法: Console ログ確認。手動で History へ戻れること（Esc 等で）

---

## 10. SceneRouter 共通動作

**関連コード**: `SceneRouter.GoToRoutine()`

### 正常系

- [ ] **TC-SR-01**: 遷移中に `IsTransitioning == true` になる  
  手順: 遷移開始直後に `SceneRouter.Instance.IsTransitioning` の値を確認  
  期待結果: 遷移中は `true`、完了後は `false`  
  確認方法: Debug.Log を `GoToRoutine` の前後に追加して確認

- [ ] **TC-SR-02**: HeavyLoadScene（GamePlay / SongSelect）遷移時は LoadingOverlay が表示される  
  手順: SongSelect へ遷移する  
  期待結果: フェードアウト後に LoadingOverlay が表示され、ロード完了後に非表示になる  
  確認方法: 目視確認

- [ ] **TC-SR-03**: 非 HeavyLoadScene（Config / History / Result）遷移時は LoadingOverlay が表示されない  
  手順: Config / History / Result へ遷移する  
  期待結果: フェードのみ。LoadingOverlay は表示されない  
  確認方法: 目視確認

- [ ] **TC-SR-04**: 遷移完了後に旧シーンがアンロードされている  
  手順: 任意の遷移を実行後、Hierarchy を確認  
  期待結果: `_Persistent` と新シーンのみが存在する（旧シーンは消えている）  
  確認方法: Unity Hierarchy の Scene 一覧を確認

- [ ] **TC-SR-05**: 同じシーンへの再遷移（例: GamePlay → GamePlay）でシーンが重複しない  
  手順: Result の Retry で GamePlay → GamePlay を実行  
  期待結果: GamePlay シーンが 2 つ存在しない（Pre-evict で旧シーンが先に削除される）  
  確認方法: Hierarchy を確認。Console に `[SceneRouter] Pre-evicting stale: GamePlay` ログが出ること

### 異常系

- [ ] **TC-SR-06**: 遷移中に GoTo を再度呼ぶ → 警告ログで無視される  
  手順: フェードアニメーション中にボタンを連打（または Esc を連打）  
  期待結果: Console に `[SceneRouter] Transition in progress — ignoring GoTo(...)` が出力。二重遷移は発生しない  
  確認方法: Console ログ確認

- [ ] **TC-SR-07**: Build Settings に存在しないシーン名を GoTo → エラーログで停止  
  手順: `SceneNames` ディクショナリに存在しない `SceneId` を渡す（コードで直接呼び出し）  
  期待結果: Console に `[SceneRouter] No scene name mapping for ...` エラー。遷移は行われない

---

## 11. 遷移後キー入力 回帰テスト

**背景**:  
`InputActionAsset` は ScriptableObject のため、全シーンコントローラーが **同一** の `InputAction` インスタンスを参照する。  
アディティブロードの遷移順序は「新シーン `OnEnable`（Enable）→ 旧シーン `OnDisable`（Disable）」であるため、旧シーンの `OnDisable` で `Disable()` を呼ぶと新シーンの入力が無効化されるバグが存在した（2026-05-13 修正済み）。

**修正対象ファイル**:
- `TitleController.OnDisable`
- `SongSelectController.OnDisable`
- `ConfigController.OnDisable`
- `HistoryController.OnDisable`
- `ResultController.OnDisable`（OnEnable/OnDisable パターンへ変更）

### 回帰テスト一覧

以下を **すべて** 実行し、すべてのシーン遷移後にキー入力が機能することを確認する。

| ID | 遷移パス | 確認するキー操作 |
|----|----------|-----------------|
| TC-KI-01 | Title → History → **Title** | 矢印左右でメニュー切替 |
| TC-KI-02 | Title → Config → **Title** | 矢印左右でメニュー切替 |
| TC-KI-03 | Title → SongSelect → **Title** (Back) | 矢印左右でメニュー切替 |
| TC-KI-04 | Title → SongSelect → GamePlay → Result → **Title** | 矢印左右でメニュー切替 |
| TC-KI-05 | Title → SongSelect → GamePlay → Result → **SongSelect** (Select) | 上下矢印で楽曲移動 |
| TC-KI-06 | Title → SongSelect → GamePlay → **SongSelect** (Pause→Quit) | 上下矢印で楽曲移動 |
| TC-KI-07 | Title → History → GamePlay(replay) → **History** | 上下矢印でリスト移動 |
| TC-KI-08 | 上記 TC-KI-07 の後さらに → **Title** | 矢印左右でメニュー切替 |

- [ ] **TC-KI-01**: 遷移後 Title のキー入力が正常
- [ ] **TC-KI-02**: 遷移後 Title のキー入力が正常
- [ ] **TC-KI-03**: 遷移後 Title のキー入力が正常
- [ ] **TC-KI-04**: 遷移後 Title のキー入力が正常
- [ ] **TC-KI-05**: 遷移後 SongSelect のキー入力が正常
- [ ] **TC-KI-06**: 遷移後 SongSelect のキー入力が正常
- [ ] **TC-KI-07**: 遷移後 History のキー入力が正常
- [ ] **TC-KI-08**: 遷移後 Title のキー入力が正常

---

## 変更履歴

| 日付 | 変更内容 |
|------|----------|
| 2026-05-13 | 初版作成（画面遷移テストに絞り再構成） |
