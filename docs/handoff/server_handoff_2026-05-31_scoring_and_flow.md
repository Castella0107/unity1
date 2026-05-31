# PVP サーバー引き継ぎ資料 — 採点ルール変更 & フロー未完部分

- **From:** Castella(Unity / C# クライアント・現行 C# サーバー担当)
- **To:** K(PVPharmonics / Go サーバー担当)
- **日付:** 2026-05-31
- **対象コミット:** `main` @ `093936d`(github.com/Castella0107/unity1）

---

## 0. このドキュメントの目的と読み方

直近のクライアント作業で **サーバーの採点パイプライン / 試合終了セマンティクスに「当たる」変更** を入れました。Go サーバー側の設計・実装と整合させる必要がある点を、**仕様 + 現行 C# 実装の参照 + K 側で決める/実装することの分担** に分けてまとめます。

優先度:

| # | テーマ | 緊急度 | K 側影響 |
|---|--------|--------|----------|
| 1 | **セクター同点タイブレーク**(新スコアリングルール) | ★★★ | 採点 & レーティング直撃。Go finalize で必須再現 |
| 2 | **不戦勝/不戦敗 + 切断/再接続**(フロー⑫) | ★★ | サーバーの forfeit / disconnect 判定が前提 |
| 3 | **プレイ中の相手ライブスコア**(VSバー/リード/セクター勝敗) | ★ | 将来。ライブ同期 or ゴースト方式の選択 |
| 4 | **タイブレークの DB 永続化 & DTO**(Phase 2 未実装) | ★★ | スキーマ & レスポンス契約 |
| 5 | ロビーのティア/LP/ラダー/シーズン | ★ | 完全に K ドメイン(プレースホルダーで待ち) |
| 6 | **ホールド判定/スコア式の変更**(§5.1、別件) | ★★★ | `ScoreCalculator` verbatim 共有。Go の再現必須 |

> 方針(既存合意の再確認): C# Domain は **Go 再実装の仕様書**として参照。bit-perfect は保証しない(整数マイクロポイント + Glicko2 は ±0.01 許容)。本書の数式・しきい値は現行 C# 実装の値です。

---

## 1. ★最重要 — セクター同点タイブレーク(新スコアリングルール)

### 1.1 ルール定義(確定仕様)

1 試合 = 3 曲 × 5 セクター = 最大 15 セクター対戦。各セクターは両者のセクタースコアを比較し、**勝者 +1.0pt / 引分 +0.5pt ずつ**(従来どおり)。これに難易度倍率を掛けて表示ポイント化。

**今回の変更:** セクタースコアが **完全同点(ScoreA == ScoreB)** のとき、引分にせず **タイブレーク値が大きい側がそのセクターを取る(1.0 / 0.0)**。

```
タイブレーク値 TB = Σ(2 × PerfectPlus_count + 1 × Perfect_count)   ※そのセクター内のノーツ/ホールドティックを集計
```

セクター判定の優先順位:

```
if   ScoreA > ScoreB        -> A 勝ち
elif ScoreA < ScoreB        -> B 勝ち
elif TB_A   > TB_B          -> A 勝ち   ← 新規(同点タイブレーク)
elif TB_A   < TB_B          -> B 勝ち   ← 新規
else                        -> 引分(0.5 / 0.5)   ※スコアも TB も同値のときのみ
```

- **背景:** 同一譜面・接近プレイで完全同点が稀に発生(実例: alice=bob=115384)。DRAW を極力出さず「確実に優劣をつける」のがユーザー要望。
- **後方互換:** TB が両者 0(未供給/旧データ)なら従来どおり引分。

### 1.2 ★ レーティング(Glicko2)への影響 ← ここが K 設計と最も当たる

**タイブレークは sector の勝敗(Outcome)を変えるので、Glicko2 の入力スコアも変わります。**

- 現行 Glicko2 入力: 各セクターを 1 試合とみなし、**素の勝敗** `win=1.0 / draw=0.5 / loss=0.0` を与える(難易度倍率は **Glicko には掛けない**。倍率は 15pt 表示スコアにのみ効く)。
- タイブレークで「引分→勝ち」に変わったセクターは、Glicko 入力も **0.5/0.5 → 1.0/0.0** に変わる。
- 相補性(A と B のスコア和 = 1.0)は維持される。

→ **Go の finalize で同点タイブレークを実装しないと、勝敗・レート変動が C# クライアント表示とズレます。** 採点とレート計算は必ず同点タイブレーク込みで。

### 1.3 データフロー(現行 C# / 参照実装)

```
リプレイ(入力列, chartHash 内包)
  └─ ReplayValidationCore.ValidateAsync (Server/RhythmGame.Server/Services/ReplayValidationCore.cs)
       └─ JudgmentRunner.Run → JudgmentEngine → PlayProgressAggregator
            └─ PlayProgressSnapshot を返す
                 ├─ SectorScores   : int[5]  (従来; スコアデルタ方式, Σ == CurrentScore)
                 └─ SectorTieBreaks : int[5]  ★今回追加 (Σ 2×P+ + P, セクター毎)
  └─ PvpController がスナップショットから SectorScores / SectorTieBreaks を保存
  └─ 両者提出後 finalize: SectorPair[15] を組み立て MatchScoring.Score → Outcome + Glicko2
```

#### タイブレーク値の集計規則(セクター帰属)

`PlayProgressAggregator`(`Assets/_Project/Scripts/Domain/Play/PlayProgressAggregator.cs`):

- 各判定イベント処理時、**まず `UpdateSectorIfNeeded(timeMs)` でセクター index を進めてから** `_counts[j]++` と同じタイミングで `TrackSectorTie(j)` を呼ぶ:
  - `PerfectPlus` → 現セクターの TB に **+2**
  - `Perfect` → **+1**
  - `Great / Good / Miss` → +0
- **セクター帰属はスコアデルタと同一の時刻基準**(`UpdateSectorIfNeeded` の `timeMs >= sectorEndsMs[idx]` でセクション境界を跨ぐ)。Go 側も「スコアをどのセクターに加算するか」と完全に同じ帰属で TB を集計すること。
- ホールドティック(`ApplyTick`)も `PerfectPlus`/`Miss` のみで、P+ は +2。
- `sectorEndsMs` は S1..S4 の終了時刻(長さ4)、S5 は曲末。`meta.Sectors.Take(4).Select(s => s.EndMs)`。

#### `MatchScoring.Score`(`Assets/_Project/Scripts/Domain/Pvp/MatchScoring.cs`)

- `SectorPair { SongId, SectorIndex, ScoreA, ScoreB, Difficulty, TieA, TieB }`(TieA/TieB は default 0)。
- 上記 1.1 の優先順位で `SectorOutcome { Draw, AWins, BWins }` を決定 → `rawA/rawB ∈ {0, 0.5, 1.0}` に難易度倍率を掛けて `PointsA/PointsB`。
- 合計 → `MatchOutcomeKind`(A>B/A<B/==)。
- 難易度倍率: `easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00 / 不明 1.00`。

### 1.4 現行 C# サーバーでの保存場所(参照 / Go 設計の対応づけ用)

`Server/RhythmGame.Server/Services/`:

- `ActiveMatchStore.cs`
  - `PlayerSubmission.SectorTieBreaks : int[][]`(all-at-once /submit 用、[songIndex][sector]）
  - `ActiveMatch.PerSongTieBreaksA/B : int[][]`(完全同期 per-song 用)
  - `EnsurePerSong()` で両方を初期化
- `PvpController.cs`
  - `ExtractTieBreaks(snapshot)` … `vr.Snapshot.SectorTieBreaks` を 5 件 0 詰めで取り出す
  - `Tie(arr, song, sec)` … null 安全に [song][sec] を取得(未供給は 0 = 引分維持)
  - **`SectorPair` を作る 5 箇所すべてに tieA/tieB を付与**:
    1. per-song submit 時の当該曲ポイント計算
    2. song-result GET の当該曲ポイント計算
    3. `AccumulateBothSubmitted`(累計 / クリンチ判定)
    4. `FinalizeMatchAsync`(all-at-once、`SubmissionA/B.SectorTieBreaks` 由来)
    5. `FinalizePerSongAsync`(完全同期、`PerSongTieBreaksA/B` 由来)

→ **Go でも「累計 / クリンチ判定 / finalize」すべてで同点タイブレークを通すこと。**一部だけ反映すると、途中表示の累計と最終結果がズレます。

### 1.5 K が決める/実装すること(タイブレーク)

- [ ] Go の JudgmentRunner 相当に **セクター毎 TB 集計**(2×P+ + P、スコアと同じセクター帰属)を追加。
- [ ] Go の finalize / 累計 / クリンチ判定で **同点タイブレーク**を適用(1.1 の優先順位)。
- [ ] Glicko2 入力を **タイブレーク後の Outcome** で生成(1.2)。
- [ ] **TB を DTO / 永続化に載せる**(後述 §4)。

---

## 2. ★★ 不戦勝 / 不戦敗 + 切断 / 再接続(フロー⑫)— サーバー判定が前提

### 2.1 現状(クライアント)

- 試合中断は `PvpFlowController.AbortMatch(reason)`(`Assets/_Project/Scripts/Network/PvpFlowController.cs`)→ `PVPMatchEnd` に `ErrorMessage` を渡して終了。
- 主な reason: 相手提出タイムアウト(per-song poll 180s / 全体 poll 60s 失敗)、ネットワーク不可、リプレイ欠落、submit 拒否、例外。
- 表示(今回実装、`PvpMatchEndController`):中断理由を **MATCH INCOMPLETE / CONNECTION ERROR / MATCH ABORTED** に分類して表示。
- **重要: 相手タイムアウトは「勝敗なしの中断」**。現状 **不戦勝(自分の勝ち)にする処理はサーバーにもクライアントにも無い。**

### 2.2 フロー仕様が要求するもの(⑫)

「エラー/相手切断モーダル(**不戦勝 / 不戦敗 / 再接続中** で文言可変)」。これを満たすには **サーバー側の試合終了セマンティクス**が必要:

- **不戦勝/不戦敗(walkover):** 相手が一定時間提出しない/切断したら、**残った側を勝ちとして試合を確定**するか?その場合レート変動はどうするか(通常勝ち扱い? 無変動? 部分?)。
- **切断検出:** 現行は HTTP polling のみで「切断」概念が無い(提出が来ないだけ)。WebSocket 化後はハートビート等で検出可能。
- **再接続中:** リトライ/再接続の猶予時間と、その間クライアントに返すステータス。

### 2.3 K が決める/実装すること(⑫)

- [ ] **walkout の確定条件**(無提出タイムアウト秒数 / 切断検知)と **勝敗・レート規則**を定義。
- [ ] サーバーが forfeit 時に「勝者つきで finalize」する API 挙動(`outcomeKind` を勝敗付きで返す or 専用フィールド)。
- [ ] 「再接続中」を表す中間ステータス(クライアントはモーダルで「再接続中…」を出す想定)。
- クライアント受け口: 現状は `MatchResultDto.outcomeKind`(-1=進行中/0=Draw/1=AWins/2=BWins)と `ErrorMessage` を見ている。**forfeit を「勝者つき正常終了」で返してくれれば、クライアントは通常の VICTORY/DEFEAT 表示に乗せられる**(walkover 専用の文言を出したい場合は別フラグが欲しい — 要相談)。

---

## 3. ★ プレイ中の相手ライブスコア(VS バー / リード / セクター勝敗)— 将来

### 3.1 現状

- 対戦中 HUD に **VS スコアバー / リード差(self − opp)/ セクター毎 win-lose タグ** を実装済みだが、**相手側は全てプレースホルダー**(`------` / `+---` / `--`)。
- 理由: 現 PVP は **完全非同期**(各自プレイ → リプレイ提出 → 両者提出後にサーバーが開示)で、**プレイ中に相手のライブスコアが存在しない**。
- 自分側(スコア/リードの自分分)は実値で動く。相手名・相手の静的 Rate(`GET /api/pvp/user/{id}/stats` の rating)は試合前に確定するので実データ表示済み。

### 3.2 既にある足場

- `PvpProgressOverlay`(クライアント)が **0.5 秒毎に進捗を POST**:`POST /api/pvp/match/{id}/progress { userId, songIndex, percentX1000, score }`。
- サーバー `ActiveMatch.ProgressA/B`(`PlayerProgress { SongIndex, PercentX1000, Score, UpdatedAtUnixMs }`)に保持。
- **つまり相手の「現在スコア + 進捗%」はサーバーに来ている。** クライアントがこれを GET すれば VS バーの**相手スコア(概算ライブ)**は出せる。

### 3.3 選択肢(K と相談)

- **(a) ライブ進捗ポーリング/WebSocket:** クライアントが相手 `ProgressB.score` を取得 → VS バー & リードをライブ化。**セクター毎の win-lose ライブは不可**(進捗は total score + % のみで、セクター内訳が無い)。
- **(b) ゴーストリプレイ:** 相手が先にプレイ済みなら、その確定リプレイを取得してプレイ中に再生 → セクター毎まで完全ライブ。ただし「相手が先に完了している」前提 or 事前取得の仕組みが要る。
- どちらを採るかで WebSocket / API 設計が変わるため **方針確認が必要**。当面はプレースホルダー維持で問題なし。

### 3.4 K が決める/実装すること

- [ ] 相手ライブスコアの供給方式((a)/(b)/なし)を決定。
- [ ] (a) なら相手 progress 取得 API(WebSocket push 推奨)。(b) なら確定リプレイ取得 API + タイミング保証。

---

## 4. ★★ タイブレークの DTO / DB 永続化(Phase 2 未実装)

### 4.1 現状の穴

今回タイブレークは **in-memory(ActiveMatch / Submission)+ SongResultDto** までは流したが、**最終結果 `MatchResultDto` と DB `MatchEntity` には未反映**。

- `MatchResultDto.sectorScoresA/B`(15 件 int)はあるが **tiebreak 配列が無い**。
- DB `MatchEntity` は `SectorScoresA/B` を CSV 文字列で保存(`Server/RhythmGame.Server/Data/AppDbContext.cs`)。tiebreak カラム無し。DB は **`EnsureCreated`(マイグレーション無し)** なのでカラム追加 = dev DB 再作成(= レート/履歴リセット)を伴うため、今回は見送り。
- 影響: **最終結果⑪のヘッダ(勝敗/総pt/レート)はサーバー計算値なので正しい**。未対応で残るのは、クライアントが履歴用に再計算する曲別内訳(`PvpFlowController.BuildSongLines`)で、**完全同点セクターが稀に「引分」表示になる**こと(ヘッダの勝敗とは独立、軽微)。

### 4.2 現行 DTO(camelCase / C# REST)— Go では §6 の命名へ

`SongResultDto`(per-song 開示、`Assets/_Project/Scripts/Network/NetworkDtos.cs`):

```
songIndex, bothSubmitted,
selfSectors[5], oppSectors[5],
selfSectorTieBreaks[5], oppSectorTieBreaks[5],   ★今回追加
selfSongPoints, oppSongPoints, selfCumulative, oppCumulative,
clinch, matchOver, result(MatchResultDto)
```

`MatchResultDto`(最終結果):

```
matchId, userIdA, userIdB, songs[],
sectorScoresA[15], sectorScoresB[15],
（★Phase2 で追加したい: sectorTieBreaksA[15], sectorTieBreaksB[15]）
totalPointsA, totalPointsB, outcomeKind(-1/0/1/2),
ratingABefore/After, ratingBBefore/After, completedAtUnixMs
```

### 4.3 K が決める/実装すること

- [ ] `MatchResultDto`(最終)に **per-sector tiebreak 配列**を載せる(クライアント履歴の曲別内訳を厳密一致させるため)。
- [ ] 永続化スキーマに tiebreak を含める(Go は sqlc/pgx なので素直にカラム追加で OK。C# 側のような EnsureCreated 制約は無い)。
- [ ] 命名/エンベロープは §6 に従う。

---

## 5. 参照:採点パイプラインの正確な仕様(Go 再実装用)

> これらは既存仕様で今回変更なし。タイブレークと併せて Go で再現する際の基準値。

- **表示スコア:** 0〜1,000,000。`ScoreCalculator`(マイクロポイント整数ベース)。
- **判定窓 / ランク等:** `JudgmentRunner` / `JudgmentWindow`(PerfectPlus ±16ms 等)を仕様として参照。
- **セクタースコア:** スコアデルタ方式 `sector[i] = score_at_end[i] - score_at_end[i-1]`(Σ == 表示スコア)。
- **難易度倍率:** easy0.75 / normal0.80 / hard0.90 / extra1.00。**表示ポイントにのみ**作用、Glicko には作用しない。
- **Glicko2:** τ=0.5 / Scale=173.7178 / ε=1e-6 / Illinois 上限100。各セクター=1試合、素の勝敗(1/0.5/0)を与える。A+B=1.0 を維持。レート差は ±0.01 許容。
- **クリンチ(早期決着):** per-song 完全同期で、2 曲目(index≥1)以降に **どちらかの累計 ≥ 8.0pt** で決着(15pt 満点・過半数)、3 曲目をスキップ。`PvpController` の per-song submit 内で判定。**この累計計算も同点タイブレーク込み**であることに注意(§1.4-3)。
- **chartHash 登録の注意:** サンプル譜面は `charts/extra.json` の **宣言値 chartHash**(`0000…0001`)で登録(再計算しない)。リプレイ検証は **リプレイ内包の chartHash で登録譜面を引く**ため、リプレイがどの曲のものかは問わず「内部的に valid なら通る」。Go の検証も「登録済みハッシュとの一致 + JudgmentRunner 再生」で同様。

### 5.1 ★ ホールド判定 / スコア式(K 再現必須・別件だが採点パイプライン直撃)

> タイブレークとは別件だが、**直近でホールド判定のスコア式が変わっており、`ScoreCalculator` はサーバー検証 pipeline と verbatim 共有**のため Go 側の再現が必須(でないと検証スコアがズレる)。既存リプレイの再計算も旧式とは合わなくなる点に注意。

仕様(`Assets/_Project/Scripts/Domain/Play/BpmTimeline.cs` / `HoldJudgmentTracker.cs` / `ScoringEventCounter.cs`):

- **ホールド = 頭(tap)+ ボディティック + 尾(tail)** の合算でスコア/コンボを構成。
- **ティック間隔 = 1 小節 / 2**(`HoldTicksPerMeasure = 2`)。小節長 = `GetBeatIntervalMs × BeatsPerMeasure`、**`BeatsPerMeasure = 4`(拍子情報が無いため 4/4 固定)**。実質「2 拍ごとに 1 ティック」。
- ボディティックは `(startMs, endMs)` の**厳密に内側**に配置。ただし **`endMs − HoldTailGuardMs`(`HoldTailGuardMs = 1.0ms`)以降のティックは生成しない**(末尾の小節区切りが tail と重なってコンボ/スコアが二重加算されるのを防止 = 尾優先)。
- 各ティックの判定: **押下継続中は `PerfectPlus`**。離上しても **ガード `GUARD_MS = 50.0ms` 以内の短い離上は許容(P+ 継続)**、ガード超過で **放棄(abandon)→ 残りティックを全て `Miss`** で流す。
- 尾の解決: `EndMs` 到達時、押下継続 or ガード内離上なら P+、ガード超過なら Miss(離上の明示操作は不要)。
- **満点整合:** `ScoringEventCounter.CountHoldTicks` も**同じ `GetHoldTickIntervalMs`** を使い、`HoldTailGuardMs` で同様に末尾を打ち切る。これで全 PerfectPlus 時の合計が `1,000,000` に一致する(検証済)。

→ K の Go 実装で **ティック生成位置(2/小節・末尾ガード 1ms)・離上ガード 50ms・満点整合**を同一にすること。bit-perfect は非保証方針だが、**ティック個数がズレると判定数・スコアが構造的にズレる**ので、ティック生成ロジックは厳密一致が必要(許容差で吸収できない種類のズレ)。

---

## 6. API / DTO 命名の対応(現行 C# → Go 長期方針)

長期方針(既存合意)に合わせる際の対応表。**新規追加した tiebreak フィールドも同ルールで命名してください。**

| 項目 | 現行 C#(短期) | Go(長期) |
|------|----------------|-----------|
| フィールド命名 | camelCase (`selfSectorTieBreaks`) | **snake_case** (`self_sector_tie_breaks`) — Unity 側は `[JsonProperty]` で対応 |
| URL プレフィックス | `/api/pvp/...` | `/api/v1/pvp/...`(`/health` のみ直下) |
| 成功レスポンス | 素の DTO | `{"data": {...}}` |
| 失敗レスポンス | HTTP 4xx + 文字列/DTO | `{"error": {"code": "...", "message": "..."}}` |

新規エンドポイントは無し。既存に **フィールド追加のみ**(`*_sector_tie_breaks`)。

---

## 7. K 側 決定事項チェックリスト(まとめ)

- [ ] **(§1)** Go 採点に「セクター毎 TB 集計(2×P+ + P)」+「同点タイブレーク」を実装。finalize / 累計 / クリンチ / Glicko 全てに適用。
- [ ] **(§4)** `MatchResultDto`(最終)+ 永続化に per-sector tiebreak を追加。命名は snake_case / `{data}`。
- [ ] **(§2)** 不戦勝/不戦敗の確定条件・レート規則・「再接続中」ステータスを定義。forfeit を勝者付きで返す API 挙動を決定(walkover 専用フラグの要否も)。
- [ ] **(§3)** プレイ中の相手ライブスコア供給方式(ライブ進捗 / ゴースト / なし)を決定。
- [ ] **(§5)** chartHash 登録・Glicko 許容差・クリンチ仕様を Go 実装で踏襲。
- [ ] **(§5.1)** ホールド判定/スコア式(2 ティック/小節・末尾ガード 1ms・離上ガード 50ms・満点整合)を Go の JudgmentRunner で厳密再現。**別件だが採点 pipeline 直撃。**

---

## 付録:主な参照ファイル(本リポ `main` @ 093936d）

| 役割 | パス |
|------|------|
| セクター集計 + TB | `Assets/_Project/Scripts/Domain/Play/PlayProgressAggregator.cs` |
| スナップショット | `Assets/_Project/Scripts/Domain/Play/PlayProgressSnapshot.cs` |
| 採点(同点タイブレーク) | `Assets/_Project/Scripts/Domain/Pvp/MatchScoring.cs` |
| リプレイ検証 | `Server/RhythmGame.Server/Services/ReplayValidationCore.cs` |
| 判定再生 | `Assets/_Project/Scripts/Domain/Replay/JudgmentRunner.cs` / `Domain/Judgment/JudgmentEngine.cs` |
| 試合状態保持(in-memory) | `Server/RhythmGame.Server/Services/ActiveMatchStore.cs` |
| エンドポイント / finalize | `Server/RhythmGame.Server/Services/PvpController.cs` |
| 永続化エンティティ | `Server/RhythmGame.Server/Data/AppDbContext.cs` |
| クライアント DTO | `Assets/_Project/Scripts/Network/NetworkDtos.cs` |
| 試合進行(クライアント) | `Assets/_Project/Scripts/Network/PvpFlowController.cs` |
| 結果表示(中断分類含む) | `Assets/_Project/Scripts/UI/Pvp/PvpMatchEndController.cs` |
| 曲リザルト表示 | `Assets/_Project/Scripts/UI/Pvp/PvpSongResultController.cs` |
