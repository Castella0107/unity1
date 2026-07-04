# 処理ボトルネック監査・修正項目リスト (2026-07-04)

対象: PVP本体 (Assets/_Project/Scripts) + ChartEditor (Tools/ChartEditor) + Domain/ChartEdit(自動生成系)。
コード精読ベースの静的監査。優先度 = 発生頻度 × 1回あたりコスト。

---

## A. ゲームプレイ・ホットパス (60fps 常時経路)

### A-1【高】GameHud: 毎フレーム無条件の TMP テキスト再代入 + 文字列アロケーション
- `Assets/_Project/Scripts/UI/HUD/GameHud.cs:130-142` (PVP時は 147-153, 189 も)
- 判定カウント×5・スコア(`ToString("N0")`)・レート(`ToString("F2")+"%"`)を値が変わらなくても毎フレーム `.text` 代入 → TMP メッシュ再構築 ×7〜8/フレーム + GC ゴミ。
- **修正**: 前回値キャッシュで変化時のみ更新。文字列生成は `TMP_Text.SetText(format, ...)` のゼロアロケーション版へ。

### A-2【高】JudgmentEngine: 全ノート O(n) 走査を毎フレーム実行
- `Assets/_Project/Scripts/Domain/Judgment/JudgmentEngine.cs:100, 117`(実体 `ChartDataNoteSource.cs:78-84`)
- 期限切れタップ検出が全レーン全タップを毎フレーム訪問(ソート済みなのにポインタ前進なし)。ホールド頭のオートミス判定も全ホールド走査。加えて L100 は iterator でフレーム毎ヒープ確保。
- **修正**: レーン別「次に期限切れになる未ヒットノート」前進インデックスを導入し O(新規期限切れ数) に。ホールドも進行ポインタ管理。
- ⚠️ **注意**: 判定順序が変わるとスコアパリティ(K/Goサーバー・golden vectors)を壊しうる。判定結果・処理順は bit-perfect 維持を検証必須(ParityVectors 再実行)。

### A-3【中】JudgmentEngine: 毎フレーム `new List<LaneRef>(...)` 確保
- `JudgmentEngine.cs:128`
- アクティブホールド有無に関わらず ProcessTime 毎に List 新規確保。
- **修正**: フィールドバッファ再利用 or `_activeHoldByLane.Count == 0` で早期スキップ。パリティ影響なし(確保方法のみの変更)。

### A-4【中】GamePlayController: デバッグ時刻テキストの毎フレーム string.Format
- `Assets/_Project/Scripts/Game/GamePlayController.cs:152-153`
- 毎フレーム `string.Format` + TMP 再構築。値が毎フレーム変わるため変化検出では救えない。
- **修正**: デバッグフラグでガード or 更新間引き(0.25〜0.5s)。

### A-5【低中】判定エフェクト: ヒット毎に PlayerPrefs.GetInt
- `Assets/_Project/Scripts/Game/JudgmentEffectsController.cs:56` (実体 `JudgmentEffectStyle.cs:14-17`)
- **修正**: セッション開始時+設定変更時に読んでフィールドキャッシュ。

### A-6【低】NoteScroller/HoldNote: ScrollSpeedTimeline.VisualPos の冗長二分探索
- `NoteScroller.cs:140,169,180` / `HoldNoteController.cs:31-33`
- フレーム内で visualMs 不変なのにノート毎に二分探索。speed イベント多用譜面のみ効く。
- **修正**: フレーム先頭で1回計算して配布。

✅ クリーン確認済: NoteScroller スポーン(ポインタ前進)、NotePool/ParticlePool(プーリング済)、ComboDisplay(変化検出済)、BeatLineScroller(二分探索+プール)、Replay/AutoPlay入力(カーソル前進)、GetComponent/Find/Debug.Log のホットパス混入なし。

---

## B. UI・ネットワーク・セーブ (画面遷移・ロード)

