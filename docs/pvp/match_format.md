# PVP 試合フォーマット (3曲 × 5セクター) — 現段階設計

最終更新: 2026-05-24 / 対応コミット: `60ee41d` (Phase 5-δ 時点)

本書は **マッチングキューへの参加 → 試合確定 → 結果表示** までの一連の流れを、現在の実コードを基準にまとめたもの。
正規 PVP シーン (PVPPrematch / PVPSongPick / PVPBanPhase) は未実装で、現状は `Matchmaking` → `GamePlay × 3` → `PVPMatchEnd` の最小経路で動作する。

---

## 1. 試合の構造

設計書: 「1試合 = 3曲 × 5セクター = 最大15pt」

| 項目 | 値 | 出典 |
| --- | --- | --- |
| 1 試合の曲数 | 3 曲 | `PvpController.Create` (`shuffled.Take(3)`) |
| 1 曲あたりのセクター | 5 (index 0..4) | `MatchScoring.Score` の入力前提、`SectorPair.SectorIndex` コメント |
| 1 セクターあたり配点 | 勝者 1.0 / 引分 0.5 / 0.0 | `MatchScoring.Score` |
| 1 試合の最大ポイント | 15.0 | 3 × 5 × 1.0 |
| 採点単位の細分化 | x10 整数で永続化 | `MatchEntity.TotalPointsAx10/Bx10` |
| 楽曲プール | シーズン固定 (現在は `bootstrap_2026Q2` で test_song 系 4 曲) | `MatchPool.CreateBootstrapPool` |
| 難易度 | プール側で指定 (現在は全曲 `extra` 固定) | 同上 |

> シーズン跨ぎ減衰: `Glicko2Calculator.SeasonDecay(player, decay=0.5, floorRd=100)`
> 設計書通り `new_R = 1500 + (old_R - 1500) × 0.5`、RD は 100 未満なら 100 に持ち上げ。

---

## 2. データモデル

### 2.1 試合進行中 (in-memory, サーバープロセス内)

`ActiveMatchStore.ActiveMatch`:

```
MatchId          : Guid (string)
UserIdA, UserIdB : string
Songs            : SongPick[3]      (SongId + Difficulty)
SubmissionA/B    : PlayerSubmission (Submitted bool, SectorScores int[3][5])
ProgressA/B      : PlayerProgress   (SongIndex, PercentX1000, Score, UpdatedAtUnixMs)
CreatedAtUnixMs  : long
Finalized        : bool
```

ConcurrentDictionary で保持、プロセス再起動でロスト。

### 2.2 試合確定後 (DB 永続化)

`MatchEntity` (SQLite, EF Core 9):

```
MatchId           PK
UserIdA, UserIdB
SongIdsCsv        "song1,song2,song3"
DifficultiesCsv   "extra,extra,extra"
SectorScoresA     "s00,s01,s02,s03,s04,s10,...,s24"  (3曲×5sec = 15値)
SectorScoresB     同上
TotalPointsAx10   int (実値の 10 倍)
TotalPointsBx10   int
OutcomeKind       int (0=Draw, 1=AWins, 2=BWins)
RatingABefore/After, RatingBBefore/After   double
CreatedAtUnixMs, CompletedAtUnixMs         long
```

`UserEntity` (Glicko-2 状態):
`Rating(=1500)` / `RatingDeviation(=350)` / `Volatility(=0.06)` / `LastRatedAtUnixMs` /
`TotalPvpMatches` / `PvpWins` / `PvpLosses` / `PvpDraws`

### 2.3 Pure C# ドメイン型 (`Domain.Pvp`)

クライアント・サーバーで bit-perfect 同期 ([[feedback_replayTimingRounding]])。

- `SectorPair(songId, sectorIndex, scoreA, scoreB)`
- `SectorResult(songId, sectorIndex, scoreA, scoreB, pointsA, pointsB, Outcome)`
- `MatchOutcome(Sectors[15], TotalPointsA, TotalPointsB, MatchOutcomeKind)`
- `Glicko2Player(Rating, RatingDeviation, Volatility)` — immutable
- `Glicko2Result(opponentRating, opponentRD, score)` — score は 1.0/0.5/0.0

---

## 3. シーン構成 (Unity)

