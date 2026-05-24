# タスク: Unity クライアントの通信方式を WebSocket + S3 Pre-signed URL に変更

## 1. 背景

K さん(PVPharmonics サーバー側担当)と合意した方針:

- **対戦中通信**: 現在の REST HTTP ポーリング(0.5 秒間隔)を WebSocket に変更
- **リプレイ送信**: 現在の base64 JSON インライン送信を S3 Pre-signed URL + PUT に変更

理由:

### WebSocket への変更理由

- REST ポーリングは同時 100 試合で 400 req/sec の負荷、スケーラビリティに難
- 0.5 秒間隔の固定ポーリングは無駄なリクエストが多い(進捗変化なしでも送信)
- サーバー push 型の方が、進捗以外の通知(対戦相手切断、サーバー側エラー等)も自然に扱える
- サーバー側設計書(`docs/06_api_websocket.md`)が WebSocket 前提で書かれている
- 将来的なリアルタイム機能拡張(リアクション、ボイスチャット連携等)の余地を残せる

### S3 Pre-signed URL への変更理由

- リプレイサイズ 1.5MB × 3 曲 = 約 4.5MB を base64 化すると約 6MB → JSON ペイロード過大
- サーバーが大きい JSON を受け取る負担(メモリ消費、デコード処理)
- Cloudflare R2 のエグレス無料を活かせない(サーバー経由になるため帯域消費)
- リトライ時の効率(URL 再発行で再 PUT のみ、JSON 全体再送不要)
- サーバー側設計書(`docs/05_api_rest.md` 6.7)で採用済みの方式

## 2. 変更対象

### A. 対戦中通信を WebSocket 化

**現状(変更前)**:

- `NetworkClient.cs:SendPvpProgressAsync` / `FetchPvpProgressAsync` で POST/GET `/api/pvp/match/{matchId}/progress`
- `PvpProgressOverlay.cs` が 0.5 秒間隔でポーリング

**変更後**:

- WebSocket クライアント(`System.Net.WebSockets.ClientWebSocket` 推奨)を新規追加
- 接続先: `ws://localhost:8080/api/v1/pvp/match/{matchId}/ws`(本番は wss://)
- 接続後即、認証メッセージを送信(後述の認証フロー参照)
- 進捗送信(C→S): 自分の進捗が変化したタイミングでメッセージ送信(0.5 秒間隔の固定ポーリング廃止)
- 進捗受信(S→C): サーバー push 型で相手の進捗を受信

**WebSocket メッセージ仕様(暫定、K さんと最終確定するため要相談)**:

C → S:

```json
{
  "type": "auth",
  "user_id": "u_xxxxx"
}
```

(将来 Phase 4-B で JWT 化、今は user_id のみ)

```json
{
  "type": "progress",
  "song_index": 1,
  "percent_x1000": 45000,
  "score": 234567
}
```

S → C:

```json
{
  "type": "opponent_progress",
  "song_index": 1,
  "percent_x1000": 43200,
  "score": 220000
}
```

```json
{
  "type": "match_state",
  "state": "started" | "song_completed" | "match_finalized" | "opponent_disconnected"
}
```

```json
{
  "type": "error",
  "code": "UNAUTHORIZED" | "MATCH_NOT_FOUND" | ...,
  "message": "..."
}
```

**接続管理**:

- 接続失敗時のリトライ(指数バックオフ、最大 3 回)
- 切断検知(ping/pong、または HeartBeat)
- 再接続時の状態同期(初回メッセージで現在の自分の進捗を送信)
- マッチ終了時に明示的に Close

### B. リプレイ送信を S3 Pre-signed URL + PUT に変更

**現状(変更前)**:

- `NetworkClient.cs:SubmitMatchAsync` で `SubmitMatchRequestDto.songs[].replayDataBase64` を含む JSON を POST `/api/pvp/match/{matchId}/submit` に送信
- 3 曲分のリプレイバイナリを base64 化してまとめて送信

**変更後**:

2 段階のフローに変更:

**段階 1: Pre-signed URL の取得**

```
POST /api/v1/pvp/match/{matchId}/replay-urls
Authorization: Bearer <jwt> (Phase 4-B 以降)
Content-Type: application/json

{
  "user_id": "u_xxxxx",
  "songs": [
    { "song_id": "test_song",   "size_bytes": 1234567 },
    { "song_id": "test_song_1", "size_bytes": 2345678 },
    { "song_id": "test_song_2", "size_bytes": 3456789 }
  ]
}
```

レスポンス:

```json
{
  "data": {
    "upload_urls": [
      {
        "song_id": "test_song",
        "url": "https://r2.example.com/replays/.../song1.bin?signature=...",
        "expires_at": "2026-05-24T12:00:00Z",
        "headers": {
          "Content-Type": "application/octet-stream"
        }
      },
      {
        "song_id": "test_song_1",
        "url": "https://r2.example.com/replays/.../song2.bin?signature=...",
        "expires_at": "2026-05-24T12:00:00Z",
        "headers": {
          "Content-Type": "application/octet-stream"
        }
      },
      {
        "song_id": "test_song_2",
        "url": "https://r2.example.com/replays/.../song3.bin?signature=...",
        "expires_at": "2026-05-24T12:00:00Z",
        "headers": {
          "Content-Type": "application/octet-stream"
        }
      }
    ]
  }
}
```

**段階 2: 各 URL に対してバイナリを PUT**

```
PUT <upload_url>
Content-Type: application/octet-stream

<リプレイバイナリそのまま、base64 エンコード不要>
```

3 曲分すべて PUT 完了したら段階 3 へ。

**段階 3: アップロード完了通知**

```
POST /api/v1/pvp/match/{matchId}/submit
Authorization: Bearer <jwt> (Phase 4-B 以降)
Content-Type: application/json

{
  "user_id": "u_xxxxx",
  "song_uploads_confirmed": [
    "test_song",
    "test_song_1",
    "test_song_2"
  ]
}
```

レスポンス: 現状の `SubmitMatchResponseDto` と同じ構造(`accepted`、`matchFinalized`、`result` 等)。

**重要な変更点**:

- `SubmitMatchRequestDto.songs[].replayDataBase64` フィールド廃止
- リプレイバイナリ自体は **base64 エンコード不要**(`PUT` 時にそのままバイナリ送信)
- リプレイファイルパス(`File.ReadAllBytes(replayPath)` で読み込んだ生バイナリ)を直接 PUT する

**エラーハンドリング**:

- Pre-signed URL の有効期限切れ(`expires_at` 経過後の PUT は失敗)→ URL 再取得して再 PUT
- PUT 失敗(ネットワークエラー、5xx)→ 該当の URL のみ再 PUT
- 段階 3 の `/submit` 失敗 → エラーに応じて段階 1 からやり直し

**SubmissionQueue.cs との統合**:

- リプレイ送信失敗時、現在は `persistentDataPath/submission_queue/{playId}.json` に保存
- 新方式では「Pre-signed URL 取得失敗」「PUT 失敗」「完了通知失敗」の 3 種類のエラーを区別して保存
- 再送時は各エラーの段階から再開

## 3. 影響を受ける主要ファイル

以下のファイル(およびその関連テスト)を変更:

- `Assets/_Project/Scripts/Network/NetworkClient.cs`
  - `SendPvpProgressAsync` / `FetchPvpProgressAsync` 削除
  - `SubmitMatchAsync` を 3 段階フローに分割または書き換え
  - 新規: `RequestReplayUploadUrlsAsync`、`UploadReplayBinaryAsync`、`ConfirmReplayUploadAsync`

- `Assets/_Project/Scripts/Network/NetworkDtos.cs`
  - `SubmitMatchRequestDto` / `SubmitMatchSongDto` 削除または書き換え
  - 新規: `ReplayUploadUrlsRequestDto`、`ReplayUploadUrlsResponseDto`、`SongUploadUrlDto`、`SubmitConfirmRequestDto`

- 新規ファイル: `Assets/_Project/Scripts/Network/PvpWebSocketClient.cs`
  - WebSocket 接続管理、メッセージ送受信、再接続ロジック
  - C# の `System.Net.WebSockets.ClientWebSocket` を使用

- `Assets/_Project/Scripts/UI/Pvp/PvpProgressOverlay.cs`
  - HTTP ポーリングロジック削除
  - PvpWebSocketClient からのコールバックで進捗を更新

- `Assets/_Project/Scripts/Network/PvpFlowController.cs`
  - `SubmitAndFinish` を 3 段階フロー(URL 取得 → 3 曲 PUT → 完了通知)に書き換え
  - `PollUntilFinalizedAsync` は `/api/v1/pvp/match/{matchId}` の GET を維持(WebSocket 切断時のフォールバック)
  - WebSocket 接続のライフサイクル管理(StartMatch で接続、FinishToMatchEndScene で切断)

- `Assets/_Project/Scripts/Network/SubmissionQueue.cs`
  - 失敗状態の表現を 3 種類(URL 取得失敗 / PUT 失敗 / 完了通知失敗)に拡張
  - 再送ロジックを各段階対応に書き換え

- `Assets/_Project/Tests/EditMode/Network/` 配下
  - 既存テストの更新
  - 新規 WebSocket クライアントのテスト追加(モックサーバーまたはローカル WebSocket サーバーで検証)

## 4. 実装の進め方

以下の順序で進めること:

### Step 1: 設計確認(コード変更前)

- 本指示書を完全に読み込み、影響範囲を把握する
- 既存の `NetworkClient.cs`、`PvpFlowController.cs`、`PvpProgressOverlay.cs`、`SubmissionQueue.cs`、`NetworkDtos.cs` を読み込み、現状の実装を理解する
- 質問・不明点があれば、コード変更を開始する前に Markdown でまとめて報告する
- サーバー側 API 仕様で曖昧な部分(WebSocket メッセージスキーマの詳細、エラーコード一覧、Pre-signed URL の expires_at の長さ等)があれば質問にまとめる

### Step 2: PvpWebSocketClient.cs の新規実装

- 新規ファイルとして実装
- `System.Net.WebSockets.ClientWebSocket` を使用
- 接続、認証、メッセージ送受信、再接続、切断のライフサイクルを実装
- 単体テスト追加(可能なら)

### Step 3: PvpProgressOverlay.cs の書き換え

- HTTP ポーリングを削除
- PvpWebSocketClient からの進捗受信コールバックで OnGUI 更新
- 既存テストの更新

### Step 4: リプレイ送信の S3 PUT 化

- NetworkDtos.cs の DTO 変更
- NetworkClient.cs に新規メソッド追加
- PvpFlowController.cs の SubmitAndFinish を 3 段階フローに書き換え
- SubmissionQueue.cs の失敗状態拡張

### Step 5: 統合テスト

- ローカルの Go サーバー(`http://localhost:8080`)に対して PVP 開始 → 試合進行 → リプレイ送信 → match 完了までの end-to-end フローを手動テスト
- サーバー側がまだ実装していない場合(Phase 3〜6 の実装待ち)は、モックサーバーで動作確認

### Step 6: 古いコードの削除

- `SendPvpProgressAsync` / `FetchPvpProgressAsync` の削除
- 旧 `SubmitMatchRequestDto.replayDataBase64` 関連の削除
- 関連する未使用コードのクリーンアップ

## 5. ServerConfig.cs の更新

現状: `DefaultBaseUrl = "http://localhost:5246"` (ASP.NET Core デフォルト)

PVPharmonics サーバーは Go + ポート 8080 で稼働するため、開発時の Unity から見た接続先を更新:

- `DefaultBaseUrl = "http://localhost:8080"` (REST 用)
- `DefaultWebSocketBaseUrl = "ws://localhost:8080"` (WebSocket 用、新規追加)
- `DefaultApiPrefix = "/api/v1"` (新規追加、サーバー側のバージョニング規約に合わせる)
- 全エンドポイントパスに `/api/v1` プレフィックスを追加(/api/ping → /api/v1/ping のように)

PlayerPrefs で上書き可能な設計は維持。

## 6. 進捗報告

各 Step 完了時に、何を変更したかを Markdown で報告すること。最終的に変更ファイル一覧と動作確認結果をまとめたサマリレポートを作成する。

K さんがレビューしやすいよう、コミット粒度は機能単位(Step 単位)で分割すること。

## 7. 注意事項

- **PVPharmonics サーバー側リポジトリへの操作は一切禁止**(別リポジトリ、アクセス権なし)
- 本リポジトリ(unity1)への変更は許可されている、コミット・push も実施して OK
- 設計に迷う部分は質問する(独断で進めない)
- 既存の Pure C# Domain Layer(MatchScoring、Glicko2Calculator 等)は変更しない(ローカルテスト用に維持)
- ChartEditor 機能(Domain/ChartEdit/)には触らない
- AccountTabController の認証スタブには触らない(Phase 4-B で別途実装)

## 8. 参考情報

- サーバー側分析レポート: K さんから別途共有(unity_analysis_v2_2026-05-24.md)
- WebSocket メッセージスキーマ詳細: 現状暫定、K さんと合意後に最終化
- 既存実装:
  - `/Assets/_Project/Scripts/Network/NetworkClient.cs`
  - `/Assets/_Project/Scripts/Network/NetworkDtos.cs`
  - `/Assets/_Project/Scripts/Network/PvpFlowController.cs`
  - `/Assets/_Project/Scripts/Network/PvpProgressOverlay.cs`
  - `/Assets/_Project/Scripts/Network/SubmissionQueue.cs`
  - `/Assets/_Project/Scripts/Network/ServerConfig.cs`

## 9. 質問事項テンプレート

Step 1 完了時、以下のテンプレートで質問を整理して報告:

```
## サーバー側 API 仕様確認事項

### A. WebSocket メッセージスキーマ
- (例)error メッセージの code 一覧は何が想定されているか?
- (例)match_state の state 値は started/song_completed/match_finalized/opponent_disconnected の 4 種類で確定か、追加があるか?
- (例)サーバーからの ping/pong 間隔の想定値は?

### B. Pre-signed URL
- (例)expires_at の長さの想定(数分?数十分?)
- (例)PUT 失敗時の URL 再取得制限はあるか?
- (例)既にアップロード済みの song_id に対して再 PUT した場合の挙動は?

### C. エラーレスポンス形式
- (例)HTTP のエラーレスポンスは {data: {...}} ではなく {error: {...}} の形式か?
- (例)WebSocket のエラーメッセージ後の接続は維持されるか、自動切断か?

### D. その他
- (今回の変更で発生した不明点を箇条書き)
```

これらが解決してから Step 2 以降に進む。

## 10. ゴール

- WebSocket ベースの対戦中通信が動作する
- S3 Pre-signed URL + PUT 方式でリプレイ送信が動作する
- 旧方式(REST ポーリング、base64 JSON)のコードが削除される
- 既存テストが通る、新規テストが追加される
- K さんがレビュー可能な状態でコミット・push される