### B-1【高】曲選択: カーソル移動毎に4難易度の譜面をフルパース
- `Assets/_Project/Scripts/UI/SongSelect/SongSelectController.cs:552-566` + `Game/ChartLoader.cs:51-65`
- Level(int) 1個の表示のために譜面 JSON 全体を UnityWebRequest で毎回読込&フルパース×4。キャッシュなし。カーソル連打で GC/CPU スパイク連発。
- **修正**: meta.json に難易度別 Level を持たせて一括読み(推奨)。または ChartLoader に LRU キャッシュ + SelectSong デバウンス。

### B-2【高】サーバー楽曲同期: N+1 直列通信 + メインスレッド同期 I/O
- `Assets/_Project/Scripts/Network/Api/ServerSongLibrary.cs:123-150, 174-235`
- 曲毎に GET /songs/{id} → 譜面DL を直列 await。SongSelect は同期完了までリスト非表示。さらに `File.ReadAllBytes`+SHA-256 がメインスレッド同期実行。
- **修正**: 同時数制限付き `Task.WhenAll` 並列化。ハッシュ計算/ディスク読込は `Task.Run` 退避。曲メタ先行表示+譜面遅延取得。

### B-3【中】曲リスト起動: meta を1曲ずつ直列 await
- `SongSelectController.cs:268-274` + `ChartLoader.cs:174-192`
- **修正**: `Task.WhenAll` 並列化 or メタ索引ファイル一括読み(B-1 と同時解決可)。

### B-4【中】曲選択ジャケット: 毎回ダウンロード + Texture2D リーク
- `SongSelectController.cs:536-550`
- キャッシュなし・旧テクスチャ Destroy なしで VRAM が増え続ける。(背景側 JacketBackgroundController は LRU 済で問題なし)
- **修正**: 既存 `JacketLoader`(LRU+破棄あり)を流用。

### B-5【中】履歴画面: ベスト×ById・曲名×meta の N+1 直列ロード
- `Assets/_Project/Scripts/UI/History/HistoryController.cs:163-188`
- **修正**: ById はまとめて1クエリ、meta 解決は並列化/索引化。

### B-6【中】Config データタブ: メインスレッドで全ファイル再帰走査
- `Assets/_Project/Scripts/UI/Config/DataTabController.cs:120-150, 282-293` + `Save/ReplayStorage.cs:86-97`
- `async Task` 署名だが実体は全部同期。タブを開くたびフリーズ。リプレイの count/size を別々にフルスキャン。
- **修正**: `Task.Run` 退避 + 1回のスキャンで count/size 同時算出 + 結果キャッシュ。

### B-7【中】PVP 提出: リプレイ同期読込 + Base64 化をメインスレッドで
- `Assets/_Project/Scripts/Network/Api/PvpResultBridge.cs:46-47`
- **修正**: 既存 `ReplayStorage.ReadAsync` 使用 + Base64 化を `Task.Run` へ。

### B-8【低中】リスト全行一括 Instantiate(仮想化なし) + 選択毎の強制レイアウト再構築
- `SongSelectController.cs:280-299` / `HistoryController.cs:357-365, 386-394, 439`
- **修正**: 可視域のみ生成する仮想化/プーリング。曲数・履歴件数が増えたら着手。

### B-9【低】HiSpeed/Modifier キー連打毎に PlayerPrefs.Save (ディスクフラッシュ)
- `Assets/_Project/Scripts/UI/SongSelect/PlayOptionsController.cs:43-59`
- **修正**: setter は値のみ、Save() は画面離脱時に1回。

### B-10【低】AuthManager: プロパティ毎に PlayerPrefs 参照
- `Assets/_Project/Scripts/Network/Api/AuthManager.cs:31-49`
- **修正**: メモリキャッシュ + 書込時のみ同期。

✅ クリーン確認済: SQLite層(async接続・使い回し)、ApiClient(非同期・ブロッキングなし)、MatchSocketClient(バックグラウンド受信+バッファ再利用)、進捗送信0.5sスロットル、ReplayStorage(非同期FileStream)、メインスレッド `.Result/.Wait()` なし。

---

## C. ChartEditor + 譜面自動生成 (Domain/ChartEdit)