```
Title.unity
  └─ Online ボタン → SceneRouter.GoTo(Matchmaking)

Matchmaking.unity                    [MatchmakingController]
  └─ queue 成立 → PvpFlowController.StartMatch

GamePlay.unity (×3)                  [GamePlayController + PvpProgressOverlay]
  └─ 完走毎に PvpFlowController.OnSongCompleted

PVPMatchEnd.unity                    [PvpMatchEndController]
  └─ Back to Title
```

常駐シングルトン (Bootstrap で `RuntimeInitializeOnLoadMethod` 自動 spawn):

- `NetworkClient` — REST 通信全般
- `PvpFlowController` — 3 曲連戦の状態保持と一括 submit
- `PvpProgressOverlay` — 試合中の相手進捗ポーリング/表示
- `SceneRouter`, `LocalIdentity`, etc.

正規 UI が未割当でも、`MatchmakingController` / `PvpMatchEndController` は OnGUI フォールバックで操作可能。

---

## 4. マッチング (queue/join → status poll → matched)

### 4.1 クライアント側 (`MatchmakingController.RunMatchmakingLoop`)

```
JoinQueueAsync(userId)
  ├─ status == "matched"  → StartMatchFromQueueResponse(body) → 完了
  └─ status == "queued"   → 1.5 秒間隔で GetQueueStatusAsync を poll
                            ├─ "matched" → StartMatchFromQueueResponse
                            ├─ "idle"    → 再 join (キュー drop 救済)
                            └─ "queued"  → 継続
Cancel → LeaveQueueAsync + SceneRouter.GoTo(Title)
```

`pollIntervalSec = 1.5f`、キャンセル時は途中で抜ける。

### 4.2 サーバー側 (`MatchmakingQueueService`)

単一 lock + `Queue<string> _waiting` + `Dictionary<userId, MatchedNotice> _matched`。

```
Join(userId):
  - 既に waiting → status=Queued
  - 既に _matched にある → status=Matched + 通知を返す (consume はしない)
  - waiting に他ユーザーがいる → Dequeue → ペアリング:
      MatchPool.CreateBootstrapPool() から 3 曲ランダム選択
      ActiveMatchStore.Create(other, userId, picks) → MatchId 発行
      _matched[other] = (matchId, userId, songs)   ← 相手側 poll で取り出す
      自分には status=Matched + 同 matchId を即返す
  - waiting が空 → Enqueue → status=Queued

GetStatus(userId):
  - _matched にあれば Remove して 1 度だけ返す (consume)
  - waiting にあれば Queued
  - それ以外 Idle
```

### 4.3 API

| メソッド | パス | 機能 |
| --- | --- | --- |
| POST | `/api/pvp/queue/join`   | `{userId}` → `QueueResponseDto` |
| POST | `/api/pvp/queue/leave`  | `{userId}` → idle |
| GET  | `/api/pvp/queue/status?userId=` | `QueueResponseDto` (matched は 1 度で consume) |

`QueueResponseDto`: `Status` (idle/queued/matched) + `MatchId` + `OpponentId` + `Songs[3]` + `QueueDepth`

> 直接 `POST /api/pvp/match/create {userIdA, userIdB}` でキューを介さず試合を作る経路もある (検証/デバッグ用)。

---

## 5. 試合中フロー (`PvpFlowController`)

`StartMatch(matchId, opponentId, songs)` で開始すると、以下を曲数分繰り返す:

```
LaunchCurrentSong()
  → GamePlayParameters { IsPvp=true, PvpMatchId, PvpSongIndex, PvpOpponentId, HiSpeed=1.0, Modifier=None, ... }
  → SceneRouter.GoTo(GamePlay)
  → プレイ完走
  → GamePlayController が PVP モードを検知し OnSongCompleted(songId, replayPath) をコール
  → CurrentSongIndex++
  → CurrentSongIndex < 3 なら LaunchCurrentSong、そうでなければ SubmitAndFinish
```

PVP モードでは `GamePlayController` が:

- `SubmitToServerFireAndForget` (ソロ用 leaderboard 提出) を **スキップ**
- 完走後の Result シーン遷移を **抑止** (PvpFlowController に委譲)

### 5.1 相手進捗のリアルタイム配信 (Phase 5-δ)

`PvpProgressOverlay` (PVP モードかつ GamePlay 中のみ動作):

- GamePlayController の Update から `UpdateLocalProgress(songIndex, percent0to1, score)` を毎フレーム受ける
- `PollIntervalSec = 0.5f` 間隔で `POST /api/pvp/match/{matchId}/progress`
  - in-flight は 1 件制限、前回完了するまで次は投げない
