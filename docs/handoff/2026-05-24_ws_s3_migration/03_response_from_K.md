# 質問書への回答 — WebSocket + S3 移行は短期棚上げ、現状維持で続行

回答日: 2026-05-24
回答者: K (PVPharmonics サーバー担当)
対応: `unity_websocket_s3_migration_questions.md` への回答

---

## 0. 結論(最優先で読むこと)

**WebSocket + S3 PUT 移行タスクは短期的に棚上げします。**

理由:

- PVPharmonics サーバー(Go)は Phase 2c(認証のみ)まで実装済み、PVP 関連は未実装
- `/api/v1/pvp/*` および `/api/v1/pvp/match/{matchId}/ws` は数ヶ月後の実装予定
- 現時点で Unity 側を WebSocket + S3 PUT に変更しても、接続先が存在しない

短期方針(Go サーバー完成まで):

- Unity 側は **現状維持**(HTTP polling + base64 JSON + C# サーバー接続)
- `Server/`(ASP.NET Core)は **当面ローカル検証用に維持**、削除や archive 退避は不要
- `unity_websocket_s3_migration_task.md` の Step 2 以降には **着手しない**
- 現コードは触らず、Unity 側の機能拡充・ゲームコンテンツ実装に集中

長期方針(Go サーバー Phase 3〜6 完成後、おそらく数ヶ月後):

- Go サーバー(PVPharmonics)に置き換え
- そのタイミングで Unity 側を WebSocket + S3 PUT に書き換え
- C# サーバー(`Server/`)はその時点で archive 退避を再協議

つまり質問書 B〜G(WebSocket スキーマ、S3 詳細、命名規約、エラー形式等)への詳細回答は、
**Go サーバー完成後の移行時に改めて作成する**。今は棚上げする。

質問書の内容自体は良い指摘ばかりなので、K の手元で保管し、Go の設計に反映する。

---

## A. 各項目への回答

### A-1. 現リポ `Server/`(ASP.NET Core)の扱い

選択: **当面ローカル検証用に維持**

- `Server/` は main ブランチに残したまま、削除・archive 退避は不要
- 当面、Unity 開発時のローカル検証用として継続稼働
- Go サーバーが Phase 3〜6 まで完成した時点で archive 退避を再協議

注意: C# サーバーへの機能追加は最小限に抑えること。Go サーバー完成時に廃止する前提なので、新規機能を追加してもいずれ捨てることになる。バグ修正や Unity 開発上必要な微調整に留める。

### A-2. PVPharmonics の現状

| 項目 | 回答 |
|---|---|
| 言語 | Go(net/http + ServeMux + pgx/v5 + sqlc + go-redis/v9) |
| リポ URL | github.com/mashikanimashi-commits/pvpharmonics-server(Private、Castella0107 に Read アクセス付与済み) |
| 現状 | Phase 2c 完了、`/api/v1/auth/*` と `/api/v1/users/me` のみ動作 |
| `/api/v1/ping` | 未実装(Phase 2.5 で追加予定、近日中) |
| `/api/v1/pvp/match/{matchId}/ws` | 未実装(Phase 5 で実装予定) |
| 起動方法 | リポジトリ clone + docker-compose up でローカル起動可、ただし PVP エンドポイントなし |
| 動作可能タイムライン | Phase 3 着手 〜 Phase 6 完成まで数ヶ月 |
| 公開タイミング | ConoHa VPS 契約予定、本番 URL は後日共有 |

短期的に Unity から Go サーバーに接続する必要はない(現状の C# サーバー接続を継続)。

### A-3. Domain 層(Pure C#)bit-perfect 同期方針

Go 側の方針:

- `Glicko2Calculator` を Go で再実装、Pure C# 実装を仕様書として参照
  - 定数: τ=0.5、Scale=173.7178、ε=1e-6、Illinois 上限 100 回 を採用
- `JudgmentRunner` および `ScoreCalculator` を Go で再実装、Pure C# 実装を仕様書として参照
  - マイクロポイント方式 `X_micro = ceil(10^12 / N)` を採用
  - 判定種別 5 種(PerfectPlus / Perfect / Great / Good / Miss)、判定窓 ±16/33/50/83ms を採用
  - 難易度倍率 easy 0.75 / normal 0.80 / hard 0.90 / extra 1.00 を採用

bit-perfect 同期について:

- 完全な bit-perfect 一致は **保証しない方針**
  - 理由: C# `double` と Go `float64` の演算順序差、`Math.Round()` の挙動差等で IEEE 754 微小差が発生しうる
- 代わりに **整数演算ベース + 許容誤差** で検証
  - マイクロポイント(10^12 ベース)は整数で計算する設計に変更し、Glicko-2 以外は整数演算で bit-perfect 維持を目指す
  - Glicko-2 は浮動小数点が避けられないため、許容誤差(レーティング差 ±0.01 程度)を設けて判定

リプレイ検証の役割分担:

- Phase 6 着手時(数ヶ月後)に Go 側で `JudgmentRunner` 相当を実装
- 入力イベントベースのリプレイバイナリ(現状の C# 実装と同形式)を Go でデコード・スコア再計算
- クライアント送信スコアとの差が許容誤差を超えた場合に「不正検出」として扱う
- 完全一致は求めない

詳細仕様は Phase 6 着手時に Castella0107 と再協議する。

---

## B. WebSocket メッセージスキーマ

回答: **棚上げ**。Go サーバーで WebSocket エンドポイント実装時(Phase 5、数ヶ月後)に改めて回答する。

現状の HTTP polling(`/api/pvp/match/{matchId}/progress`)を継続使用してよい。

K の手元では質問書 B 全項目を保管し、Phase 5 設計時に参照する。

---

## C. Pre-signed URL(リプレイアップロード)

回答: **棚上げ**。Go サーバーで S3 PUT エンドポイント実装時(Phase 6、数ヶ月後)に改めて回答する。

現状の base64 JSON 送信(`/api/pvp/match/{matchId}/submit` で `replayDataBase64` インライン)を継続使用してよい。

K の手元では質問書 C 全項目を保管し、Phase 6 設計時に参照する。

---

## D. REST API 命名規約とプレフィックス

短期方針:

- 現状の Unity 側 DTO(camelCase)はそのまま維持
- C# サーバー(`Server/`)との連携も現状の規約で続行
- 今すぐ Unity DTO を snake_case に書き換える必要はない

長期方針(Go サーバー置換時):

- Go サーバー側 JSON は **snake_case** で統一(Go の慣例)
- Unity 側は `Newtonsoft.Json` の `[JsonProperty("user_id")]` 属性で対応
  - DTO 内部のフィールド名は camelCase のまま、JSON シリアライゼーション時に snake_case に変換
- `/api/v1` プレフィックス付き(Go サーバーは全エンドポイント `/api/v1/...` に統一済み)
  - 例外: `/health` のみルート直下(バージョニング対象外)

短期的にはこれらの変更は不要。Go サーバー置換時に対応する。

---

## E. エラーレスポンス形式

短期方針:

- 現状の C# サーバーのエラー形式(`SubmitResponseDto` の `Accepted=false, Error="..."` 等)で続行

長期方針(Go サーバー置換時):

- 成功時: `{ "data": { ... } }` でラップ
- 失敗時: `{ "error": { "code": "...", "message": "..." } }` 形式
- HTTP ステータスコードとエラーコードの対応関係は Go サーバー実装時に確定

短期的には変更不要。

---

## F. 既存 API・機能との関係

### F-1. ソロプレイ用 API

- `/api/replay/validate`(ソロのリプレイ検証 + PlayRecord 保存): 短期は C# サーバーで継続使用
- `/api/leaderboard/{songId}/{difficulty}`: 同上
- 長期方針: 両方とも Go サーバー(Phase 7 想定)に移植予定

### F-2. SubmissionQueue の永続化

短期: 現状のまま、PVP submit には未使用、`/api/replay/validate` 用のみで OK

長期(Go 移行時): PVP submit が S3 PUT 化される時に、3 段階の失敗(URL 取得失敗 / PUT 失敗 / 完了通知失敗)に拡張するかは再協議。ソロも S3 PUT 化するかも同時に決める。

### F-3. テストの方針

- 既存 EditMode テスト(Pvp/Glicko2CalculatorTests, MatchScoringTests 等)は Domain 単体テストなので影響なし、そのまま維持
- 通信側のテストは短期は不要(現状動いているコードを変更しないため)
- 長期(Go 移行時)に WebSocket / S3 のモックサーバー設計を再協議

---

## G. スケジュール・運用

### G-1. Step 着手のブロッカー

回答: **Step 2 以降の着手は不要**。短期方針として WebSocket + S3 移行を棚上げするため、`unity_websocket_s3_migration_task.md` の Step 2〜6 は全て保留する。

### G-2. コミット粒度

回答: 短期方針では大きなコミットは発生しないため、現状の main 直 push 運用で OK。

### G-3. main 直 push 運用

回答: 現状維持で OK。短期的に大きな書き換えは発生しないため、`feature/ws-s3-migration` ブランチを切る必要なし。

---

## H. その他

### 設計書の共有

- `docs/05_api_rest.md`, `docs/06_api_websocket.md` 等は Phase 2.5 で v0.2 化作業を予定
- v0.2 化後にリポジトリで共有(Read アクセスから docs/ ディレクトリを直接参照可)
- 質問書の内容を反映した形で更新する

### `unity_analysis_v2_2026-05-24.md` の共有

- K の手元のレポート、近日中にリポジトリ `docs/reviews/` 配下に配置予定
- 配置後、Read アクセスから参照可能になる

---

## 今後のクライアント担当者(Castella0107)への依頼

### 短期(数ヶ月、Go サーバー完成まで)

- Unity 側の機能拡充に集中(ゲームコンテンツ、UI 改善、追加譜面、エディタ機能完成等)
- C# サーバー(`Server/`)は現状維持、機能追加は最小限
- `NetworkClient.cs` の通信レイヤー抽象化を **余裕があれば** 推奨
  - 例: `INetworkClient` インターフェースを切り、現在の実装を `RestNetworkClient` とする
  - 将来 `WebSocketNetworkClient` に切り替える時の差し替えコストを下げる
  - これは強い要請ではない、可能な範囲で

### 長期(Go サーバー完成後)

- WebSocket + S3 PUT への書き換え依頼を改めて出す
- そのタイミングで質問書 B〜G への詳細回答を提供
- C# サーバーの archive 退避を実施

---

## まとめ

| 項目 | 短期対応 | 長期対応 |
|---|---|---|
| Unity 通信実装 | 現状維持(HTTP polling + base64 JSON) | WebSocket + S3 PUT 化 |
| 接続先 | C# サーバー(`Server/`) | Go サーバー(PVPharmonics) |
| C# サーバー | 維持、機能追加は最小限 | archive 退避 |
| Go サーバー | Phase 3〜6 を実装中 | 完成、本番採用 |
| 質問書 B〜G | 棚上げ | Go 完成時に詳細回答 |
| `unity_websocket_s3_migration_task.md` | Step 2 以降は着手しない | 改めて指示書を発行 |

不明点や追加質問があれば、棚上げ前提でも質問してくれて構わない。

以上。
