# 13. 実装者向けクイックリファレンス

実装時の「どこを触るか」クイックリファレンス。  
詳細は `docs/handbook.md` を参照。

---

## 13.1 新しい楽曲を追加する

```
StreamingAssets/Songs/{songId}/
├── audio.ogg     (or .mp3 / .wav)
├── jacket.png    (任意)
├── meta.json     (楽曲メタ情報)
└── charts/
    └── extra.json  (難易度ごとに別ファイル)
```

**meta.json 最小構成**:
```json
{
  "songId": "my_song",
  "title": "楽曲タイトル",
  "artist": "アーティスト",
  "bpm": 160.0,
  "durationMs": 120000,
  "sectors": [
    {"id": 0, "name": "S1", "endMs": 24000},
    {"id": 4, "name": "S5", "endMs": 120000}
  ]
}
```

**chartHash 注意**: 非 hex 文字列はエラーの原因。省略すれば ChartParser が SHA-256 を自動計算。

---

## 13.2 新しいシーンを追加する

1. `Assets/_Project/Scenes/` に `.unity` 作成
2. File > Build Settings にシーンを追加
3. `Domain/Scene/SceneId.cs` に enum 値追加
4. `Scene/SceneRouter.cs` の `SceneNames` 辞書に追加
5. 重いシーンなら `HeavyLoadScenes` にも追加
6. 遷移パラメータが必要なら `Domain/Scene/ISceneParameters.cs` にクラス追加

---

## 13.3 新しい設定タブを追加する

1. Config.unity のタブパネルに UI 追加
2. `*TabController.cs` に `LoadSettings()` / `SaveSettings()` 実装
3. PlayerPrefs キー命名規則: `"{TabName}_{Key}"` (例: `"Audio_MasterVolume"`)
4. `PlayerPrefs.Save()` を忘れずに呼ぶ

---

## 13.4 シーン間でパラメータを渡す (ParameterStore)

**渡す側 (SceneRouter.GoTo の第2引数)**:
```csharp
var p = new GamePlayParameters { SongId = "my_song", Difficulty = "extra" };
SceneRouter.Instance.GoTo(SceneId.GamePlay, p, TransitionStyle.FadeBlack);
```

**受け取る側 (遷移先の Start)**:
```csharp
var p = ParameterStore.GetPending<GamePlayParameters>();
if (p == null) { /* フォールバック */ }
```

**再起動用 (同一パラメータで再プレイ)**:
```csharp
var p = ParameterStore.GetCurrent<GamePlayParameters>();
```

**既存の実装クラス**:

| クラス | フィールド |
|---|---|
| GamePlayParameters | SongId, Difficulty, HiSpeed, JudgeOffset, VisualOffset, Modifier |
| ResultParameters | View (PlayResultView), SourceGamePlayParameters |

---

## 13.5 主要 API 早見表

### SceneRouter
```csharp
SceneRouter.Instance.GoTo(SceneId.GamePlay, params, TransitionStyle.FadeBlack);
bool busy = SceneRouter.Instance.IsTransitioning;
```

### RepositoryService
```csharp
var repo = RepositoryService.Instance;
if (!repo.IsReady) return;  // async 初期化完了まで待つ
await repo.PlayRecords.SaveAsync(record);
var best = await repo.PlayRecords.GetBestAsync("song_id", "extra");
await repo.Offsets.SaveAppSettingsAsync(settings);
```

### AudioConductor
```csharp
// オフセット適用 (StartSong より前)
_conductor.ApplyAppOffsets(settings);
_conductor.ApplyPerSongOffset(perSongOffset);

// 再生
_conductor.StartSong(clip, prerollSec: 2.0);

// 時刻取得 (毎フレーム)
double judgeMs  = _conductor.JudgmentTimeMs;  // 判定ウィンドウ用
double visualMs = _conductor.VisualTimeMs;    // ノーツスクロール用

// 制御
_conductor.Pause(); _conductor.Resume(0.5); _conductor.Stop();
```

### JacketBackgroundController
```csharp
JacketBackgroundController.Instance.SetJacket("song_id");
JacketBackgroundController.Instance.SetFallback();
JacketBackgroundController.Instance.SetCanvasEnabled(false);  // GamePlay 中必須
```

### ComboDisplay
```csharp
_comboDisplay.SetCombo(aggregator.CurrentCombo);
// 0 を渡すと非表示
// 50/100/250/500/1000 でゴールドフラッシュ
```

---

## 13.6 よくある間違いと対処

| 間違い | 対処 |
|---|---|
| Time.time で判定 | AudioSettings.dspTime 基準の SongTimeMs を使う |
| async void で例外を握り潰す | try-catch で囲み Debug.LogError |
| Coroutine の前に SetActive(true) 忘れ | SetActive(true) → StartCoroutine の順序 |
| PowerShell で BOM 付き UTF-8 書き出し | `New-Object System.Text.UTF8Encoding $false` を使う |
| Material.SetFloat を OnDestroy でリセット忘れ | OnDestroy で初期値に戻す |
| _Persistent 以外に EventSystem を置く | _Persistent の1個だけ残して他は削除 |

---

## 13.7 テスト追加のテンプレート

### Domain Unit テスト
```csharp
[TestFixture]
public class NewFeatureTests {
    [Test]
    public void Description_WhenCondition_ExpectedResult() {
        var chart = new ChartBuilder().WithBpm(120).AddTap(LaneRef.Lane0, 1000).Build();
        var snap  = new JudgmentRunner().Run(chart, ReplayBuilder.AllPerfectPlus(chart));
        Assert.AreEqual(1_000_000, snap.CurrentScore);
    }
}
```

### SQLite 統合テスト
```csharp
[TestFixture]
public class NewRepositoryTests {
    TempSqliteDb _db;
    [SetUp] public async Task SetUp() {
        _db = new TempSqliteDb();
        _repo = new SqlitePlayRecordRepository();
        await _repo.InitializeAsync(_db.FilePath);
    }
    [TearDown] public void TearDown() => _db.Dispose();
}
```

---

## 13.8 Phase 3 実装開始前チェックリスト

- [ ] 169 EditMode テスト全 Pass
- [ ] GamePlay → Result 遷移成功
- [ ] docs/handbook.md を最新に更新
- [ ] docs/troubleshooting.md に Phase 2 教訓を追記
- [ ] SceneRouter の SceneNames に PVP シーン群を追加
- [ ] SceneId.cs に PVP enum 値を追加
- [ ] ISceneParameters に PVP パラメータクラスを追加
