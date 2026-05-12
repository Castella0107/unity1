# 9. Phase 1 確定事項

Phase 1 (2026年5月完成) で行った設計判断を記録する。  
「なぜこの設計にしたか」の議論と採用理由を残す。

---

## 9.1 JudgmentSystem 責務分割 — Aggregator 分離

### 議論

**案A (採用前)**: JudgmentSystem が判定計算・スコア集計・イベント発火を全て担当。

**案B (採用)**: JudgmentSystem は判定計算とイベント発火のみ。  
スコア集計は PlayProgressAggregator に分離。

### 採用理由

- サーバー再利用: Phase 4 でサーバー側がリプレイ検証を行う際、  
  Aggregator だけを動かせばスコアを再計算できる
- テスト容易性: Aggregator は Pure C# なので JudgmentSystem なしで単体テスト可能
- 責務の明確化: JudgmentSystem = 「今のノーツに対して何の判定か」の判定機、  
  Aggregator = 「その判定でスコアがいくつになるか」の集計機

### 最終形

```
JudgmentSystem
  └─ OnJudged event (Judgment j, double deltaMs)
       └─→ PlayProgressAggregator.ApplyHit / ApplyMiss / ApplyTick
       └─→ JudgmentEffectsController (エフェクト)

PlayProgressAggregator
  └─ Snapshot() → PlayProgressSnapshot
```

### 関連ファイル

- `Scripts/Game/JudgmentSystem.cs`
- `Scripts/Domain/Play/PlayProgressAggregator.cs`
- `Scripts/Domain/Play/PlayProgressSnapshot.cs`

---

## 9.2 Result 伝達 — Record / View 分離

### 議論

**案A**: GamePlay シーンから Result シーンへ直接スコア値を渡す (int のみ)。

**案B (採用)**: PlayRecord (永続化用) と PlayResultView (表示用 ViewModel) を分離。  
GamePlay → ParameterStore → Result の伝達に PlayResultView を使用。

### 採用理由

- PlayRecord は SQLite に保存する完全記録 (リプレイパス含む)
- PlayResultView は Result 画面に表示するために必要な最小限のデータ  
  (ベストスコア比較、新記録フラグなど)
- 将来 PVP でも同じ Result 画面を使えるよう ViewModel として抽象化

### 最終形

```
GamePlayController.TriggerResultAsync
  └─ PlayRecordFactory.Create(snapshot, ...) → PlayRecord
  └─ RepositoryService.PlayRecords.SaveAsync(record)
  └─ PlayResultView { Record, SongTitle, IsNewBest, BestBefore }
  └─ ResultParameters { View, SourceGamePlayParameters }
  └─ SceneRouter.GoTo(Result, resultParams)
```

### 関連ファイル

- `Scripts/Domain/Play/PlayRecord.cs`
- `Scripts/Domain/Play/PlayResultView.cs`
- `Scripts/Domain/Play/PlayRecordFactory.cs`
- `Scripts/Domain/Scene/ISceneParameters.cs` (ResultParameters)

---

## 9.3 シーン遷移 — SceneRouter フル機能

### 議論

**案A**: SceneManager を各 Controller が直接呼ぶ (シンプル)。

**案B (採用)**: SceneRouter Singleton が全遷移を一元管理。  
FadeBlack 演出・ParameterStore・_isTransitioning フラグを統合。

### 採用理由

- 遷移中の二重呼び出し防止 (`_isTransitioning` フラグ)
- 遷移前後の演出 (FadeBlack) を一元化
- シーンパラメータ受け渡し (ParameterStore) との統合
- PVP での高速切り替え (FastCut) 等の TransitionStyle 拡張が容易

### Phase 2 デバッグで追加したロジック

シーン二重ロードバグ発生後に **Pre-evict ステップ**を追加:

```csharp
// 同名シーンが既にロードされていたら先に Unload
for (int i = SceneManager.sceneCount - 1; i >= 0; i--) {
    var s = SceneManager.GetSceneAt(i);
    if (s.name == targetLabel && s.isLoaded) {
        var evict = SceneManager.UnloadSceneAsync(s);
        while (evict != null && !evict.isDone) yield return null;
    }
}
// その後 LoadSceneAsync
```

### 最終形のフロー

1. ParameterStore.SetPending
2. FadeBlack FadeIn (0.3s)
3. LoadingOverlay 表示 (HeavyLoad 時のみ)
4. **Pre-evict**: 同名シーン Unload
5. LoadSceneAsync(target, Additive)
6. SetActiveScene
7. 旧シーン全 Unload (_Persistent 以外)
8. AggressiveCleanup (GC + UnloadUnused)
9. LoadingOverlay 非表示
10. FadeBlack FadeOut (0.3s)

### 関連ファイル

- `Scripts/Scene/SceneRouter.cs`
- `Scripts/Domain/Scene/ParameterStore.cs`
- `Scripts/Scene/TransitionFx.cs`

---

## 9.4 ローカル永続化 — SQLite

### 議論

**案A**: PlayerPrefs のみ使用 (シンプル)。

**案B**: JSON ファイル保存。

**案C (採用)**: sqlite-net-pcl を使った SQLite。

### 採用理由

- プレイ記録の集計クエリ (ベストスコア、ランキング) が SQL で容易
- Phase 4 でサーバー連携した際の同期が容易 (レコード単位)
- PlayerPrefs は小規模設定値のみ使用、構造化データは SQLite

