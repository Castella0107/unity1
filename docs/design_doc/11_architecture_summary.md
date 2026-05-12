# 11. アーキテクチャ要約

Phase 1+2 完成時点のシステム構造を要約する。  
詳細は `docs/architecture/architecture.md` を参照。

---

## 11.1 3層アーキテクチャ

```
+------------------------------------------+
|  Pure C# Layer (Domain)                  |
|  Unity 非依存、サーバー再利用可           |
|  JudgmentEngine / ScoreCalculator        |
|  PlayProgressAggregator / BpmTimeline    |
|  ReplayEncoder / ReplayDecoder           |
|  IPlayRecordRepository (IF のみ)         |
+------------------------------------------+
         ↑ 使用
+------------------------------------------+
|  Save Layer                              |
|  SQLite 実装 / InMemory 実装             |
|  SqlitePlayRecordRepository              |
|  SqliteOffsetRepository                  |
|  RepositoryService (DontDestroyOnLoad)   |
+------------------------------------------+
         ↑ 使用
+------------------------------------------+
|  Unity Layer (MonoBehaviour)             |
|  AudioConductor / JudgmentSystem         |
|  NoteScroller / GameInputController      |
|  各シーン Controller                     |
+------------------------------------------+
         ↑ 調整・常駐
+------------------------------------------+
|  Persistent Services (_Persistent シーン)|
|  SceneRouter / HitSoundPlayer            |
|  JacketBackgroundController              |
|  BeatGridController / AudioVolumeBinder  |
|  TransitionFx / LoadingOverlay           |
|  EventSystem                             |
+------------------------------------------+
```

**設計原則**:
- Domain Layer は Unity に依存しない。Phase 4 でサーバー側に移植可能。
- Save Layer は Repository パターンで Domain から分離。テスト時は InMemory に切り替え。
- Persistent Services は最小限に。過剰な Singleton は避ける。

---

## 11.2 シーン構成と Build Index

| Build Index | シーン | 役割 |
|---|---|---|
| 0 | Bootstrap | 起動 → _Persistent ロード → Title 遷移 |
| 1 | _Persistent | 常駐サービス (DontDestroyOnLoad) |
| 2 | Title | メインメニュー |
| 3 | SongSelect | 選曲 (HeavyLoad) |
| 4 | GamePlay | プレイ (HeavyLoad) |
| 5 | Result | リザルト |
| 6 | Config | 設定 (7タブ) |
| 7 | History | プレイ履歴 |

HeavyLoad シーン (SongSelect / GamePlay) は遷移中に LoadingOverlay を表示。

---

## 11.3 Canvas Sort Order 一覧

| Sort Order | Canvas | 表示シーン | 役割 |
|---|---|---|---|
| -1000 | JacketBackgroundCanvas | 全シーン | ぼかし背景 |
| -500 | BeatGridCanvas | GamePlay のみ | BPM グリッド |
| 0 | 各シーン Canvas | 各シーン | HUD / UI |
| 998 | LoadingOverlayCanvas | 遷移中 | 進捗バー |
| 999 | TransitionFxCanvas | 遷移中 | FadeBlack |

---

## 11.4 _Persistent シーン 常駐 GameObject 一覧

| GameObject | 主なコンポーネント | 役割 |
|---|---|---|
| SceneRouter | SceneRouter, AudioListener | シーン遷移管理, 永続 AudioListener |
| HitSoundPlayer | HitSoundPlayer, AudioSource | タップ音・判定音 |
| JacketBackgroundCanvas | JacketBackgroundController, Canvas | ぼかし背景 (Sort -1000) |
| BeatGridCanvas | BeatGridController, Canvas | BPM グリッド (Sort -500) |
| TransitionFxCanvas | TransitionFx, Canvas | FadeBlack 演出 (Sort 999) |
| LoadingOverlayCanvas | LoadingOverlay, Canvas | 進捗バー (Sort 998) |
| AudioVolumeBinder | AudioVolumeBinder | AudioMixer ← PlayerPrefs 同期 |
| EventSystem | EventSystem, InputSystemUIInputModule | UI イベント処理 |

---

## 11.5 Domain Layer 主要クラス一覧

### 判定・スコア系

| クラス | 役割 |
|---|---|
| Judgment | PerfectPlus / Perfect / Great / Good / Miss enum |
| JudgmentWindow | 判定ウィンドウ ±ms 定義 |
| HoldJudgmentTracker | Hold ノーツの Head/Tick/Tail 判定 |
| BpmTimeline | テンポ変化対応 BPM・拍間隔計算 |
| PlayProgressAggregator | リアルタイム スコア/コンボ 集計 |
| ScoreCalculator | スコア計算 (1,000,000 理論値) |
| ScoringEventCounter | 採点イベント総数カウント |

### データモデル系

| クラス | 役割 |
|---|---|
| NoteData | Tap / Hold / FxTap / FxHold |
| ChartData | 譜面全体 (Notes / Events / Sectors) |
| PlayRecord | 1プレイの完全記録 |
| PlayResultView | Result 画面用 ViewModel |

### リプレイ系

| クラス | 役割 |
|---|---|
| ReplayData | Header / Metadata / Result / InputEvents |
| ReplayEncoder | → byte[] シリアライズ |
| ReplayDecoder | byte[] → ReplayData |
| ReplayInputBuffer | プレイ中の入力録画 |
| JudgmentRunner | リプレイ再実行 → スコア計算 |

### 永続化 Interface

| クラス | 役割 |
|---|---|
| IPlayRecordRepository | プレイ記録 CRUD |
| IOffsetRepository | オフセット設定 CRUD |

---

## 11.6 GamePlay シーン MonoBehaviour 関係

```
GamePlayController (オーケストレーター)
  ├─ ChartLoader.LoadMetaAsync / LoadChartAsync / LoadAudioAsync
  ├─ AudioConductor.StartSong(clip, prerollSec)
  ├─ NoteScroller.Initialize(chart)
  ├─ JudgmentSystem.Initialize(chart, meta, comboBorder)
  └─ GameHud.Initialize(meta, chart, isPvP)

GameInputController
  └─ OnLaneDown / OnLaneUp → JudgmentSystem

JudgmentSystem
  ├─ OnJudged event → JudgmentEffectsController / JudgmentDisplay
  └─ AdvanceTo(JudgmentTimeMs) [毎フレーム]

JudgmentEffectsController
  ├─ JudgmentParticlePool.Spawn
  ├─ HitSoundPlayer.PlayJudgment
  └─ Update: ComboDisplay.SetCombo(aggregator.CurrentCombo)

AudioConductor (DontDestroyOnLoad)
  ├─ SongTimeMs (dspTime 基準)
  ├─ JudgmentTimeMs (= SongTimeMs - JudgmentOffset)
  └─ VisualTimeMs (= SongTimeMs - VisualOffset)
```

---

## 11.7 音声タイミングの基準クロック

```
AudioSettings.dspTime  ← Unity audio thread の絶対基準
    ↓
AudioConductor.SongTimeMs
    = (dspTime - _dspStartTime) × 1000

JudgmentTimeMs = SongTimeMs - JudgmentOffset
VisualTimeMs   = SongTimeMs - VisualOffset

JudgmentSystem.AdvanceTo(JudgmentTimeMs)  [毎フレーム]
GameInputController callback → JudgmentTimeMs を即取得
```

**Time.time / audioSource.time は判定に使用禁止。**
