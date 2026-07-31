# リーダーボード設計書 (クライアント側)

| 版 | 日付 | 内容 |
|---|---|---|
| 0.1 | 2026-07-30 | 初版 (Claude 仮案 — K レビュー待ち)。サーバー側: pvpharmonics-server/docs/leaderboard_design.md |

> **状態: 仮案 / 実装済み。** 本書どおりに実装してあり、K レビューで設計変更が入れば実装も追従する。
> DTO はサーバーの phase7_api_dto_spec.md v1.0 (+ leaderboard_design.md §7 の変更) に従う。

---

## 1. 全体像

```
[ソロプレイ終了]
  GamePlayController ─ リプレイ保存後 ─→ ScoreSubmitService.SubmitAsync()   (裏で非同期・失敗はログのみ)
                                             │ POST /score/validate (chart_id + replay + claim)
                                             ↓
                                       サーバーが再判定して chart_best_scores へ UPSERT

[選曲画面 → R キー]
  SongRankingController ─→ LeaderboardApi.GetLeaderboardAsync(songId, diff)      … 上位 20 件
                        └→ LeaderboardApi.GetPersonalBestAsync(songId, diff)     … YOUR RANK
```

## 2. API クライアント — `Network/Api/LeaderboardApi.cs` (新規)

PvpApi と同一規約 (ApiClient.GetAsync / PostAsync、`{"data":...}` アンラップ)。

```csharp
GetLeaderboardAsync(string songId, string difficulty, int limit = 20, int offset = 0)
    → GET /leaderboard/{songId}/{difficulty}?limit=&offset=      : LeaderboardDto
GetPersonalBestAsync(string songId, string difficulty)
    → GET /leaderboard/{songId}/{difficulty}/personal-best        : PersonalBestFetchDto
```

DTO (`ApiDtos.cs` に追加、フィールドはサーバー spec の snake_case に対応):

- `LeaderboardDto { song_id, difficulty, season_id, entries[], total }`
- `LeaderboardEntryDto { rank_position, user_id, display_name, score, rank, achieved_at }`
- `PersonalBestFetchDto { song_id, difficulty, season_id, personal_best (null 可) }`
- `PersonalBestDto { rank_position, score, rank, perfect_plus..miss, max_combo, achieved_at }`

## 3. スコア提出 — `Network/Api/ScoreSubmitService.cs` (新規)

- 呼び出し元: `GamePlayController` のセッション終了処理 (リプレイ保存直後)。
- **送信条件** (すべて満たす場合のみ):
  1. ソロプレイ (PVP は対象外 — PVP は /matches/submit 経由で別管理)
  2. オートプレイでない / リプレイ再生でない
  3. ログイン済み (AuthManager)
  4. `ServerSongLibrary.TryGetChartId(songId, difficulty, out chartId)` が成功
     (= サーバーが配信している譜面。ローカルのみの譜面は送らない)
- 送信内容: `chart_id` + `replay_base64` (保存したものと同一バイナリ) + `claim` (実測の
  score/rank/判定内訳/max_combo — サーバーの claim_matched 検証ログ用)
- 失敗時: リトライせずログのみ (`[ScoreSubmit] ...`)。プレイ体験をブロックしない
  (await しない fire-and-forget。オフライン時は次のプレイでまた試みるだけ)。
- レスポンスの `best_updated` を Result 画面で使えるよう `ScoreSubmitService.LastResult` に保持
  (今回は表示未実装 — 将来「BEST 更新!」バッジ用のフック)。

## 4. ServerSongLibrary の拡張

- 同期時 (`SyncCoreAsync` 手順 2) に `charts[].chart_id` を `(songId, difficulty) → chartId` の
  辞書へ保持し、`TryGetChartId` で参照できるようにする。
- オフラインキャッシュ (`IndexFile`) にも chart_id を含め、キャッシュ復元でも参照可能にする。

## 5. ランキング画面 — `SongRankingController` の結線

既存の COMING SOON スタブ (`LoadAsync`) を実 API に置換:

1. `GetLeaderboardAsync(songId, diff, limit: 20)` → `_rows[i].Bind(rank_position, display_name, score, rank)`
   - 自分のエントリ (`user_id == AuthManager.UserId`) は行をハイライト
   - 0 件: 「まだ記録がありません — 最初の記録を作ろう!」
   - エラー: 「ランキングを取得できませんでした ({code})」
2. `GetPersonalBestAsync` → フッター `YOUR RANK  #12   870,000  (B)`。未登録なら「YOUR RANK  —」
3. 既存の操作系 (↑↓スクロール / ESC 復帰 / OnGUI フォールバック) は不変。

RankingRowView は既存のまま利用する (Bind の引数形状は既存実装に合わせる)。

## 6. テスト / 検証

- EditMode: 送信条件判定を Domain の純関数 `ScoreSubmitPolicy.ShouldSubmit` に切り出し、
  `ScoreSubmitPolicyTests` (6 ケース) でテスト。
  ※ DTO デシリアライズは EditMode テストの対象外 (テスト asmdef が Domain のみ参照のため) —
  JSON 形状の正しさはサーバー側 Go テスト (handler_leaderboard_test.go) が担保する。
- 実機: SongRanking シーンをプレイモードで起動しスクリーンショット確認済み
  (`Assets/Screenshots/lb_ui_screencap.png` — ヘッダー/列/フッター描画、API 呼び出し到達、
  本番サーバー未デプロイのためエラーステータス表示までを確認)。
- **検証結果 (2026-07-30 深夜)**: コンパイルエラーなし、EditMode テスト 300/300 緑
  (ScoreSubmitPolicyTests 6 件含む)。
- **デプロイ注意**: 本番でリーダーボードが機能するには、サーバーの migration 011 適用+新ビルドの
  デプロイが必要 (それまでは「ランキングを取得できませんでした」表示になる)。
  シーズンが未作成の環境では NO_ACTIVE_SEASON → 「シーズンが開始されていません」表示。

## 7. 仮案の設計判断 (K 要レビュー)

1. 提出タイミングは「プレイ終了直後・非同期」 — Result 画面での明示ボタンにしない
   (プレイヤー操作なしで常に集計される方が音ゲーの通例に合う)。
2. 失敗時リトライ無し (オフラインキュー無し) — 初版はシンプルに。必要なら再送キューを後付け。
3. テストソング (Test Song *) も提出対象に含める (PVP 除外とは別問題。ランキングに出したくなければ
   サーバー側 or 表示側でのフィルタを別途指示ください)。
