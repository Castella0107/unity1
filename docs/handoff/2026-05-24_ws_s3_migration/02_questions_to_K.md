# Unity クライアント WebSocket + S3 移行タスク — 質問・確認事項

作成日: 2026-05-24
作成: Castella (Unity クライアント担当)
宛先: K さん (PVPharmonics サーバー担当)
対応タスク: `unity_websocket_s3_migration_task.md` Step 1 (コード変更前の質問報告)

---

## 0. はじめに

タスク md に記載の Step 1「コード変更前に質問・不明点を Markdown でまとめて報告」に基づき、現状の不確定要素を整理しました。
**回答が揃うまで Step 2 以降のコード変更には着手していません**。

なお、現リポ (unity1) の `Server/` 配下には Phase 4-A〜5-δ で構築した ASP.NET Core 10 + EF Core 実装が存在します。本タスク md では言及がないので、こちらの扱いから先に擦り合わせさせてください (項目 A 参照)。

---

## A. 設計の根幹に関わる確認事項 (最優先)

### A-1. 現リポ `Server/` (ASP.NET Core) の扱い

Phase 4-A〜5-δ で構築した現リポの C# サーバー実装には以下があります。

- `PvpController` (queue/match/submit/progress)
- `LeaderboardController` (`/api/leaderboard/...` + `/me`)
- `ReplayRestController` (`/api/replay/validate` + Users/PlayRecords UPSERT)
- `ActiveMatchStore` / `MatchmakingQueueService` (in-memory)
- `AppDbContext` (SQLite + EF Core 9)
- 共有 Domain (`<Compile Include="...Assets/_Project/Scripts/Domain/**/*.cs">` で Unity 側を直リンク)
- e2e 検証実績あり (alice vs bob で finalize + Glicko2 1500→1588.36 / →1411.64 確認)

PVPharmonics (Go) に乗り換えるなら不要ですが、こちら側で判断したいので意図を教えてください:

- [ ] **棄却**: `Server/` を `archive/csharp-server` ブランチに退避して main から削除
- [ ] **参考実装として残置**: そのまま残し、Go 実装の仕様照合に使う
- [ ] **当面ローカル検証用に維持**: Go サーバーが手元で動くまではこちらでテスト

**こちら側の暫定方針**: 別ブランチ退避を予定 (main から削除)。問題あれば指摘ください。

### A-2. PVPharmonics の現状

- 言語: Go ですか? (タスク md では明示なし、ポート 8080 のみ)
- リポ URL: 当方アクセス不可とのことですが、CI で動いてる/Docker image が公開されてる等の手元で立ち上げる方法はありますか?
- 現在の進捗状況: `/api/v1/ping` は動きますか? `/api/v1/pvp/match/{matchId}/ws` は動きますか?
- 動作可能タイムライン: Step 5 (統合テスト) のとき、ローカルで `http://localhost:8080` を起動できる前提でいいですか? それともモックサーバー必要ですか?

### A-3. Domain 層 (Pure C#) の bit-perfect 同期方針

現リポでは `JudgmentRunner` / `Glicko2Calculator` / `ScoreCalculator` 等を Unity・C#サーバー双方で **同一ソース** (`Assets/_Project/Scripts/Domain/`) として共有しており、これにより VALID 判定の bit-perfect 同期 (score=997586 等を実証) を実現しています ([[feedback_replayTimingRounding]] 参照)。

タスク md の「Pure C# Domain Layer は変更しない (ローカルテスト用に維持)」は、Unity 側 Domain をそのまま残すという意味で理解しました。

それを踏まえて:

- PVPharmonics (Go) 側では Glicko2 / JudgmentEngine を **Go で再実装** という理解で合っていますか?
- bit-perfect が必要だった「リプレイ検証 (`JudgmentRunner.Run`)」は、サーバー側で誰がやりますか?
  - (a) Go で完全再実装し、IEEE 754 double 演算を厳密一致させる
  - (b) サーバーは保存のみで検証は捨てる (= リプレイ改竄は完全に信用ベース)
  - (c) サーバーが C# サービスを別途呼び出す
