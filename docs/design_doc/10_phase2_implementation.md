# 10. Phase 2 実装記録

Phase 2 (2026年5月完成) で実装した機能の設計判断と最終形を記録する。

---

## 10.1 背景ジャケット [Phase 2-1]

### 設計

ジャケット画像を全画面表示し、ぼかしエフェクトを適用して背景として使用する。  
全シーンで共通表示するため `_Persistent` シーンに配置 (DontDestroyOnLoad)。

### Canvas Sort Order 設計

| Sort | Canvas | 役割 |
|---|---|---|
| -1000 | JacketBackgroundCanvas | ぼかし背景 (最背面) |

### ポイント

- GamePlay 中は 3D レーン・ノーツが見えるよう `SetCanvasEnabled(false)` が必須
  - `GamePlayController.Start()` で false
  - `TriggerResultAsync()` で true に戻す
- CanvasGroup.alpha でクロスフェード遷移

### 関連ファイル

- `Scripts/UI/JacketBackgroundController.cs` (Persistent Service)
- `Scripts/UI/JacketLoader.cs` (非同期ジャケット読み込み)
- Shader: ぼかし処理 (RawImage + Material)

---

## 10.2 BPM 連動グリッド [Phase 2-2]

### 設計

BPM に合わせてパルスするグリッドを GamePlay 画面の背景に表示。  
ビート感覚の視覚的補助。

### Canvas Sort Order

| Sort | Canvas | 役割 |
|---|---|---|
| -500 | BeatGridCanvas | BPM グリッド (GamePlay 中のみ) |

### BPM 同期

```
AudioConductor.SongTimeMs → BpmTimeline.GetBpmAt(ms)
  └─→ 次の拍タイミング計算
       └─→ グリッド Shader の _PulseIntensity 更新
```

### 注意点

Material.SetFloat はアセット直接変更のため `OnDestroy`/`Unbind` で初期値にリセット:

```csharp
void OnDestroy() {
    _gridMaterial?.SetFloat("_PulseIntensity", 1.0f);
}
```

### 関連ファイル

- `Scripts/UI/BeatGridController.cs` (Persistent Service)
- BeatGrid Shader

---

## 10.3 判定エフェクト [Phase 2-3]

### 設計

判定発火時に3種類の演出を行う:
1. **パーティクル**: レーン位置でスポーン、判定色で発光
2. **判定テキスト**: 画面中央にフェードアウト + FAST/LATE 表示
3. **コンボ演出**: コンボ数字の bump アニメーション

### コンポーネント構成

```
JudgmentEffectsController (JudgmentSystem.OnJudged を購読)
  ├─ JudgmentParticlePool.Spawn(pos, color, mul)
  ├─ HitSoundPlayer.PlayJudgment(j)
  └─ Update: ComboDisplay.SetCombo(aggregator.CurrentCombo)

JudgmentDisplay (JudgmentSystem.OnJudged を購読)
  └─ _judgeText + _timingText でフェードアウト表示

ComboDisplay
  ├─ SetCombo(combo): 数字更新 + bump Coroutine
  ├─ Milestones {50, 100, 250, 500, 1000}: ゴールド色フラッシュ
  └─ combo=0 で SetActive(false) (非表示)
```

### JudgmentTextPopup の扱い

Phase 2 デバッグで **無効化** (JudgmentDisplay と二重表示になるため):

```csharp
// JudgmentEffectsController.HandleJudged 内
// if (_textPopup != null) _textPopup.Show(j);  ← 削除済み
```

### 関連ファイル

- `Scripts/Game/JudgmentEffectsController.cs`
- `Scripts/Game/JudgmentParticlePool.cs`
- `Scripts/Game/JudgmentColors.cs`
- `Scripts/Game/ComboDisplay.cs`
- `Scripts/UI/JudgmentDisplay.cs`

---

## 10.4 ヒット音 [Phase 2-4]

### 設計

2種類の音を再生:
1. **タップ音** (`PlayTapClick`): キー押下の即時フィードバック
2. **判定音** (`PlayJudgment`): 判定結果に応じた音

### HitSoundLibrary

複数の音源をランダムまたはラウンドロビンで再生してマンネリ防止。

### Hold tick 判定音の抑制

Hold 維持中に毎拍 PerfectPlus 判定が発火するため判定音が連打になる問題が発生。  
**MIN_SOUND_INTERVAL_MS = 32.0ms** のスロットリングで抑制:

