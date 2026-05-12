# 開発者ハンドブック

Unity リズムゲームプロジェクトの開発者向けレシピ集。
「○○を変えたい時、何を読んでどこを触るか」を素早く解決するためのリファレンス。

**最終更新: 2026-05-11 (Phase 4c 完成時点)**

関連ドキュメント:
- アーキテクチャ図: `docs/architecture/architecture.md`
- 設計書: `OneDrive/デスクトップ/rhythm_game_design.pdf`

---

## 目次

1. [プロジェクト概要](#1-プロジェクト概要)
2. [開発環境セットアップ](#2-開発環境セットアップ)
3. [レシピ集 — よくある変更](#3-レシピ集--よくある変更)
   - 3.11 [新しい IInputSource 実装を追加する](#311-新しい-iinputsource-実装を追加する)
   - 3.12 [StageInitializer を使って新モードを追加する](#312-stageinitializer-を使って新モードを追加する)
4. [テスト戦略](#4-テスト戦略)
   - 4.6 [JudgmentEngine の単体テストを書く](#46-judgmentengine-の単体テストを書く)
5. [主要 API リファレンス](#5-主要-api-リファレンス)
6. [設計判断の記録](#6-設計判断の記録)
7. [既知の制約・落とし穴](#7-既知の制約落とし穴)
8. [Phase 別実装スコープ](#8-phase-別実装スコープ)
9. [付録 A: ファイル早見表](#付録-a-ファイル早見表)
10. [付録 B: 用語集](#付録-b-用語集)

---

## 1. プロジェクト概要

### 1.1 ゲーム概要

Windows 向け Unity 製リズムゲーム。
- **ボタン構成**: 4K メインレーン(0-3) + FX 2レーン(fxL/fxR) — SDVX 参考
- **描画**: チュウニズム風 3D レーン、URP + Bloom
- **判定**: PerfectPlus (±16ms) / Perfect (±33ms) / Great (±50ms) / Good (±83ms) / Miss
- **スコア理論値**: 1,000,000 点、全 Perfect+ で ぴったり
- **モード**: ソロプレイ + オンライン PVP 対戦 (Phase 3 以降)

### 1.2 アーキテクチャ 3層

| 層 | 内容 | 特徴 |
|---|---|---|
| **Domain (Pure C#)** | 判定エンジン・スコア・リプレイ | Unity 非依存、サーバー再利用可 |
| **Unity Layer** | MonoBehaviour・シーン固有処理 | Domain を利用 |
| **Persistent Services** | _Persistent シーン常駐 | DontDestroyOnLoad でシーン跨ぎ |

### 1.3 採用ライブラリ

- **sqlite-net-pcl**: PlayRecord / Offset の SQLite 永続化
- **Newtonsoft.Json**: ChartParser での JSON デシリアライズ
- **NAudio** (NuGetForUnity): Windows 音声デバイス変更検知
- **TextMeshPro**: 全テキスト描画
- **Input System (New)**: キー入力

---

## 2. 開発環境セットアップ

### 2.1 必要環境

- Unity 6 (6000.3.14f1) + URP
- Windows x86_64 ターゲット
- Visual Studio / Rider (C# 9.0)

### 2.2 初回起動 — Editor で Play する場合

1. プロジェクトを Unity で開く
2. **Tools > Scene AutoLoader > Toggle** で AutoLoader が有効 (デフォルト ON) であることを確認
3. どのシーンを開いた状態でも ▶ ボタンを押す
4. `SceneAutoLoader.cs` が `Bootstrap.unity` を自動で開いて Play 開始
5. Bootstrap → _Persistent → Title の順で正しくロードされる

> **なぜ必要か**: `▶` は現在開いているシーンから起動するため、_Persistent が先にロードされず SceneRouter.Instance 等が null になる。

### 2.3 AudioMixer の手動セットアップ

AudioMixer はプログラムから作成不可 (Unity 制約)。初回のみ手動作業:

1. `Assets/_Project/Audio/` フォルダ内で右クリック → Create > Audio > Audio Mixer → `MainAudioMixer`
2. Mixer ウィンドウで `Master` 下に `Music`・`SFX` 子グループを追加
3. 各グループの `Volume` パラメータを Expose:
   - `MasterVolumeDb` (Master グループ)
   - `MusicVolumeDb` (Music グループ)
   - `SfxVolumeDb` (SFX グループ)
4. `_Persistent.unity` でアサイン:
   - `AudioVolumeBinder._mainMixer` → `MainAudioMixer`
   - `HitSoundPlayer._sfxGroup` → `MainAudioMixer/SFX`
5. `GamePlay.unity`:
   - `AudioConductor` の `AudioSource.Output` → `MainAudioMixer/Music`

> **未設定でも動く**: AudioVolumeBinder / HitSoundPlayer はフォールバックで `AudioSource.volume` 直接制御。

### 2.4 テスト実行

```
Window > General > Test Runner > EditMode タブ > Run All
```

現状 **169 テスト全 Pass**。テストを追加したら Run All で確認すること。

### 2.5 テスト楽曲を用意する

初回のみ:
```
Tools > Generate Test Audio
```
`StreamingAssets/Songs/test_song/audio.wav` (65秒サイン波) が生成される。
`test_song_1/2/3` も同様に生成。

---

## 3. レシピ集 — よくある変更

### 3.1 新しい楽曲を追加する

```
StreamingAssets/Songs/{songId}/
├── audio.ogg        (or .mp3 / .wav — ChartLoader が順番に試行)
├── jacket.png       (任意、JacketLoader が読み込む)
├── meta.json        (楽曲メタ情報)
└── charts/
    └── extra.json   (難易度ごとに別ファイル)
```

**meta.json の必須フィールド** (ChartParser.ParseMeta):
```json
{
  "songId": "my_song",
  "title": "曲名",
  "artist": "アーティスト",
  "bpm": 160.0,
  "durationMs": 120000,
  "sectors": [
    {"id": 0, "name": "S1", "endMs": 24000},
    {"id": 1, "name": "S2", "endMs": 48000},
    {"id": 2, "name": "S3", "endMs": 72000},
    {"id": 3, "name": "S4", "endMs": 96000},
    {"id": 4, "name": "S5", "endMs": 120000}
  ]
}
```

**charts/extra.json の必須フィールド** (ChartParser.ParseChart):
```json
{
  "version": 1,
  "songId": "my_song",
  "difficulty": "extra",
  "level": 15,
  "chartHash": "abcdef0123456789...",  // 省略可: ChartParser が SHA-256 自動計算
  "totalNotes": 500,
  "events": [
    {"type": "bpm", "timeMs": 0, "bpm": 160.0, "multiplier": 1.0}
  ],
  "notes": [
    {"id": 1, "type": "tap",    "lane": "0",   "timeMs": 1000, "durationMs": 0},
    {"id": 2, "type": "hold",   "lane": "1",   "timeMs": 2000, "durationMs": 500},
    {"id": 3, "type": "fxTap",  "lane": "fxL", "timeMs": 3000, "durationMs": 0},
    {"id": 4, "type": "fxHold", "lane": "fxR", "timeMs": 4000, "durationMs": 1000}
  ]
}
```

> **chartHash の注意**: `"abc123test"` のような非 hex 文字列を入れると
> `HexStringToBytes` が FormatException を投げる。
> 省略すれば `ChartParser` が JSON 本文から SHA-256 を自動計算する。

SongSelect に表示するには `SongSelectController` でのリスト生成ロジックを確認。

---

### 3.2 新しいシーンを追加する

1. `Assets/_Project/Scenes/` に `.unity` 作成
2. File > Build Settings に追加
3. `Domain/Scene/SceneId.cs` に enum 値追加
4. `Scene/SceneRouter.cs` の `SceneNames` 辞書に追加:
   ```csharp
   { SceneId.MyNewScene, "MyNewScene" },
   ```
5. 重いシーン (Assets 読み込みあり) なら `HeavyLoadScenes` にも追加
6. 遷移パラメータが必要なら `Domain/Scene/ISceneParameters.cs` に追加:
   ```csharp
   public sealed class MyNewSceneParameters : ISceneParameters {
       public string Foo { get; set; }
   }
   ```
7. Controller クラスを `UI/{SceneName}/` に作成

---

### 3.3 判定エフェクトを追加・変更する

判定発火のパイプライン:

```
JudgmentSystem.OnJudged
  └── JudgmentEffectsController.HandleJudged
        ├── JudgmentParticlePool.Spawn()     ← 3D パーティクル
        ├── HitSoundPlayer.PlayJudgment()    ← 判定音
        └── (JudgmentTextPopup は無効化済み)
```

新しい演出を追加するには `JudgmentEffectsController.HandleJudged` に処理を追加する。
エフェクトスタイルを設定から変えられるようにするなら `JudgmentEffectStyle.cs` と連携。

---

### 3.4 Config タブに設定を追加する

1. Config.unity の該当タブパネルに UI 追加
2. 該当 `*TabController.cs` の `LoadSettings()` / `SaveSettings()` に追加:
   ```csharp
   // 読み込み
   _mySlider.value = PlayerPrefs.GetFloat("Audio_MyParam", 0f);
   // 保存
   PlayerPrefs.SetFloat("Audio_MyParam", _mySlider.value);
   PlayerPrefs.Save();
   ```
3. PlayerPrefs キー命名規則: `"{TabName}_{Key}"` (例: `"Game_JudgmentEffectStyle"`)
4. 変更を即反映させる場合は `onValueChanged` に反映処理を登録

---

### 3.5 Input System にアクションを追加する

1. `Assets/_Project/Input/InputSystem_Actions.inputactions` を Editor で開く
2. 対象 ActionMap に Action 追加 (例: `Gameplay` Map の新しいレーン)
3. Binding を追加 (Keyboard / Gamepad)
4. 対象 Controller の `Awake()`:
   ```csharp
   var map = _inputAsset.FindActionMap("Gameplay", throwIfNotFound: true);
   _myAction = map.FindAction("MyAction", throwIfNotFound: true);
   ```
5. `OnEnable` / `OnDisable` で `_myAction.Enable()` / `.Disable()`

---

### 3.6 SQLite テーブルを変更する

1. `Save/Tables/PlayRow.cs` (or 対象 Row クラス) にフィールド追加
2. `SqlitePlayRecordRepository.cs` の CREATE TABLE クエリにカラム追加:
   ```csharp
   "new_column INTEGER DEFAULT 0"  // DEFAULT で旧DBとの互換性確保
   ```
3. `Save/RowMapper.cs` の変換ロジック更新
4. `Domain/Play/PlayRecord.cs` にも対応フィールドを追加
5. スキーマ変更が大きい場合は InitializeAsync 内でマイグレーション:
   ```csharp
   await db.ExecuteAsync("ALTER TABLE Plays ADD COLUMN new_column INTEGER DEFAULT 0");
   ```
6. テスト追加: `Tests/EditMode/SQLiteIntegration/SqliteOffsetRepositoryTests.cs` 参考

---

### 3.7 リプレイ形式を変更する

1. `Domain/Replay/ReplayData.cs` の Header / Metadata / Result / InputEvent 変更
2. `ReplayHeader.CurrentVersion` をインクリメント
3. `ReplayEncoder.cs` の書き出し処理を更新
4. `ReplayDecoder.cs` の読み込み処理を更新 (旧バージョン互換が必要なら分岐)
5. 往復テスト更新: `Tests/EditMode/ReplayDecoderTests.cs`
6. `Crc32` チェックサムが末尾に付くので改竄・破損を検出できる

---

### 3.8 シーン間で値を渡す (ParameterStore)

**渡す側 (遷移元)**:
```csharp
var p = new GamePlayParameters { SongId = "my_song", Difficulty = "extra" };
SceneRouter.Instance.GoTo(SceneId.GamePlay, p, TransitionStyle.FadeBlack);
// GoTo の第2引数に渡すと ParameterStore.SetPending が自動で呼ばれる
```

**受け取る側 (遷移先 Start)**:
```csharp
var p = ParameterStore.GetPending<GamePlayParameters>();
if (p == null) { /* フォールバック */ }
```

**再起動・リトライで再利用する場合**:
```csharp
// GetPending は1回取り出すと Current に移動する
// 後から Current を再取得可能
var p = ParameterStore.GetCurrent<GamePlayParameters>();
```

**存在確認**:
```csharp
if (ParameterStore.HasPending<ResultParameters>()) { ... }
```

---

### 3.9 Persistent Service を追加する

1. MonoBehaviour 派生クラス作成:
   ```csharp
   public class MyService : MonoBehaviour {
       public static MyService Instance { get; private set; }
       void Awake() {
           if (Instance != null) { Destroy(gameObject); return; }
           Instance = this;
           DontDestroyOnLoad(gameObject);
       }
   }
   ```
2. `_Persistent.unity` の Hierarchy に GameObject 追加してスクリプトをアタッチ
3. Awake 実行順を考慮 (SceneRouter が先に Instance 確立される必要がある場合は Script Execution Order で調整)

---

### 3.10 HUD に新しい表示を追加する

| 種別 | 場所 | 更新タイミング |
|---|---|---|
| 毎フレーム更新の数値 | `GamePlay.unity` の Canvas 内、`GameHud.cs` に追加 | `Update()` |
| 一過性アニメーション | 専用 GameObject + Coroutine | 判定イベント等のタイミング |
| 全シーン共通表示 | `_Persistent` シーンに Persistent Service | 任意 |

---

### 3.11 新しい IInputSource 実装を追加する

**用途例**: AutoPlay (CPU 自動再生)、ネットワーク入力 (PVP のリモートプレイヤー)、
デモモード (譜面の自動演奏プレビュー)。

```csharp
// 1. IInputSource を実装する Pure C# クラスを作成
//    場所: Assets/_Project/Scripts/Domain/Input/
public class AutoPlayInputSource : IInputSource
{
    // IInputSource が要求するイベント
    public event Action<LaneRef, double> OnLaneDown;
    public event Action<LaneRef, double> OnLaneUp;

    readonly List<NoteData> _notes;
    int _nextIdx;

    public AutoPlayInputSource(ChartData chart)
        => _notes = chart.Notes.OrderBy(n => n.TimeMs).ToList();

    // 2. AudioConductor.JudgmentTimeMs を渡して毎フレーム Advance する
    public void Advance(double songTimeMs)
    {
        while (_nextIdx < _notes.Count && _notes[_nextIdx].TimeMs <= songTimeMs)
        {
            var note = _notes[_nextIdx++];
            OnLaneDown?.Invoke(note.Lane, note.TimeMs);
            if (note.DurationMs > 0)
                OnLaneUp?.Invoke(note.Lane, note.TimeMs + note.DurationMs);
        }
    }
}
```

**手順**:

1. `Domain/Input/` に `IInputSource` を実装する Pure C# クラスを作成
2. `OnLaneDown` / `OnLaneUp` イベントを正しいタイミングで発火する
3. GamePlay Controller の `Start()` で `JudgmentSystem.Initialize` に渡す:
   ```csharp
   var autoInput = new AutoPlayInputSource(_chart);
   _judgment.Initialize(_chart, _meta, autoInput, Judgment.Good);
   ```
4. ライブ入力を使わない場合は `_liveInput.enabled = false` で無効化
   (ReplayPlaybackController が模範実装)
5. Controller の `Update()` で `Advance(conductor.JudgmentTimeMs)` を毎フレーム呼ぶ

**注意**: `IInputSource` は Pure C# なので Unity 型 (`LaneId`) は受け取れない。
`GameInputController` が境界で `(LaneRef)(int)lane` のキャストを担うパターンを踏襲する。

---

### 3.12 StageInitializer を使って新モードを追加する

**用途例**: チュートリアルモード、観戦モード、PVP モード (Spectator)。

```csharp
// 新規 MonoBehaviour: TutorialController.cs
public class TutorialController : MonoBehaviour
{
    [SerializeField] AudioConductor _conductor;
    [SerializeField] NoteScroller   _scroller;
    [SerializeField] JudgmentSystem _judgment;
    [SerializeField] GameHud        _hud;

    async void Start()
    {
        // 1. ParameterStore のフラグで自分が動くか判定
        var prm = ParameterStore.GetPending<GamePlayParameters>()
               ?? ParameterStore.GetCurrent<GamePlayParameters>();
        if (prm == null || !prm.IsTutorial) { gameObject.SetActive(false); return; }

        // 2. チャート / メタ / オーディオ読み込み
        var meta  = await ChartLoader.LoadMetaAsync(prm.SongId);
        var chart = await ChartLoader.LoadChartAsync(prm.SongId, prm.Difficulty);
        var clip  = await ChartLoader.LoadAudioAsync(prm.SongId);

        // 3. StageInitializer で 3D 共通初期化
        //    - JacketBackground を隠す
        //    - BeatGrid バインド
        //    - NoteScroller 初期化
        //    - GameHud 初期化
        StageInitializer.BindStageVisuals(_conductor, chart, meta, _scroller, _hud);

        // 4. モード固有の IInputSource を渡す
        var tutorialInput = new TutorialInputSource(chart);  // 独自実装
        _judgment.Initialize(chart, meta, tutorialInput, Judgment.Good);

        _conductor.StartSong(clip, prerollSec: 2.0);
    }

    void OnDestroy()
    {
        // 6. 終了時に必ず UnbindStageVisuals を呼ぶ
        StageInitializer.UnbindStageVisuals();
    }
}
```

**チェックリスト**:

| ステップ | 内容 |
|---|---|
| 1 | `ParameterStore` の識別フラグを `GamePlayParameters` に追加 (`IsTutorial`, `IsSpectator` 等) |
| 2 | 新規 `XxxController : MonoBehaviour` を `Game/` に作成 |
| 3 | `Start()` 先頭で自分が動くか判定し、不要なら `gameObject.SetActive(false)` |
| 4 | `StageInitializer.BindStageVisuals()` を呼ぶ (3D 共通初期化) |
| 5 | `JudgmentSystem.Initialize()` で適切な `IInputSource` を渡す |
| 6 | `OnDestroy()` と完了ハンドラ両方で `StageInitializer.UnbindStageVisuals()` を呼ぶ |
| 7 | `GamePlay.unity` の Hierarchy に新 Controller の GO を追加し、Inspector を配線する |

---

### 3.13 サーバー側で新譜面を認識させる

**用途**: サーバーで新譜面のリプレイ検証を有効化したいとき。

**結論**: クライアント (Unity) 側と **完全に同じ場所に置くだけ**。サーバーは起動時にスキャンして自動登録する。
Assets/StreamingAssets/Songs/{songId}/
├── meta.json
└── charts/
├── normal.json
├── hard.json
└── extra.json

**手順**:

1. クライアント側の手順 (§3.1 新しい楽曲を追加する) を完了する
2. サーバーを再起動する: `cd Server; dotnet run --project RhythmGame.Server`
3. 起動ログで以下を確認:
[ChartRepo] Indexed: my_song/extra hash=AB12CD34EF567890...
[ChartRepo] Total indexed: N charts from C:\Users...\StreamingAssets\Songs

**仕組み**:

`FileSystemChartRepository` は起動時に以下の処理を行う:

| ステップ | 処理 |
|---|---|
| 1 | `Assets/StreamingAssets/Songs/` 配下を `Directory.GetDirectories` でスキャン |
| 2 | 各 `{songId}/meta.json` の存在を確認 (なければスキップ) |
| 3 | 各 `{songId}/charts/*.json` を `File.ReadAllBytes` で読み込み |
| 4 | `SHA256.HashData(chartBytes)` で譜面ハッシュを計算 |
| 5 | `Dictionary<string, ChartEntry>` に `hashHex → (songId, difficulty, metaPath, chartPath)` を格納 |

検証時はリプレイ内の `ChartHash` (= クライアントが計算したハッシュ) を渡せば一致する譜面が引ける。

**注意**:

- 譜面 JSON が 1 文字でも変わるとハッシュが変わるので、クライアントとサーバーで **同じファイル内容** を使う必要がある
- サーバーは譜面 JSON を起動時に 1 回だけ読む。譜面を追加・修正した場合はサーバーを再起動すること
- 音声ファイル (`audio.ogg`/`.wav`) はサーバーでは読まないので不要 (検証は譜面 + メタデータだけで完結)

**関連**: [§3.1 新しい楽曲を追加する](#31-新しい楽曲を追加する)

---
## 4. テスト戦略

### 4.1 4層テスト戦略

| Layer | 種別 | 場所 | 採用 |
|---|---|---|---|
| 1 | Domain Unit Test | EditMode | ✅ 採用 |
| 2 | SQLite 統合テスト | EditMode (TempSqliteDb) | ✅ 採用 |
| 3 | Play Mode テスト | — | ❌ 不採用 (手動確認で代替) |
| 4 | 自動プレイ検証 | EditMode (JudgmentRunner) | ✅ 採用 |

現状: **169 テスト (19ファイル)** 全 Pass。

### 4.2 Domain Unit テストの書き方

```csharp
[TestFixture]
public class ScoreCalculatorTests {
    [Test]
    public void AllPerfectPlus_Returns1000000() {
        var chart = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();
        var replay = ReplayBuilder.AllPerfectPlus(chart);
        var snap = new JudgmentRunner().Run(chart, replay);
        Assert.AreEqual(1_000_000, snap.CurrentScore);
    }
}
```

NUnit 使用。GameObjects 不要、Pure C# なので高速。

### 4.3 SQLite 統合テストの書き方

```csharp
[TestFixture]
public class SqlitePlayRecordRepositoryTests {
    TempSqliteDb _db;
    SqlitePlayRecordRepository _repo;

    [SetUp]
    public async Task SetUp() {
        _db   = new TempSqliteDb();
        _repo = new SqlitePlayRecordRepository();
        await _repo.InitializeAsync(_db.FilePath);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();  // WAL/SHM 含め自動削除

    [Test]
    public async Task SaveAndGetBest_WorksCorrectly() {
        var record = /* PlayRecord 生成 */;
        await _repo.SaveAsync(record);
        var best = await _repo.GetBestAsync("song_id", "extra");
        Assert.IsNotNull(best);
    }
}
```

### 4.4 自動プレイ検証 (Layer 4)

```csharp
// 全 PerfectPlus → 1,000,000 ぴったり (これが基準値)
var chart = new ChartBuilder()
    .WithBpm(120)
    .AddTap(LaneRef.Lane0, 1000)
    .AddTap(LaneRef.Lane1, 2000)
    .AddHold(LaneRef.Lane2, 3000, 1000)
    .Build();

var replay = ReplayBuilder.AllPerfectPlus(chart);
var snap   = new JudgmentRunner().Run(chart, replay);

Assert.AreEqual(1_000_000, snap.CurrentScore);
Assert.AreEqual(0, snap.MissCount);
```

`ScoringEventCounter.Count` が ChartBuilder / ChartParser / JudgmentRunner で同値を返すことが理論値保証の根拠。

### 4.5 テストヘルパー

| ヘルパー | 用途 |
|---|---|
| `ChartBuilder` | テスト用 ChartData をメソッドチェーンで生成 |
| `ReplayBuilder.AllPerfectPlus(chart)` | 全 PerfectPlus のリプレイを自動生成 |
| `TempSqliteDb` | テスト用一時 DB ファイル (TearDown で自動削除) |

### 4.6 JudgmentEngine の単体テストを書く

**既存テスト**: `JudgmentRunnerTests.cs` — Phase 4-pre 完成時点で 18 件。
サーバー側判定検証の「正解実装」として機能する。

**基本パターン**:

```csharp
[TestFixture]
public class MyJudgmentTests
{
    // 1. テスト用 ChartData を構築
    static ChartData MakeChart()
    {
        return new ChartBuilder()
            .WithBpm(120)
            .AddTap(LaneRef.Lane0, 1000)
            .AddTap(LaneRef.Lane1, 2000)
            .AddHold(LaneRef.Lane2, 3000, durationMs: 1000)
            .Build();
    }

    // 2. テスト用 ReplayData (入力イベント列) を構築
    static ReplayData MakeReplay_AllPerfectPlus(ChartData chart)
        => ReplayBuilder.AllPerfectPlus(chart);  // 既存ヘルパー

    static ReplayData MakeReplay_MissFirst(ChartData chart)
    {
        // カスタム: Lane0 だけ叩かず他は PerfectPlus
        var builder = new ReplayBuilder(chart);
        builder.SkipLane(LaneRef.Lane0);   // Lane0 を Miss
        builder.HitAllPerfectPlus(LaneRef.Lane1, LaneRef.Lane2);
        return builder.Build();
    }

    [Test]
    public void AllPerfectPlus_Returns1000000()
    {
        var chart  = MakeChart();
        var replay = MakeReplay_AllPerfectPlus(chart);

        // 3. JudgmentRunner.Run(chart, meta, replay) で結果取得
        var meta = SongMetadata.CreateForTest(durationMs: 5000);
        var snap = new JudgmentRunner().Run(chart, meta, replay);

        // 4. PlayProgressSnapshot の各フィールドを assert
        Assert.AreEqual(1_000_000, snap.CurrentScore);
        Assert.AreEqual(0,         snap.MissCount);
        Assert.AreEqual(0,         snap.FastCount + snap.LateCount);
    }

    [Test]
    public void MissFirstNote_ScoreIsLower()
    {
        var chart  = MakeChart();
        var replay = MakeReplay_MissFirst(chart);
        var meta   = SongMetadata.CreateForTest(durationMs: 5000);

        var snap = new JudgmentRunner().Run(chart, meta, replay);

        Assert.Greater(1_000_000, snap.CurrentScore);
        Assert.AreEqual(1, snap.MissCount);
    }
}
```

**検証できる PlayProgressSnapshot フィールド**:

| フィールド | 意味 |
|---|---|
| `CurrentScore` | 最終スコア (0–1,000,000) |
| `PerfectPlusCount` | PerfectPlus 判定数 |
| `PerfectCount` | Perfect 判定数 |
| `GreatCount / GoodCount / MissCount` | 各判定数 |
| `MaxCombo` | 最大コンボ数 |
| `FastCount / LateCount` | 入力が早すぎた / 遅すぎた回数 |

**ReplayBuilder の主なヘルパー**:

| メソッド | 動作 |
|---|---|
| `ReplayBuilder.AllPerfectPlus(chart)` | 全ノーツをぴったりタイミングで入力 |
| `ReplayBuilder.WithOffset(lane, noteId, offsetMs)` | 特定ノーツに意図的なズレを加える |
| `new ReplayBuilder(chart).Build()` | カスタムイベント列を組み立て |

> テストは `Assets/_Project/Tests/EditMode/` に配置。
> `[TestFixture]` クラスを追加するだけで Test Runner が自動認識する。

---

### 4.7 サーバーテスト (xUnit) の書き方

**前提**: サーバー側のテストは `RhythmGame.Server.Tests` プロジェクトに置く。Unity の EditMode テストとは別系統。

**実行コマンド**:

```powershell
cd C:\Users\CaSte\PVP\Server
dotnet test RhythmGame.Server.Tests
```

**4 種類のテストパターン**:

```csharp
// パターン 1: フェイクリポジトリで「未登録ハッシュ」のロジックをテスト
private class EmptyChartRepository : IChartRepository
{
    public Task<(ChartData chart, SongMetadata meta)?> TryGetByHashAsync(string hash)
        => Task.FromResult<(ChartData, SongMetadata)?>(null);
}

[Fact]
public async Task ChartNotRegistered_ReturnsInvalid()
{
    var service = new ReplayValidationService(NullLogger<...>.Instance, new EmptyChartRepository());
    // ...
}

// パターン 2: 実譜面ファイルを読み込んで JudgmentRunner まで通すテスト
[Fact]
public async Task RealChart_EmptyReplay_RecomputesAndMatches()
{
    // テスト cwd は bin/Debug/net10.0 → 5 階層上が PVP ルート
    var songsRoot = Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory(),
        "..", "..", "..", "..", "..",
        "Assets", "StreamingAssets", "Songs"));

    var chartFile = Path.Combine(songsRoot, "test_song", "charts", "extra.json");
    if (!File.Exists(chartFile))
    {
        Assert.True(true, "test_song chart not found, skipping");
        return;
    }
    // ... ハッシュ計算 → リクエスト構築 → service.Validate
}
```

**重要な注意点**:

| 項目 | 注意 |
|---|---|
| **テスト cwd** | `bin/Debug/net10.0` なので相対パスは `..` × 5 で PVP ルート |
| **async Task** | `async void` は xUnit v3 で削除予定 (xUnit1048 警告)。最初から `async Task` で書く |
| **Logger** | `NullLogger<T>.Instance` (Microsoft.Extensions.Logging.Abstractions) を使う |
| **環境差吸収** | テスト譜面が見つからない場合は `Assert.True(true, "skipping"); return;` で graceful skip |
| **Claim 改竄テスト** | 空リプレイ + 不正な Claim で `MismatchReason` の文字列を検証。`Assert.Contains("Score:", response.MismatchReason)` のように部分一致で書く |

**関連**: [§4.2 Domain Unit テストの書き方](#42-domain-unit-テストの書き方)

---
## 5. 主要 API リファレンス

### 5.1 SceneRouter

```csharp
// シーン遷移
SceneRouter.Instance.GoTo(SceneId.GamePlay, parameters, TransitionStyle.FadeBlack);
SceneRouter.Instance.GoTo(SceneId.Result);  // パラメータなし

// 状態確認
bool busy    = SceneRouter.Instance.IsTransitioning;
SceneId cur  = SceneRouter.Instance.CurrentScene;

// 初回起動 (BootstrapController のみ使用)
SceneRouter.Instance.InitialBoot();
```

**TransitionStyle 一覧**:

| 値 | 意味 |
|---|---|
| `None` | 即時切り替え |
| `FadeBlack` | 黒フェード (0.3s) — 通常遷移 |
| `FadeWhite` | 白フェード — Result など |
| `SlideLeft` | 左スライド |
| `SlideRight` | 右スライド |
| `FastCut` | 高速即時 (PVP 用) |
| `GameStart` | GamePlay 特別演出 |

---

### 5.2 ParameterStore

```csharp
// 渡す (SceneRouter.GoTo の第2引数でも可)
ParameterStore.SetPending(new GamePlayParameters { SongId = "x" });

// 受け取る (GetPending は Current にコピーして返す)
var p = ParameterStore.GetPending<GamePlayParameters>();

// 再取得 (Restart/Retry 用)
var p = ParameterStore.GetCurrent<GamePlayParameters>();

// 存在チェック
bool has = ParameterStore.HasPending<ResultParameters>();

// クリア
ParameterStore.Clear();
```

**既存の実装クラス**:

| クラス | フィールド |
|---|---|
| `EmptyParameters` | なし |
| `GamePlayParameters` | SongId, Difficulty, HiSpeed, JudgeOffset, VisualOffset, Modifier |
| `ResultParameters` | View (PlayResultView), SourceGamePlayParameters |

---

### 5.3 AudioConductor

```csharp
// オフセット適用 (StartSong より前に呼ぶ)
_conductor.ApplyAppOffsets(new AppOffsetSettings {
    JudgmentOffsetMs = 20,
    VisualOffsetMs   = 30
});
_conductor.ApplyPerSongOffset(perSongOffset);

// 再生
_conductor.StartSong(clip, prerollSec: 2.0);  // preroll 中は SongTimeMs < 0

// 時刻取得 (毎フレーム)
double songMs     = _conductor.SongTimeMs;
double judgeMs    = _conductor.JudgmentTimeMs;  // 判定ウィンドウ用
double visualMs   = _conductor.VisualTimeMs;    // ノーツスクロール用

// 制御
_conductor.Pause();
_conductor.Resume(prerollSec: 0.5);
_conductor.Stop();

// 状態
bool playing = _conductor.IsPlaying;
bool paused  = _conductor.IsPaused;
```

> **重要**: `Time.time` / `audioSource.time` は使用禁止。
> `AudioSettings.dspTime` 基準の `SongTimeMs` のみ使う。

---

### 5.4 RepositoryService

```csharp
var repo = RepositoryService.Instance;

// 準備完了チェック (async 初期化が終わるまで false)
if (!repo.IsReady) return;

// プレイ記録
await repo.PlayRecords.SaveAsync(playRecord);
var best = await repo.PlayRecords.GetBestAsync("song_id", "extra");
var history = await repo.PlayRecords.GetHistoryAsync(limit: 50);

// オフセット
var appOffset = await repo.Offsets.GetAppSettingsAsync();
var perSong   = await repo.Offsets.GetPerSongOffsetAsync("song_id");
await repo.Offsets.SaveAppSettingsAsync(appOffset);

// リプレイ
string path = await repo.Replays.SaveAsync(playId, replayData, playedAtUnixMs);

// アクティブデバイスプロファイル
var profile = repo.ActiveProfile;
repo.OnActiveProfileChanged += HandleProfileChanged;
```

---

### 5.5 JacketBackgroundController / BeatGridController

```csharp
// ジャケット背景
JacketBackgroundController.Instance.SetJacket("song_id");   // ジャケット表示
JacketBackgroundController.Instance.SetFallback();          // 単色フォールバック
JacketBackgroundController.Instance.SetCanvasEnabled(false); // GamePlay 中は必ず false

// BPM グリッド
BeatGridController.Instance.BindGamePlay(conductor, bpmTimeline);
BeatGridController.Instance.Unbind();
```

---

### 5.6 HitSoundPlayer

```csharp
HitSoundPlayer.Instance.PlayTapClick();             // タップ入力音
HitSoundPlayer.Instance.PlayJudgment(Judgment.Perfect);  // 判定音
```

---

### 5.7 ComboDisplay

```csharp
// JudgmentEffectsController.Update で毎フレーム呼ぶ
_comboDisplay.SetCombo(aggregator.CurrentCombo);
// 0 を渡すと gameObject.SetActive(false) で非表示
// 50/100/250/500/1000 でゴールドフラッシュ
```

---

## 6. 設計判断の記録

### 6.1 Pure C# Domain Layer を最初に作った理由

- Unity 非依存 → PlayMode 不要で高速テスト
- サーバー再利用: Phase 4 でサーバー側が同じ判定エンジンを使い bit-perfect スコア検証
- テスト容易性: 169 テストが Phase 2 デバッグでも回帰防止に効いた

### 6.2 Repository パターンの境界

Domain は `IPlayRecordRepository` / `IOffsetRepository` インターフェースのみ知る。
実装 (SQLite / InMemory) は知らない。

```
Domain (Interface) ← Save (Sqlite実装)
Domain (Interface) ← Tests (InMemory実装)
```

本番: `SqlitePlayRecordRepository`  
テスト: `InMemoryPlayRecordRepository` → 高速、ファイルシステム不要

### 6.3 SceneRouter の Unload-then-Load 順序

旧シーンを先に全 Unload してから新シーンを Additive Load する。
逆順 (先に新 Load、後で旧 Unload) だと同名シーンが一瞬2つ共存し、
`GetSceneByName` が古い方を返してバグる (Phase 2 デバッグで発覚)。

### 6.4 SceneRouter に Pre-evict ステップを追加した理由

GamePlay → GamePlay (再プレイ) 時に同名シーンが2つロードされる問題が発生。
旧シーンを Unload する step 5 のループが `s.name == targetLabel` で両方をスキップしていた。
修正: step 5 より前に対象シーンを先に Unload (Pre-evict) する。

### 6.5 JacketBackground を GamePlay 中に SetCanvasEnabled(false) する理由

_Persistent の JacketBackgroundCanvas (Sort -1000) が表示されていると、
GamePlay の 3D レーン・ノーツが Canvas Overlay に隠れる。
`GamePlayController.Start` で false、`TriggerResultAsync` で true。

### 6.6 3層オフセット (App / PerSong / DeviceProfile)

```
合計 = AppJudgmentOffset + PerSongOffset + DeviceProfile.JudgmentOffset
```

- **AppOffset**: 全体デフォルト (Config で設定)
- **PerSong**: 楽曲ごとの微調整 (Config で楽曲ごとに設定)
- **DeviceProfile**: Bluetooth ↔ 有線切り替えで即座に切り替わる

`AudioConductor.ApplyAppOffsets` + `ApplyPerSongOffset` を StartSong より前に呼ぶこと。

### 6.7 JudgmentTextPopup を無効化した理由 (Phase 2 デバッグ)

`JudgmentEffectsController` が `JudgmentTextPopup.Show(j)` と `JudgmentDisplay.OnJudged` の
両方を発火していた。画面に大きな MISS が2重表示される問題を引き起こした。
`JudgmentEffectsController.HandleJudged` から `_textPopup.Show(j)` 呼び出しを削除して解決。
`JudgmentDisplay.cs` が中央に白フェードアウトで表示する方式に統一。

### 6.8 TotalNotes = 採点イベント数の保証

スコア理論値 1,000,000 点を保証するには:

```
TotalNotes = sum of:
  - Tap: 1
  - Hold: head(1) + ticks(interval ごと) + tail(1)
  - FxTap: 1
  - FxHold: 同上
```

`ScoringEventCounter.Count(notes, bpmTimeline)` が
**ChartBuilder / ChartParser / JudgmentRunner の全3箇所で同値**を返すことが必須。
テスト: `ScoringEventCounterTests.cs`

### 6.9 Persistent Services は最小限に

_Persistent に置くのは本当に全シーンで共通するものだけ:
SceneRouter / RepositoryService (SaveService) / HitSoundPlayer /
JacketBackgroundController / BeatGridController / AudioVolumeBinder / TransitionFx / LoadingOverlay / EventSystem

シーン固有の状態は各 Controller に閉じ込める。過剰な Singleton は依存グラフを複雑にする。

---

## 7. 既知の制約・落とし穴

### 7.1 AudioMixer はプログラムから作れない

`ExtensionOfNativeClass` 属性が必要な Unity ネイティブ型。
`AudioMixer.Create()` に相当する API が存在しない。
**対処**: 2.3 の手順で手動作成。フォールバック設計 (未割当でも動く)。

### 7.2 Coroutine は GameObjects が Active でないと起動できない

```csharp
// ❌ gameObject.SetActive(false) のまま StartCoroutine は失敗
public void Show() {
    StartCoroutine(Animate());   // active でないと黙って失敗
}

// ✅ StartCoroutine より前に SetActive(true)
public void Show() {
    gameObject.SetActive(true);  // 先に active にする
    StartCoroutine(Animate());
}
```

`JudgmentTextPopup.Show` で発覚し実装済み。他のポップアップ系も同様。

### 7.3 async void Start の例外は握り潰される

```csharp
// ❌ 例外が Console に出ないことがある
async void Start() {
    await SomeAsyncMethod();  // ここで例外 → 表示されない
}

// ✅ try-catch でラップ
async void Start() {
    try {
        await SomeAsyncMethod();
    } catch (Exception e) {
        Debug.LogError("[MyClass] Start failed: " + e.Message + "\n" + e.StackTrace);
    }
}
```

`GamePlayController.Start` 等で実装済み。

### 7.4 UTF-8 BOM が JSON パースを壊す

PowerShell 5.1 の `-Encoding utf8` は **BOM 付き** UTF-8 で書き出す。
Newtonsoft.Json がファイル先頭の `﻿` を不正 JSON と判断して `JsonException` を投げる。

**対処 A**: `ChartParser.ParseMeta/ParseChart` の先頭で BOM を除去済み:
```csharp
if (json != null && json.Length > 0 && (int)json[0] == 0xFEFF)
    json = json.Substring(1);
```

**対処 B**: ファイル書き込み時は BOM なし UTF-8 で書く:
```powershell
$enc = New-Object System.Text.UTF8Encoding $false  # false = no BOM
[System.IO.File]::WriteAllText($path, $content, $enc)
```

### 7.5 ChartHash は hex string 限定

`GamePlayController.HexStringToBytes` が純 hex 文字列を前提とする。
`"abc123test"` や `"test-hash"` のような文字列で `FormatException`。

防御層2重:
1. `ChartParser.ParseChart`: 無効な hash なら JSON 本文から SHA-256 で自動計算
2. `GamePlayController.HexStringToBytes`: 非 hex 文字を検出したら warning + 32バイトゼロ返却

**chart.json に書く場合**: 64文字の有効な hex 文字列か省略する。

### 7.6 EventSystem は _Persistent の1つだけ

各シーンに EventSystem を置くと additive load 中に2つが共存し
`"There can be only one active Event System"` エラー。

修正済み: Title/GamePlay/SongSelect/Result/Config/History の EventSystem GO を全削除。
`_Persistent` の EventSystem が唯一の正規インスタンス。

### 7.7 AudioListener の重複

複数の Camera や AudioSource に AudioListener がついていると
`"There are 2 audio listeners"` 警告。

修正済み: `_Persistent` の SceneRouter に AudioListener を配置。
各シーンのカメラには `AudioListenerGuard.cs` をアタッチ
(2つ以上見つかったら `listener.enabled = false` で無効化)。

### 7.8 Material.SetFloat はアセット直接変更

Play モード中に Material の値を変えると Edit モード終了後も残る場合がある。
`OnDestroy` で初期値に戻す:

```csharp
void OnDestroy() {
    if (_material != null) {
        _material.SetFloat(PulseIntensityProp, 0f);
        _material.SetFloat(GridScaleProp, 1f);
    }
}
```

BeatGridController で実装済み。

### 7.9 Hold tick の判定音スパム

Hold 維持中に `ScoringEventCounter` が細かく PerfectPlus tick を発火させるため、
`HitSoundPlayer.PlayJudgment` が連打状態になる。

**暫定対処**: `JudgmentEffectsController` で `MIN_SOUND_INTERVAL_MS = 32.0` (ms) のスロットリング。
抜本解決は Phase 3 予定。

### 7.10 RawImage.material の SerializedProperty 名

```csharp
// ❌ 動かない場合がある
so.FindProperty("material");

// ✅ 正しい内部名
so.FindProperty("m_Material");
```

### 7.11 SceneAutoLoader が有効でないと _Persistent が null

`Tools > Scene AutoLoader > Toggle` で ON/OFF できる。
一時的に特定シーンから単体起動したい場合のみ OFF にすること。
OFF にしたまま GamePlay を単体起動すると `SceneRouter.Instance == null` でクラッシュ。

---

## 8. Phase 別実装スコープ

### Phase 1 完成

- 判定エンジン (Judgment / JudgmentWindow / HoldJudgmentTracker / BpmTimeline)
- スコア計算 (ScoreCalculator / ScoringEventCounter / PlayProgressAggregator)
- 永続化 (IPlayRecordRepository / IOffsetRepository / SQLite / InMemory)
- リプレイ (ReplayEncoder / Decoder / Runner / VarInt / Crc32)
- シーン基盤 (SceneRouter / ParameterStore / TransitionFx)
- 全シーン UI 骨格 (Title / SongSelect / GamePlay / Result / Config 7タブ / History)
- テスト戦略確立 (169 Pass)

### Phase 2 完成

- 背景演出 (JacketBackgroundController — Sort -1000)
- BPM グリッド (BeatGridController — Sort -500)
- 判定エフェクト (JudgmentEffectsController / JudgmentParticlePool / JudgmentColors)
- ヒット音 (HitSoundPlayer / HitSoundLibrary)
- コンボ表示 (ComboDisplay — bump/milestone アニメ)
- 判定テキスト (JudgmentDisplay — フェードアウト + FAST/LATE)
- PauseMenu (PauseMenu.cs — Esc / Resume / Restart / Quit)
- 3D レーン描画 (LaneVisuals / LaneLayout / NoteController / HoldNoteController)
- HUD (GameHud — スコア / コンボ / セクターパネル)
- デバイス監視 (WindowsAudioDeviceMonitor / DeviceProfileService)
- SceneAutoLoader (▶ で Bootstrap 強制起動)
- 各種バグ修正 (EventSystem 重複 / AudioListener 重複 / ChartHash FormatException / シーン二重ロード 等)

### Phase 3 (未着手)

- PVP マッチメイキング (SignalR 接続)
- Matchmaking / PVPPrematch / PVPSongPick / PVPBanPhase / PVPResult / PVPMatchEnd シーン
- Glicko-2 レーティング (Phase 4 Domain に追加)
- オンライン判定同期

### Phase 4-pre 完成 (判定パイプライン統一)

- **IInputSource / LaneRef**: Domain 層の純粋入力インターフェース
- **JudgmentEngine 統一**: JudgmentRunner が JudgmentEngine のラッパーになりロジック1本化
- **テスト**: JudgmentRunnerTests 18件 Pass

### Phase 4a 完成 (リプレイ再生)

- **ReplayPlaybackController**: Replay モードの GamePlay orchestration
- **StageInitializer**: Live / Replay 共通の 3D ステージ初期化を集約
- **ReplayHud**: 再生速度・ステータス表示 (Sort 700)
- **HistoryDetailView**: Replay ボタン + ScrollRect スクロール

### Phase 4 残 (未着手)

- サーバー側判定検証 (ASP.NET Core での Domain 共有)
- Replay Viewer 高機能化 (シーク / 速度段階)

---

## 付録 A: ファイル早見表

`Assets/_Project/Scripts/` からの相対パス。

### Domain (33ファイル)

| ファイル | 役割 |
|---|---|
| `ChartParser.cs` | JSON → ChartData / SongMetadata (Newtonsoft.Json) |
| `ChartValidator.cs` | 譜面データ整合性チェック |
| `Judgment.cs` | PerfectPlus / Perfect / Great / Good / Miss enum |
| `JudgmentWindow.cs` | 判定ウィンドウ ±ms 定義 |
| `NoteData.cs` | Tap / Hold / FxTap / FxHold |
| `Play/BpmTimeline.cs` | テンポ変化対応 BPM 取得 |
| `Play/HoldJudgmentTracker.cs` | Hold の Head/Tick/Tail 判定 |
| `Play/PlayProgressAggregator.cs` | リアルタイム スコア/コンボ 集計 |
| `Play/PlayProgressSnapshot.cs` | Aggregator の瞬間スナップショット |
| `Play/PlayRecord.cs` | 1プレイの完全記録 |
| `Play/PlayRecordFactory.cs` | Snapshot → PlayRecord 生成 |
| `Play/PlayResultView.cs` | Result 画面用 ViewModel |
| `Play/ScoringEventCounter.cs` | 総採点イベント数カウント |
| `Replay/ReplayData.cs` | Header / Metadata / Result / InputEvents |
| `Replay/ReplayEncoder.cs` | → byte[] シリアライズ |
| `Replay/ReplayDecoder.cs` | byte[] → ReplayData デシリアライズ |
| `Replay/ReplayInputBuffer.cs` | プレイ中の入力録画バッファ |
| `Replay/JudgmentRunner.cs` | リプレイ再実行 → PlayProgressSnapshot |
| `Replay/VarInt.cs` | 可変長整数エンコード |
| `Replay/Crc32.cs` | チェックサム計算 |
| `Save/IPlayRecordRepository.cs` | プレイ記録 CRUD インターフェース |
| `Save/IOffsetRepository.cs` | オフセット設定 CRUD インターフェース |
| `Save/InMemoryPlayRecordRepository.cs` | テスト用インメモリ実装 |
| `Save/InMemoryOffsetRepository.cs` | テスト用インメモリ実装 |
| `Save/AppOffsetSettings.cs` | GlobalJudgmentOffset / VisualOffset |
| `Save/DeviceProfile.cs` | デバイス別オフセットプロファイル |
| `Save/PersonalBest.cs` | 個人ベストスコア |
| `Save/PerSongOffset.cs` | 楽曲別オフセット |
| `Scene/ISceneParameters.cs` | GamePlayParameters / ResultParameters / EmptyParameters |
| `Scene/ParameterStore.cs` | シーン間パラメータ一時保存 |
| `Scene/SceneId.cs` | シーン識別 enum |

### Save (13ファイル)

| ファイル | 役割 |
|---|---|
| `SqlitePlayRecordRepository.cs` | SQLite への PlayRecord 読み書き |
| `SqliteOffsetRepository.cs` | SQLite へのオフセット設定読み書き |
| `ReplayStorage.cs` | リプレイファイル保存 (persistentDataPath) |
| `RepositoryService.cs` | SQLite 接続管理 + DontDestroyOnLoad |
| `RowMapper.cs` | Entity ↔ DTO 変換 |
| `PlayerPrefsMigrator.cs` | PlayerPrefs → SQLite マイグレーション |
| `Tables/*.cs` | PlayRow / PersonalBestRow / DeviceProfileRow 等 |

### Audio

| ファイル | 役割 |
|---|---|
| `AudioConductor.cs` | DSP 時刻基準クロック、DontDestroyOnLoad |
| `HitSoundPlayer.cs` | タップ音・判定音 Singleton |
| `HitSoundLibrary.cs` | 複数音源ライブラリ (ランダム/ラウンドロビン) |
| `AudioVolumeBinder.cs` | AudioMixer ← PlayerPrefs 同期 |
| `SineWaveGenerator.cs` | テスト用サイン波 WAV 生成 |
| `Devices/WindowsAudioDeviceMonitor.cs` | Windows 音声デバイス変更検知 |
| `Devices/DeviceProfileService.cs` | デバイス別オフセット切替 |

### Game

| ファイル | 役割 |
|---|---|
| `GamePlayController.cs` | GamePlay オーケストレーター |
| `JudgmentSystem.cs` | 判定計算 + OnJudged イベント発火 |
| `NoteScroller.cs` | ノーツ移動 (VisualTimeMs 基準) |
| `NotePool.cs` | NoteController プール管理 |
| `NoteController.cs` | 個別 Tap ノーツ表示 |
| `HoldNoteController.cs` | Hold ノーツ表示 (伸縮) |
| `LaneVisuals.cs` | レーン 3D 描画 |
| `LaneLayout.cs` | レーン X 座標定数 |
| `GameInputController.cs` | InputSystem → OnLaneDown/Up イベント |
| `JudgmentEffectsController.cs` | パーティクル / 音 / コンボ への判定配信 |
| `JudgmentParticlePool.cs` | 判定パーティクルプール |
| `JudgmentTextPopup.cs` | 大型判定テキスト **(現在 Show 呼び出し停止)** |
| `JudgmentColors.cs` | 判定別カラー定数 |
| `JudgmentEffectStyle.cs` | エフェクトスタイル設定 |
| `ComboDisplay.cs` | コンボ数字 + bump / milestone アニメ |
| `ChartLoader.cs` | StreamingAssets からの JSON / Audio 読み込み |
| `PauseMenu.cs` | Pause UI (Resume / Restart / Quit) |
| `SimpleCalibration.cs` | オフセットキャリブレーション |

### UI

| ファイル | 役割 |
|---|---|
| `Title/TitleController.cs` | カルーセルメニュー |
| `SongSelect/SongSelectController.cs` | 選曲・難易度選択 |
| `Result/ResultController.cs` | スコア / ランク / 判定数 表示 |
| `HUD/GameHud.cs` | スコア / コンボ / セクターパネル更新 |
| `Config/ConfigController.cs` | 設定 7タブ管理 |
| `Config/AudioTabController.cs` | 音量・オフセット設定 |
| `Config/DevicesTabController.cs` | 音声デバイス選択 |
| `Config/DisplayTabController.cs` | 解像度・フレームレート |
| `Config/InputTabController.cs` | キーバインド |
| `Config/AccountTabController.cs` | アカウント情報 |
| `Config/GameTabController.cs` | ゲームプレイ設定 (HiSpeed / エフェクト等) |
| `Config/DataTabController.cs` | データ管理 (エクスポート / 削除) |
| `JacketBackgroundController.cs` | ぼかし背景 Persistent Service |
| `BeatGridController.cs` | BPM グリッド Persistent Service |
| `JudgmentDisplay.cs` | 中央判定テキスト (フェードアウト + FAST/LATE) |
| `HistoryController.cs` | プレイ履歴一覧 |
| `HistoryDetailView.cs` | 履歴詳細パネル |

### Scene

| ファイル | 役割 |
|---|---|
| `SceneRouter.cs` | GoTo / Pre-evict / Unload / AggressiveCleanup |
| `BootstrapController.cs` | _Persistent ロード → InitialBoot() |
| `TransitionFx.cs` | FadeBlack (FadeIn / FadeOut コルーチン) |
| `LoadingOverlay.cs` | HeavyLoad 中の進捗バー表示 |
| `AudioListenerGuard.cs` | AudioListener 重複防止 (enabled = false) |
| `EventSystemGuard.cs` | EventSystem 重複防止 (Destroy) |
| `TransitionStyle.cs` | None / FadeBlack / FadeWhite 等 enum |

### Input

| ファイル | 役割 |
|---|---|
| `GameInputController.cs` | Gameplay ActionMap → OnLaneDown / OnLaneUp |
| `LaneId.cs` | Lane0/1/2/3/FxL/FxR enum |

### Editor

| ファイル | 役割 |
|---|---|
| `SceneAutoLoader.cs` | ▶ 押下時に Bootstrap から起動する Editor 拡張 |
| `TestAudioGenerator.cs` | Tools > Generate Test Audio (65秒サイン波 WAV) |

### Tests (EditMode, 19ファイル, 169テスト)

| ファイル | テスト対象 |
|---|---|
| `JudgmentWindowTests.cs` | 判定ウィンドウ ms 値 |
| `ScoreCalculatorTests.cs` | スコア計算 / ランク算出 |
| `ChartParserTests.cs` | JSON パース |
| `PlayProgressAggregatorTests.cs` | スコア集計ロジック |
| `HoldJudgmentTrackerTests.cs` | Hold 判定 |
| `PlayRecordFactoryTests.cs` | PlayRecord 生成 |
| `ScoringEventCounterTests.cs` | 採点イベント数カウント |
| `JudgmentRunnerTests.cs` | リプレイ再実行 |
| `ReplayEncoderTests.cs` | エンコード往復 |
| `ReplayDecoderTests.cs` | デコード往復 |
| `ParameterStoreTests.cs` | シーン間パラメータ |
| `InMemoryPlayRecordRepositoryTests.cs` | Repository 動作 |
| `InMemoryOffsetRepositoryTests.cs` | Offset Repository 動作 |
| `AppOffsetSettingsTests.cs` | オフセット集計 |
| `SQLiteIntegration/TempSqliteDb.cs` | (ヘルパー) 一時 DB |
| `SQLiteIntegration/SqliteOffsetRepositoryTests.cs` | SQLite 統合 |
| `Helpers/ChartBuilder.cs` | (ヘルパー) テスト用譜面生成 |
| `Helpers/ReplayBuilder.cs` | (ヘルパー) テスト用リプレイ生成 |

---

## 付録 B: 用語集

| 用語 | 意味 |
|---|---|
| **Aggregator** | PlayProgressAggregator — 判定状態をリアルタイムに集計するクラス |
| **BPM Timeline** | テンポ変化を時刻順に管理する Pure C# クラス |
| **Hold tick** | Hold ノーツ維持中の各拍ごとの自動 PerfectPlus 判定 |
| **Pre-evict** | SceneRouter が新シーンを Load する前に既存の同名シーンを Unload するステップ |
| **Scoring event** | スコア計算対象のイベント (Tap=1, Hold=head+ticks+tail) |
| **TotalNotes** | 採点イベント総数 (スコア分母) |
| **PerfectPlus** | 最高判定 (±16ms)、Hold tick は常に PerfectPlus |
| **PerSong offset** | 楽曲ごとの遅延補正 (JudgmentOffset / VisualOffset) |
| **DeviceProfile** | 出力デバイス別オフセット (Bluetooth ↔ 有線で切り替え) |
| **Pending param** | 次シーンに渡すパラメータ (GetPending で取り出すと Current に移動) |
| **Current param** | 直前に取り出されたパラメータ (Restart 用に再取得可能) |
| **HeavyLoad** | SceneRouter で LoadingOverlay を表示するシーン (GamePlay / SongSelect) |
| **Preroll** | StartSong の開始から音が出るまでの無音期間 (SongTimeMs < 0) |
| **dspTime** | `AudioSettings.dspTime` — Unity audio thread の基準クロック |
| **JudgmentTimeMs** | `SongTimeMs - JudgmentOffset` — 判定ウィンドウ計算に使う時刻 |
| **VisualTimeMs** | `SongTimeMs - VisualOffset` — ノーツスクロール位置計算に使う時刻 |

---

*このドキュメントは実コードから生成。Phase 3 実装開始時に更新予定。*