- レーティング (Glicko-2) も Go で再実装する場合、τ=0.5 / Scale=173.7178 / ε=1e-6 / Illinois algorithm 上限 100 回 という現リポ実装の定数値で合わせていただけますか?

---

## B. WebSocket メッセージスキーマ

### B-1. エンドポイント・接続

- 接続先 URL: `ws://localhost:8080/api/v1/pvp/match/{matchId}/ws` の理解で正しいですか?
- HTTP→WS アップグレードヘッダに認証情報を載せる方式 (例: `Authorization` ヘッダ) ですか? それとも接続後 `auth` メッセージのみで完結ですか?
- 同一 user_id の WebSocket 接続が複数張られた場合の挙動は? (新規が古いを切るか、新規を拒否するか)
- 同一マッチに 3 つ目以降 (観戦目的等) の接続が来た場合は?

### B-2. メッセージ形式の規約

- 全メッセージは JSON ですか? バイナリ (msgpack/protobuf 等) はサンプル外ですか?
- フィールド命名は `snake_case` 統一ですか? (タスク md の例は `user_id`, `song_index`, `percent_x1000` — 現 Unity 側 DTO は全部 `camelCase`)
- メッセージ ID / シーケンス番号は付けますか? (重複検知や順序保証のため)
- timestamp フィールドは含めますか? (clock skew 解析や遅延測定用)

### B-3. C → S メッセージ

- `auth`: 接続後、最初に送る前提ですか? auth 前に他の type を送った場合はどう扱われますか (即切断 / 単に無視)?
- `progress`: 送信トリガは「変化があったとき」とのこと。**スロットリング規約**はありますか? (例: 最小 100ms 間隔、percent 1% 以上変化、など) — クライアント側がどうレート制御すべきか
- 上記以外に C→S で送る type ありますか? (例: `ping`、`song_started`、`song_completed` 等の状態イベント — または「サーバーが進捗から推測」するので不要?)

### B-4. S → C メッセージ

- `opponent_progress`: サーバー側で「初回接続時に相手の最新進捗を送る」挙動はありますか? それともクライアントが何か投げないと取れないですか?
- `match_state` の state 値:
  - 「`started`」: いつ送られますか? (両者接続完了時? それとも 1 曲目開始時?)
  - 「`song_completed`」: 何の `song_completed` なのか不明 (自分? 相手? 両者?)。`song_index` を伴いますか?
  - 「`match_finalized`」: 結果データ (`MatchResultDto` 相当) は同梱されますか? それとも別途 REST で `GET /api/v1/pvp/match/{id}` を叩く必要ありますか?
  - 「`opponent_disconnected`」: 相手の切断 vs 自分の切断 (= 自分側でこのメッセージは見ない) は区別されますか? 切断後の試合扱い (= 即敗北? 60s 待機後 forfeit? その他?) はどうなりますか?
  - 上記 4 種類以外に追加 state ありますか?

### B-5. エラー・接続管理

- `error.code` の網羅一覧をください (`UNAUTHORIZED`, `MATCH_NOT_FOUND` の他に何があるか)
- error 受信後の接続は自動切断ですか? それとも継続して他メッセージを送り続けられますか?
- サーバー側からの ping/pong (RFC 6455 制御フレーム) の送信間隔は? Unity 側はクライアント側 pong 自動応答 (`ClientWebSocket` のデフォルト動作) で問題ないですか?
- 切断検知方式の推奨: HeartBeat (アプリ層 ping/pong メッセージ) vs WebSocket 制御フレーム、どちらを正にしますか?
- 接続が予期せず切れた場合、サーバーは何秒間その user_id の reconnect を待ちますか? (この間に再接続できれば試合継続、超えると forfeit?)
- 再接続時、クライアントが auth → progress (現状値) を送る順番でいいですか? それとも別途 `resume` 等の type が必要ですか?

---

## C. Pre-signed URL (リプレイアップロード)