### テーブル設計

| テーブル | 主な用途 |
|---|---|
| Plays | 全プレイ記録 (スコア、ランク、判定数) |
| PersonalBests | 楽曲+難易度別のベスト |
| DeviceProfiles | デバイス別オフセット設定 |
| PerSongOffsets | 楽曲別オフセット微調整 |
| KeyValue | マイグレーション状態等の汎用KV |

### Repository パターン

Domain は `IPlayRecordRepository` / `IOffsetRepository` インターフェースのみ知る。  
本番: `SqlitePlayRecordRepository`  
テスト: `InMemoryPlayRecordRepository` (高速・ファイル不要)

### 関連ファイル

- `Scripts/Save/SqlitePlayRecordRepository.cs`
- `Scripts/Save/SqliteOffsetRepository.cs`
- `Scripts/Save/RepositoryService.cs`
- `Scripts/Domain/Save/IPlayRecordRepository.cs`

---

## 9.5 オフセット適用フロー — 3層 + NAudio

### 3層オフセット設計

```
合計オフセット = AppOffset + PerSongOffset + DeviceProfile.Offset

AudioConductor.JudgmentTimeMs
  = SongTimeMs
    - AppOffsetSettings.JudgmentOffsetMs
    - PerSongOffset.JudgmentOffsetMs

AudioConductor.VisualTimeMs
  = SongTimeMs
    - AppOffsetSettings.VisualOffsetMs
```

| 層 | 設定箇所 | 目的 |
|---|---|---|
| AppOffset | Config 画面全体設定 | デバイス共通の遅延補正 |
| PerSongOffset | Config 楽曲別設定 | 楽曲ごとの微調整 |
| DeviceProfile | Config Devices タブ | Bluetooth/有線切替で即座に変わる補正 |

### NAudio によるデバイス監視

Windows の音声出力デバイス変更を NAudio で検知し、  
対応する DeviceProfile に自動切替:

```
WindowsAudioDeviceMonitor.DeviceChanged
  └─→ DeviceProfileService.SwitchToDevice(deviceId)
       └─→ RepositoryService.SetActiveProfile(profileId)
            └─→ AudioConductor.ApplyAppOffsets(profile.Offsets)
```

### 基準クロック

AudioSettings.dspTime を唯一の基準クロックとして使用。  
Time.time / audioSource.time は判定に使用禁止。

### 関連ファイル

- `Scripts/Audio/AudioConductor.cs`
- `Scripts/Audio/Devices/WindowsAudioDeviceMonitor.cs`
- `Scripts/Audio/Devices/DeviceProfileService.cs`
- `Scripts/Domain/Save/AppOffsetSettings.cs`

---

## 9.6 リプレイ記録

### 設計方針

**入力イベントのみ記録、判定はサーバー再計算** (チート対策)。

記録内容:
- `ReplayHeader`: バージョン、CRC32 チェックサム
- `ReplayMetadata`: SongId、Difficulty、ChartHash、オフセット値
- `ReplayResult`: スコア、ランク、判定数 (クライアント計算値、参考値)
- `ReplayInputEvent[]`: 全入力 (lane, timeMs, isDown) を時系列

### 形式

バイナリ + VarInt エンコード + CRC32 末尾チェックサム。  
`ReplayHeader.CurrentVersion` でバージョン管理、旧バージョン互換分岐可能。

### ChartHash

`ChartParser.ParseChart` が chart.json 本文から SHA-256 で自動計算。  
同一譜面のリプレイ照合に使用。

### 関連ファイル

- `Scripts/Domain/Replay/ReplayData.cs` (Header/Metadata/Result/InputEvent)
- `Scripts/Domain/Replay/ReplayEncoder.cs`
- `Scripts/Domain/Replay/ReplayDecoder.cs`
- `Scripts/Domain/Replay/JudgmentRunner.cs`
- `Scripts/Save/ReplayStorage.cs`

---

## 9.7 テスト戦略 — Layer 1+2+4

### 4層テスト戦略

| Layer | 内容 | 採用 |
|---|---|---|
| 1 | Domain Unit Test (Pure C#) | ◎ 採用 |
| 2 | SQLite 統合テスト (TempSqliteDb) | ◎ 採用 |
| 3 | Play Mode テスト | × 不採用 (手動確認で代替) |
| 4 | 自動プレイ検証 (JudgmentRunner) | ◎ 採用 |

Play Mode テスト (Layer 3) は起動コストが高い割に得られる確信が Layer 1/2/4 の組み合わせと大差ないため不採用。

### 自動プレイ検証 (Layer 4) の基準

```csharp
// 全 PerfectPlus → 1,000,000 ぴったり
var chart  = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();
var replay = ReplayBuilder.AllPerfectPlus(chart);
var snap   = new JudgmentRunner().Run(chart, replay);
Assert.AreEqual(1_000_000, snap.CurrentScore);
```

### Phase 1 完成時のテスト数

**169 EditMode テスト全 Pass**  
19ファイル構成 (Domain / SQLite 統合 / Replay 往復 / 自動プレイ検証)

### TotalNotes = 採点イベント数

ScoringEventCounter.Count が ChartBuilder / ChartParser / JudgmentRunner の  
**全3箇所で同値**を返すことで理論値 1,000,000 を保証。

Tap: 1イベント  
Hold: head(1) + tick(拍ごと) + tail(1)