- POST のレスポンスが両者スナップショットを兼ねるので GET は基本使わない
- 画面右上に `YOU song=2 47.3% score=520000 / OPP song=1 88.0% score=910000` のように表示

サーバー側 `ProgressUpdateDto`: `{UserId, SongIndex, PercentX1000 (0..100000), Score}`

> **既知の制約**: HTTP polling のため厳密にはリアルタイムではない。SignalR/WebSocket 化は次段。

---

## 6. リプレイ提出と試合確定 (同期型 finalize)

3 曲完走後、`PvpFlowController.SubmitAndFinish`:

```
foreach (song in 3) ReadAllBytes(replayPath) → Base64
POST /api/pvp/match/{matchId}/submit {userId, songs:[{songId, replayDataBase64}*3]}
  → SubmitResponseDto:
      Accepted=true, MatchFinalized=false → PollUntilFinalizedAsync (相手待ち)
      Accepted=true, MatchFinalized=true  → result が同梱されて即終了
      Accepted=false                       → AbortMatch
```

`PollUntilFinalizedAsync`: 2 秒間隔 × 30 回 (60s) `GET /api/pvp/match/{matchId}` を呼び、
`outcomeKind >= 0` になったら finalize 完了とみなす。タイムアウトで AbortMatch。

> **既知バグ**: 在進行中 (active) 試合の `GET /match/{id}` は `OutcomeKind=-1` を返すのみで、
> 「片側 submit 済」状態を露出しない ([[feedback_pvpMatchGetNoSubmittedSignal]])。
> e2e テストは bob を alice より先に submit する戦略を取る。

### 6.1 サーバー側 submit 処理 (`PvpController.Submit`)

```
1. Match 取得 (not found / already finalized / already submitted を弾く)
2. songs.Count == match.Songs.Count をチェック
3. 各曲 i について:
     - actual.SongId が expected.SongId と一致するか (順序固定)
     - Base64 デコード → ReplayDecoder.Decode → metadata.chartHash 取り出し
     - ReplayValidationCore.ValidateAsync(chartHash, bytes)
       (chartHash 不一致 / JudgmentRunner 結果不一致は VALID にならない)
     - PlayProgressSnapshot.SectorScores から 5 件を取り出して sectorScores[i] へ
4. mySub.Submitted = true; mySub.SectorScores = sectorScores
5. 両者揃っていれば FinalizeMatchAsync → MatchFinalized=true + Result 同梱
6. 揃っていなければ Accepted=true, MatchFinalized=false で即返却
```

VALID は bit-perfect: クライアントの `JudgmentSystem` が `Math.Round(timeMs)` で int に丸めてから engine に渡しているため、リプレイ delta (int) と完全一致する。

### 6.2 FinalizeMatchAsync

1. `SectorPair[15]` を構築 (3 songs × 5 sectors を `(songId, sectorIndex, scoreA, scoreB)` で並べる)
2. `MatchScoring.Score(pairs)` → `MatchOutcome { Sectors[15], TotalPointsA, TotalPointsB, Kind }`
3. A/B の **BEFORE** rating を読み出し、それぞれ独立に `Glicko2Calculator.Update(player, sector結果15件)` を実行
   - 各セクターを「opponent の RD/Rating に対する 1 試合 (score=1/0.5/0)」として扱う
4. UserEntity の Rating/RD/Volatility/LastRatedAtUnixMs/TotalPvpMatches/Wins/Losses/Draws を更新
5. `MatchEntity` を Insert + `SaveChangesAsync` (1 トランザクション)
6. `ActiveMatchStore.Remove(matchId)` で in-memory から除去
7. `MatchResultDto` を返却

---

## 7. レーティング計算 (Glicko-2)

ライブラリ未使用、Pure C# 実装 (`Glicko2Calculator.cs`、Glickman 2012 論文準拠)。

| 定数 | 値 |
| --- | --- |
| 初期 Rating / RD / Volatility | 1500 / 350 / 0.06 |
| システム定数 τ | 0.5 |
| 内部スケール | 173.7178 |
| 収束閾値 (ε) | 1e-6 |
| 反復上限 (Illinois) | 100 (発散ガード) |

セクター 15 件を「15 試合分」として 1 度に `Update` に渡す。
試合がない期間用に `Decay(player)` (φ のみ拡張)、シーズン跨ぎに `SeasonDecay(player, 0.5, 100)` を提供。