### C-1. URL の有効期限

- `expires_at` の典型値 (数分 / 数十分 / 1 時間以上)?
- リプレイ 3 曲 ≈ 4.5MB を順次 PUT する想定だが、3 曲目アップロード中に 1 曲目の URL が expire しないだけの長さは保証されますか?
- expire 後の PUT 失敗時、`/replay-urls` を再度叩いて全 URL 再発行 (3 曲分) になりますか? それとも残ったものだけ再発行できますか?

### C-2. アップロード制約

- 1 ファイル最大サイズの上限はありますか? (現状リプレイは長くても数 MB 程度ですが、上限を超えた場合のエラーレスポンス形式は?)
- Content-Type は `application/octet-stream` 固定ですか? それともサーバー側で自由?
- ヘッダ `headers` フィールドのキーは全て付与必須ですか? (R2 側で署名検証時にミスマッチで失敗する想定)
- Content-MD5 / Content-Length は要りますか? (含むなら署名の元値に入っているはず)

### C-3. 重複 / リトライ

- 同じ song_id に対して同じ Pre-signed URL に PUT を 2 回実行した場合 (リトライ時など)、サーバー側挙動はどうなりますか? (R2 は通常 upsert)
- PUT 失敗 (ネットワークエラー / 5xx) のリトライは、同じ URL に再 PUT してよいですか? それとも `/replay-urls` 再取得が必須ですか?
- `/replay-urls` の再取得制限はありますか? (DoS 対策 rate limit など)

### C-4. 段階 3 の `/submit`

- `song_uploads_confirmed` の順序は意味を持ちますか? (例: マッチ作成時の songs 順序と一致必須? それとも自由順?)
- アップロード前に `/submit` を叩いた場合、サーバー側で R2 から取得を試みて失敗 → エラーを返す挙動ですか? それともクライアント側で先に全 PUT 完了を確認してから叩くべきですか?
- リプレイの bit-perfect 検証 (現在 `ReplayValidationCore`) は、`/submit` 受信タイミングで R2 から取得して実行されますか?
  - その場合、検証で INVALID になった場合のレスポンス形式は? (現実装は `Accepted=false, Error="songs[i] validate: ..."`)

---

## D. REST API 命名規約とプレフィックス

### D-1. snake_case vs camelCase

- 全 DTO で snake_case 統一ですか? それとも JSON は snake_case、内部 Go struct は PascalCase という慣習的なものですか?
- 既存 Unity DTO は全部 camelCase なので、これを機に全部書き換えるか、Newtonsoft の `[JsonProperty("user_id")]` で対応するか、こちらで判断していいですか?

### D-2. `/api/v1` プレフィックス

タスク md は「全エンドポイントパスに `/api/v1` プレフィックスを追加」とありますが:

- `/api/ping` → `/api/v1/ping` で確定ですか?
- `/api/leaderboard/{songId}/{difficulty}` → `/api/v1/leaderboard/...` で確定ですか? (そもそも leaderboard は Go 側に実装される予定?)
- `/api/replay/validate` → `/api/v1/replay/validate` で確定ですか? (この API は PVP とは別物のソロ用。Go 側に実装される予定?)

### D-3. WebSocket と REST のパス整合性

- REST: `/api/v1/pvp/match/{matchId}/...`
- WS: `/api/v1/pvp/match/{matchId}/ws`

この階層で正しいですか? 別ホスト (`ws-server.example.com` 等) に分離する計画はありますか?

---

## E. エラーレスポンス形式

タスク md には部分的に `{ "data": { ... } }` の形式が見えます (Pre-signed URL レスポンス例)。

- 成功時の REST レスポンスは常に `{ "data": ... }` でラップされますか?
- 失敗時の REST レスポンスは `{ "error": { "code": "...", "message": "..." } }` の形式ですか?
- HTTP ステータスコードと `error.code` の対応関係はありますか?
- WebSocket の `error` メッセージと REST の `error` レスポンスの `code` 命名規約は共通ですか?

---