⚠️ Domain 修正の正本: `Assets/_Project/Scripts/Domain/ChartEdit/` 側で直し `sync_domain.py` で Tools 側へ複製。エディタ UI 系は Tools 側直接。

### C-1【高】音声解析(BPM検出/Onset解析/自動配置)が全部メインスレッド同期 → UI 数秒フリーズ
- `Tools/ChartEditor/.../ChartEdit/EditorController.cs:1387, 1430, 1500`
- BeatDetector/OnsetAnalyzer は Unity 非依存なのにボタンハンドラ内で同期実行。
- **修正**: `GetData` だけメインで行い、解析は `await Task.Run(...)`。進捗表示付き推奨。

### C-2【高】バッチ BPM 検出がスキャンコルーチン内で同期実行
- `Tools/ChartEditor/.../ChartEdit/MusicFolderImporter.cs:99, 197`
- 曲数ぶんメインスレッドが繰り返し固まる。
- **修正**: C-1 と同じく解析を `Task.Run` へ。

### C-3【高】あらゆる編集で全タイムライン UI を Destroy→再生成
- `EditorController.cs:1156` (`AfterChartChanged`→`Rebuild`) + `Rendering/TimelineRenderer.cs:66, 224, 349`
- 1ノーツ置くだけで全ノーツ+全格子線 GameObject を破棄再生成。差分更新の仕組み(`_views`/`AddOrUpdate`/`Remove`)が実装済みなのに未使用。
- **修正**: EditCommand が変更したノーツ ID のみ差分更新。格子線は BPM/timesig 変更時のみ再構築。

### C-4【高】拍/スナップ格子線を曲全長ぶん一括生成(仮想化なし)
- `TimelineRenderer.cs:221, 346`
- 細かいスナップで数千本。ズーム1ノッチ毎に C-3 経由で全部作り直し。
- **修正**: 可視範囲+マージンのみ生成する仮想化 or プーリング。

### C-5【中】Overview テクスチャ: 再生中プレイヘッド移動だけで毎回フル再描画
- `Rendering/OverviewRenderer.cs:111` + `EditorController.cs:715`
- 約10Hz で `Color32[w*h]` 確保 + NpsHistogram 全ノーツ再計算 + SetPixels32/Apply。
- **修正**: 静的部分(背景+NPS+ノーツ)をキャッシュし、プレイヘッドは別 Image オーバーレイで移動のみ。

### C-6【中】AutoChartGenerator: レーン割当が O(ノーツ数²)
- `Assets/_Project/Scripts/Domain/ChartEdit/AutoChartGenerator.cs:436, 470` (正本=Domain側)
- ノーツ1個置く毎に busy リスト全走査。数千ノーツで数百万回。
- **修正**: busy をレーン別・時刻昇順保持し末尾/二分探索参照に。⚠️ 決定的シード仕様なので出力譜面が変わらないこと(同一入力→同一出力)を確認。

### C-7【中】DoAutoPlaceBeats が検出済み結果を捨てて再解析
- `EditorController.cs:1427-1430`
- `_cachedDetect` があるのに GetData+BeatDetector.Detect を再実行(全曲 STFT 二重実行)。
- **修正**: クリップ一致時は `_cachedDetect` 再利用(OnsetAnalyzer 側は既にキャッシュ済みの同パターン)。

### C-8【中】RefreshSelection が O(view数 × ノーツ数)
- `TimelineRenderer.cs:402-416`
- **修正**: id→NoteData 辞書化 or view に NoteData 参照保持。

### C-9【低中】解析前ダウンサンプリングなし
- `Domain/ChartEdit/OnsetAnalyzer.cs:88` / `BeatDetector.cs:42`
- 帯域上限 16kHz なのに 44.1kHz フルレートで STFT。2:1 デシメーションでフレーム数半減=C-1 の待ち時間短縮。
- ⚠️ 検出結果(オンセット時刻/BPM)が微変動しうるので、適用するなら BPM 検出精度の実測確認とセットで。

### C-10【低】RankTempoCandidates: 掃引ステップ毎に HashSet 新規確保 (~360回)
- `Domain/ChartEdit/BeatDetector.cs:135-139, 195`
- **修正**: HashSet 使い回し(Clear) or ビットセット化。