EditMode テスト `Assets/_Project/Tests/EditMode/Pvp/Glicko2CalculatorTests.cs` で論文 Section 6 のリファレンス値 (1500/200/0.06 + 3 戦 → 1464.06/151.52/0.05999) を Tolerance 0.01 で再現済み。

---

## 8. 結果表示 (`PvpMatchEndController`)

`PvpFlowController.FinishToMatchEndScene` から `PvpMatchEndParameters` を `ParameterStore` 経由で受け取り表示する:

```
PvpMatchEndParameters {
  MatchId, UserIdA, UserIdB, SelfUserId,
  TotalPointsA, TotalPointsB,
  OutcomeKind  (0=Draw, 1=AWins, 2=BWins),
  RatingABefore/After, RatingBBefore/After,
  ErrorMessage  ← AbortMatch 経路用
}
```

表示内容:

- ヘッダ: `VICTORY` / `DEFEAT` / `DRAW vs {opponentId}` (self の視点で判定を反転)
- スコア: `You 12.0 - 3.0 Opponent`
- レーティング: `Your rating: 1500.0 → 1588.4 (+88.4)` + opponent も同様
- AbortMatch 時 (`ErrorMessage` あり) は `Match Aborted` + 原因文を表示

正規 UI 未組込み時は OnGUI フォールバックで全て表示可能。Back to Title ボタンで Title へ。

---

## 9. API サーフェス一覧

| Method | Path | 用途 |
| --- | --- | --- |
| POST | `/api/pvp/queue/join`            | 待機列に参加 (即マッチング含む) |
| POST | `/api/pvp/queue/leave`           | キャンセル |
| GET  | `/api/pvp/queue/status?userId=`  | 待機状況 poll (matched は 1 度で consume) |
| POST | `/api/pvp/match/create`          | 直接マッチ作成 (検証用、`{userIdA, userIdB, poolSongIds?}`) |
| POST | `/api/pvp/match/{id}/submit`     | リプレイ 3 件提出 + 両者揃えば finalize |
| GET  | `/api/pvp/match/{id}`            | 進行中 (`OutcomeKind=-1`) / 確定 (DB から MatchResultDto) |
| POST | `/api/pvp/match/{id}/progress`   | 自分の進捗更新 + 両者スナップショット返却 |
| GET  | `/api/pvp/match/{id}/progress`   | 両者進捗スナップショット (finalize 済は `Finalized=true`) |

Unity 側ラッパは `NetworkClient.{Join,Leave,GetQueueStatus,CreateMatch,SubmitMatch,FetchMatch,SendPvpProgress,FetchPvpProgress}Async`。

---

## 10. 既知の制約と次段

- **正規 PVP UI シーン未実装**: PVPPrematch (試合直前情報)、PVPSongPick (BAN/PICK)、PVPBanPhase は仕様未確定で省略中。現状は queue 入って即 3 曲開始
- **楽曲プール**: 4 曲固定 (test_song 系) のみ。設計書の「20曲固定 / セクター動的選曲」は Phase 6 コンテンツ制作と同時整備
- **難易度**: 全曲 `extra` 固定 (設計書: easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00 倍率は未適用)
- **進捗配信が HTTP polling**: 0.5 秒間隔。SignalR Hub or gRPC duplex stream 化が次段
- **`GET /match/{id}` が in-progress submit を露出しない**: e2e の片側完了検知に使えない ([[feedback_pvpMatchGetNoSubmittedSignal]])
- **認証なし**: `LocalIdentity.UserId` (PlayerPrefs `DisplayName` 基準) を信用。Phase 4-B で Discord OAuth + JWT 導入予定
- **リプレイ blob のサーバー保存なし**: 検証だけして破棄。改竄解析が必要になった時点で追加

---

## 11. e2e 検証実績 (2026-05-24)

Unity Editor (alice) vs curl (bob) で 2 マッチ実行。

| Match | 結果 |
| --- | --- |
| `a45024f2` | alice 3 曲 → submit OK / bob 未提出のため 60s timeout → AbortMatch。後から bob を curl で submit したら DRAW 7.5-7.5 (1500→1500) で DB 保存 |
| `ed1c15ad` | bob を pre-submit → alice 3 曲 → submit が即 finalize → VICTORY 12-3 / 1500 → 1588.36 vs 1500 → 1411.64 |

bit-perfect VALID は test_song_2 (Hold 16 個含む) で score=997586 / rt=152ms を確認済 (Phase 4-A 検証クローズ)。