## F. 既存 API・機能との関係

### F-1. ソロプレイ用 API

- `/api/replay/validate` (`ReplayRestController`): ソロのリプレイ検証 + PlayRecord 保存。Go サーバーで同等機能を実装する予定はありますか? それとも当面ローカル C# サーバーで運用?
- `/api/leaderboard/{songId}/{difficulty}` + `/me`: 同様の質問。Go 側に移植予定ですか?

### F-2. SubmissionQueue の永続化

`SubmissionQueue` は現状 `/api/replay/validate` 用に作られたもの (ソロ送信失敗の保留)。PVP submit には使われていません。

- ソロ用 `/replay/validate` 側もリプレイ S3 PUT 化されますか? その場合 SubmissionQueue を「Pre-signed URL 取得失敗 / PUT 失敗 / 完了通知失敗」3 段階に拡張するのは PVP 用と兼用設計でいいですか?
- それともソロは現方式 (base64 JSON) のまま、PVP のみ S3 PUT 化ですか?

### F-3. テストの方針

- `Assets/_Project/Tests/EditMode/` 配下に既存テストあり (Pvp/Glicko2CalculatorTests, MatchScoringTests)。これらは Domain 単体テストで通信を含まないため影響ありません。
- 通信側の新規テストはモックサーバー (例: WireMock / 自前 ASP.NET 簡易スタブ) を立てる前提でいいですか? PVPharmonics の Go 実装が手元で動かせるならそれを使いますが、不可なら別途モック設計が必要です。
- WebSocket のモックは何を使う想定ですか? (`websocketd` / `mockserver` / Go の `net/http/httptest` 等)

---

## G. スケジュール・運用

### G-1. Step 着手のブロッカー

- 上記 A-1 (現リポ Server 扱い), A-2 (PVPharmonics 状況), B 全般 (WebSocket スキーマ), C-1 (expires_at 長さ) が決まらないと Step 2 (PvpWebSocketClient.cs 実装) に入れません
- 上記 C, D, E, F が決まらないと Step 4 (S3 PUT 化) に入れません
- Step 3 (PvpProgressOverlay 書き換え) は Step 2 の I/F に依存

### G-2. コミット粒度

タスク md に「コミット粒度は機能単位 (Step 単位) で分割」とあり、こちらで合意です。
- Step 2: WebSocket クライアント新規追加 (1 PR / 1 コミット)
- Step 3: PvpProgressOverlay 書き換え (別コミット)
- Step 4: NetworkDtos / NetworkClient / PvpFlowController / SubmissionQueue (4 ファイル間で密結合のため 1 コミット or 2 コミットを想定)
- Step 6: 旧コード削除 (1 コミット、レビュー観点で diff が見やすい)

### G-3. 主版運用

現リポは `main` 直 push 運用で合意済み。本タスクは影響範囲が大きいので、初回は `feature/ws-s3-migration` ブランチを切って PR ベースで進めようと思っていますが、main 直 push のままで良いですか?

---

## H. その他

- 設計書 (`docs/06_api_websocket.md`, `docs/05_api_rest.md` 6.7) はサーバー側リポにあるとのことですが、エクスポートして共有いただけますか? (本タスク md と齟齬がないかの最終確認に使いたい)
- `unity_analysis_v2_2026-05-24.md` (タスク md 8 章で別途共有とあるもの) も併せていただけますか?

---

## 当方からの作業着手予定

回答をいただき次第:

1. (即) Step 1 完了報告として、本 md への回答を反映した「合意済み仕様サマリ」を作成
2. Server/ ASP.NET 退避方針が合意なら、`archive/csharp-server` ブランチ作成 + main から削除
3. Step 2 (PvpWebSocketClient.cs 新規実装) 着手
4. 以降タスク md の Step 順で進行、各 Step 完了時に進捗レポート md を出力

回答の形式は本 md に「→ K's answer:」で追記してもらう形でも、別 md に番号 (A-1, B-3 等) を引いた回答書でも構いません。

よろしくお願いします。