### C-11【低】clip.GetData の全サンプルコピー(20MB級)を操作毎に重複確保
- `EditorController.cs:1384, 1427, 1497` / `MusicFolderImporter.cs:194` / `WaveformRenderer.cs:81`
- **修正**: クリップ単位でモノ化済み float[] を1回取得しキャッシュ共有。

✅ クリーン確認済: FFT は本物の radix-2(素朴DFTではない)、STFT 単一パス+バッファ再利用、Undo は差分コマンド方式、波形テクスチャはズーム/スクロールで再構築しない設計、保存は Ctrl+S 時のみ、再生 Update は O(1)。

---

## 実装状況 (2026-07-04 対応)

✅ **実装済み** (このセッションで対応):
- **A-1** GameHud 変化検出化 (`GameHud.cs`): カウント/スコア/レート/VSバー/セクター菱形を前回値キャッシュで変化時のみ `.text` 更新。曲跨ぎ用に Initialize でキャッシュリセット。
- **A-3** JudgmentEngine List 確保除去 (`JudgmentEngine.cs`): `_activeLaneScratch` 再利用 + アクティブホールド0本で早期 return。**ParityVectors で baseline と bit 完全一致を実証** (real_castella の対goldens差分は 2026-07-01 ホールド終端仕様変更由来で本変更とは無関係)。
- **A-4** デバッグ時刻テキスト間引き (`GamePlayController.cs`): 0.1秒間引き。
- **C-1/C-2** 音声解析バックグラウンド化 (`EditorController.cs`/`MusicFolderImporter.cs`): `clip.GetData` のみメインスレッド、`ToMono`/`Detect`/`Analyze`/`RankTempoCandidates` を `Task.Run`。`_analysisBusy` で多重起動防止。バッチは coroutine yield で完了待ち。
- **C-6** AutoChartGenerator O(n²) レーン割当 (`AutoChartGenerator.cs`): `BusyMap` でレーン別バケット化。**走査集合は元々 lane フィルタ済のため RNG 消費順・出力とも完全不変**(決定性テスト維持)。
- **C-7** DoAutoPlaceBeats のキャッシュ再利用: `_cachedDetect` 流用で二重解析を回避。
- **C-10** BeatDetector HashSet 再利用 (`BeatDetector.cs`): `[ThreadStatic]` で ~360回/解析の確保を除去(バッチ並列でもスレッドセーフ)。
- **B-1** 曲選択の譜面フルパース回避 (`SongSelectController.cs`): (songId|difficulty)→Level のセッションキャッシュで再選択時の再パースを排除。
- **B-4** ジャケットのリーク修正: LRU+破棄付き `JacketLoader` を流用、OnDestroy で ClearCache。選択変更時の取り違え防止に captured ガード追加。
- Domain 変更 (A-3 除く C-6/C-10) は `sync_domain.py` で ChartEditor へ同期済み。`RhythmGame.Domain` の dotnet ビルド 0エラーで検証。