```csharp
double nowMs = Time.unscaledTimeAsDouble * 1000.0;
if (nowMs - _lastJudgmentSoundMs >= MIN_SOUND_INTERVAL_MS) {
    HitSoundPlayer.Instance?.PlayJudgment(j);
    _lastJudgmentSoundMs = nowMs;
}
```

### AudioMixer

`AudioMixer` は Unity ネイティブクラスのためプログラム作成不可。  
手動セットアップ手順:
1. Assets > Create > Audio > Audio Mixer → MainAudioMixer
2. Master / Music / SFX グループ作成
3. Volume を Expose: MasterVolumeDb / MusicVolumeDb / SfxVolumeDb
4. AudioVolumeBinder に割り当て

未割当でも `AudioSource.volume` 直接制御でフォールバック動作。

### 関連ファイル

- `Scripts/Audio/HitSoundPlayer.cs`
- `Scripts/Audio/HitSoundLibrary.cs`
- `Scripts/Audio/AudioVolumeBinder.cs`

---

## 10.5 スコア履歴画面 [Phase 2-5]

### 設計

SQLite に保存されたプレイ記録を一覧・詳細表示する。

### 機能

- **フィルタ**: 難易度別 / ランク別 / ソート順 (最新順 / スコア順)
- **一覧**: 楽曲名、難易度、スコア、ランク、プレイ日時
- **詳細**: 判定数内訳 (PP/P/Gr/Gd/M/Fast/Late)、コンボ、バッジ (AllPerfect+ 等)

### 主要 API

```csharp
var history = await RepositoryService.Instance.PlayRecords.GetHistoryAsync(limit: 100);
```

### 関連ファイル

- `Scripts/UI/HistoryController.cs`
- `Scripts/UI/HistoryDetailView.cs`
- `Scripts/Save/SqlitePlayRecordRepository.cs`

---

## 10.6 PauseMenu 本格実装 [Phase 2-6]

### 設計

GamePlay 中の Esc キーで Pause。3つの操作を提供:

| ボタン | 動作 |
|---|---|
| Resume | 3秒カウントダウン後に再開 |
| Restart | 同一楽曲・同一設定で再プレイ (ParameterStore.GetCurrent) |
| Quit | SongSelect または Title に戻る |

### Restart の実装

```csharp
// Result 遷移せず再度 GamePlay を起動
var p = ParameterStore.GetCurrent<GamePlayParameters>();
SceneRouter.Instance.GoTo(SceneId.GamePlay, p, TransitionStyle.None);
```

`GetCurrent` (= `GetPending` が1度呼ばれた後も保持) を使い  
同一パラメータで再ロード。

### 関連ファイル

- `Scripts/Game/PauseMenu.cs`

---

## 10.7 シーン遷移演出 [Phase 2-7]

### TransitionStyle 一覧

| Style | 演出 | 主な使用場面 |
|---|---|---|
| None | 即時切替 | Bootstrap → Title |
| FadeBlack | 0.3s 黒フェード | 通常遷移 |
| FadeWhite | 0.3s 白フェード | Result 画面など |
| SlideLeft | 左スライド | 戻る操作 |
| SlideRight | 右スライド | 進む操作 |
| FastCut | 即時切替 (PVP 高速) | PVP シーン間 |
| GameStart | GamePlay 専用演出 | 選曲 → GamePlay |

### LoadingOverlay

HeavyLoad シーン (GamePlay / SongSelect) の遷移中に進捗バーを表示:

```
_Persistent シーン: LoadingOverlayCanvas (Sort 998)
  └─ SceneRouter が進捗 0→100% を更新
```

### SceneAutoLoader (Editor 拡張)

Unity Editor の ▶ ボタンは現在開いているシーンを起動する。  
`SceneAutoLoader.cs` が ▶ 押下時に Bootstrap.unity を強制的に開き、  
常に正しい起動シーケンス (Bootstrap → _Persistent → Title) を保証する。

```
▶ 押下 → SceneAutoLoader → Bootstrap.unity を開く → Play 開始
■ 押下 → EditorPrefs から元シーンを復元
```

### 関連ファイル

- `Scripts/Scene/TransitionFx.cs`
- `Scripts/Scene/LoadingOverlay.cs`
- `Scripts/Scene/TransitionStyle.cs`
- `Assets/_Project/Editor/SceneAutoLoader.cs`