✅ **実装済み (2回目・中〜低優先の追加対応)**:
- **A-5** 判定エフェクト Prefs キャッシュ (`JudgmentEffectsController.cs`): スタイルを OnEnable で1回読みキャッシュ。判定毎の PlayerPrefs.GetInt を排除。
- **A-6** VisualPos 冗長二分探索 (`ScrollSpeedTimeline.cs`): 1要素メモ (構築後不変=純関数) でフレーム内の同一 visualMs 再探索を除去。
- **B-2** サーバー楽曲同期 (`ServerSongLibrary.cs`): 曲詳細取得と譜面DLを同時数8のバッチ並列化、SHA-256/read/write を Task.Run 退避。
- **B-3** 曲リスト meta (`SongSelectController.cs`): 直列→同時数16バッチ並列 (ApplySort で並べ替えるため順序不問)。
- **B-5** 履歴 N+1 (`HistoryController.cs`): ベスト ById 取得・曲名 meta 解決をバッチ並列化。
- **B-6** Config データタブ (`DataTabController.cs`/`ReplayStorage.cs`): 全ファイル再帰走査を Task.Run 退避 + count/size を1回走査に統合。
- **B-7** PVP 提出 (`PvpResultBridge.cs`): リプレイ読込+Base64 化を Task.Run 退避。
- **B-9** PlayerPrefs.Save 連打 (`PlayOptionsController.cs`): HiSpeed/Modifier setter の Save を除去、Close/OnDestroy/SongSelect離脱で Flush 集約。
- **B-10** AuthManager (`AuthManager.cs`): トークン類を起動時1回読みのメモリキャッシュ化 (書込時のみ同期)。
- **C-3/C-4** タイムライン差分更新 (`TimelineRenderer.cs`): ノーツを差分更新 (BuildNotesDiff) 化+テンポ署名で格子線/マーカーの再構築を必要時のみに。1ノーツ編集で全 GameObject を破棄再生成しなくなった。
- **C-5** Overview 分離 (`OverviewRenderer.cs`): 静的部分(背景+NPS+ノーツ)をキャッシュし、再生中はコピー+プレイヘッド列のみ描画。NpsHistogram 再計算と全ノーツ再描画を10Hz経路から除去。
- **C-8** RefreshSelection 辞書化 (`TimelineRenderer.cs`): O(view×notes)→O(view+notes)。
- **C-11** GetData キャッシュ共有 (`EditorController.cs`): モノ化サンプルをクリップ単位キャッシュし detect/analyze で共有。
- Domain 変更 (A-6/C-8/C-11) は sync 済み。`RhythmGame.Domain` 再ビルド 0エラー。

🎯 **フレームレート 60/144 対応** (`DisplayTabController.cs`): Config→Display→FPS で 60/120/144/240/Unlimited を選択可 (既存機構)。既定を 144 に変更し、シーン遷移でシーン別 QualitySettings が vSync を上書きしても保たれるよう毎ロード再適用。判定は音声クロック基準+`FrameMs=1000/60` 固定なので 144 化してもスコアパリティ不変。※vSync ON 時はモニタ refresh に従うため、狙った 60/144 を出すには vSync OFF (既定) のまま FPS を選ぶ。

⏸ **見送り継続** (要実測 or 別ブランチ):
- **C-9** 解析前ダウンサンプリング: 検出結果(BPM/オンセット時刻)が変わりうるため BPM精度の実測とセットで別途。
- **A-2** JudgmentEngine 全ノート O(n) 走査のポインタ化: 効果大だが判定順序変更でスコアパリティを壊すリスク。golden 22本+real_castella の再検証を伴う単独ブランチで実施すべき。
- **B-8** リスト仮想化: 曲数・履歴件数が実運用で問題化したら着手 (現状は Instantiate 一括だが件数が小さい)。

⚠️ **要ビルド確認**: PVP本体は Unity 再ビルド、ChartEditor は独立リポ (BuildEditorScene → BuildWin64) で再ビルド後に実機確認。ChartEditor は `async void` 導入のため Play/Player 実行時の解析ボタン動作を目視確認推奨。

## 推奨着手順

| 順 | 項目 | 理由 |
|---|---|---|
| 1 | A-1, A-3, A-4 | プレイ中の恒常負荷。パリティ無関係で安全に即修正可 |
| 2 | C-1, C-2, C-7 | 自動生成の実測フェーズ直前。フリーズ解消は体感効果最大 |
| 3 | B-1 (+B-3), B-4 | 曲選択の操作感に直結。JacketLoader 流用と meta 化で低リスク |
| 4 | C-3, C-4 | エディタ編集レスポンス。差分更新機構が既にあるので配線のみ |
| 5 | A-2 | 効果大だがスコアパリティ検証(golden 22本+real_castella 3本)必須のため単独ブランチで |
| 6 | B-2, B-5, B-6, B-7, C-5, C-6, C-8 | 中優先。順次 |
| 7 | 残りの低優先 (A-5, A-6, B-8〜10, C-9〜11) | 実測で問題化したら |
