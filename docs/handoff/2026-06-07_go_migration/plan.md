# Go サーバー(PVPharmonics)移行計画 — クライアント大幅改修

作成: 2026-06-07 / 根拠: pvpharmonics-server@53da6c4(docs v0.4〜1.0 + 実コード確認)
K からの指示: 「C#.NET 仮サーバーから本番サーバーに置き換え、付随する修正案を提案もしくは実施」

---

## 1. サーバー完成範囲(実コードのルート登録で確認済み)

**実装済み**: 認証(register/login/refresh/logout, JWT 1h+リフレッシュ30日Rotation) / users(me GET・PATCH, {user_id}) /
songs・charts(一覧/詳細/ダウンロード, chart_hash=ファイルSHA-256) / PVPキュー(join[difficulty_preference]/status/leave, レート差±100→無制限拡大) /
matches(prematch/ready/pick/ban/state/submit/result) / シーズン(admin作成・切替+レートリセット, current) /
**WebSocket** `/ws/match/{id}`(auth→ready同期→progress中継→heartbeat→切断グレース) /
**リプレイ検証エンジン**(internal/engine+replay, C#リプレイのgoldenテスト済, bit-perfect, 申告値はengine再計算で上書き) /
forfeit・タイムアウト自動処理 / 本番Docker化+運用Runbook

**未実装(Phase 6/7 の残り)**: `/score/validate`(ソロ検証) / `/score/personal-best` / `/leaderboard/{song}/{diff}` /
`/rankings/global` / `/users/me/matches`(履歴) / `/users/me/stats`

**シード**: song_001〜003(Test Song Alpha/Beta/Gamma)×4難易度=12譜面、Season 1(pool=3曲)有効

---

## 2. プロトコル差分対照表(C#仮サーバー → Go 本番)

| 項目 | C# 仮サーバー(現クライアント) | Go 本番 |
|---|---|---|
| ベースURL | `/api/...` | `/api/v1/...`(`/health` のみルート) |
| 命名 | camelCase | **snake_case** |
| 封筒 | 生JSON | 成功 `{"data":{...}}` / 失敗 `{"error":{code,message,details}}` |
| 認証 | なし(userId 文字列を自己申告) | **JWT Bearer 必須(songs 含むほぼ全API)**、401→refresh→再試行 |
| user_id | 任意文字列(alice等) | `u_`+12英数(UUID も受理=C#互換) |
| キュー参加 | `{userId}` | `{difficulty_preference}` + JWT、`status` 1.5s poll(poll毎に再マッチ試行) |
| ドラフト | 両者ブラインドPICK(20曲)→BAN(3候補)→3曲確定→各曲前に難易度選択 | **交互ターン制+曲ごと即プレイ**(下記§3) |
| 対戦中進捗 | HTTP polling(PvpProgressOverlay) | **WebSocket** progress 0.5s / opponent_progress 中継 |
| スコア送信 | 曲ごと replay base64(検証=曲ハッシュのみ) | 曲ごと `sector_scores[5]`+`sector_tie_breaks[5]`(=2×PP+P)+`total_score`+`replay_base64`+`claim`。**engine再計算値で上書き** |
| 採点 | pt小数(1/0.5/0、満点15) | **ミリポイント整数**(1000/500/0、満点15000、クリンチ≥8000)、実効スコア=sectorScore×難易度倍率(750/800/900/1000) |
| レート | Glicko-2 セクター毎? (旧) | **1試合=1ゲーム+margin_factor(=0.5+0.5×|diff|/15000, Rのdeltaのみに乗算)** |
| forfeit | 未対応(K領域だった) | **実装済み**(ready 20s/draft切断30sグレース/play=submit 120sタイムアウトで0点自動処理、result に forfeit/forfeit_reason/forfeited_player) |
| 譜面 | クライアントローカル(宣言ハッシュ"0..01") | サーバー登録制。chart_hash=**譜面ファイルのSHA-256**。replay の chart_hash 照合+譜面でengine再計算 |
| 譜面形式 | camelCase(`timeMs`/`lane:"0"`/events) | snake_case(`time_ms`/`lane:0-5`)。**サーバーのパーサーは両形式のlane/string互換あり**、ただし hash はファイルバイト一致が前提 |
| 開始同期 | なし(各自即開始) | ready→start(WS)。※05/08設計のstart_at未来時刻方式はWS仕様v1.0では未採用(start通知のみ) |

## 3. 新ドラフトフロー(8.6 実装確定)とフェーズ・タイムアウト

```
pre_match(ready 両者, WS 20s / REST 30s)
→ pick_song1   (下位レート: 曲+自分の難易度, 60s)
→ pick_song1_h (上位レート: 自分の難易度を後出し, 30s)
→ play_song1   (両者 submit 待ち, 120s)
→ pick_song2   (上位: 曲+難易度, 60s)  ※Song1と同曲は409
→ pick_song2_l (下位: 難易度, 30s)
→ play_song2   (120s)  ※両者submit後クリンチ判定(累積≥8000で終了)
→ ban_song3    (プール4-5曲から 下位→上位 の順に各1曲BAN, 30s)
→ pick_song3_diff (確定したSong3の難易度を両者ブラインド選択, 30s)
→ play_song3   (120s)
→ finished / aborted
```
- タイムアウトは `GET /state` 呼び出し時にサーバーが自動処理(ランダム選択/0点submit等) → クライアントは state polling を切らさないこと
- `acting_player` で手番表示。`phase_deadline`(RFC3339)でタイマー表示
- 切断: draft中=30sグレース(再接続可)、play中=表示専用(120s submitタイムアウトが処理)

### 画面への影響(現行 → 新)
| 現行画面 | 新役割 |
|---|---|
| PVPPrematch(自動進行) | **READY画面に再編**: 両者プロフィール+レート+**変動予測(win/lose/draw)**表示、READY/辞退、WS ready_ack で相手状態表示 |
| PVPSongPick(20曲ブラインド) | **ターン制ピック画面**: シーズンプール表示、自分の手番=曲+難易度選択/相手の手番=待機表示(後出し難易度選択含む)。Song1/Song2 で使い回し |
| PVPBanPhase(3候補+LINEUP発表) | **順番BAN画面**(下位先行)+Song3確定表示 → **ブラインド難易度選択**(現PVPSongSetupの簡略版を統合可) |
| PVPSongSetup(曲ごと難易度+設定) | 難易度選択はピックフェーズへ移動。**プレイ設定(速度等)+READY確認の画面として縮小** or 廃止してプレイ直行 |
| GamePlay(PVP) | WS progress 送信+**相手ライブスコア受信→相手情報ボックス実データ化**(現プレースホルダー解消) |
| PVPSongResult | submit レスポンスの song_result(ミリポイント)表示。クリンチ対応は現行同様 |
| PVPMatchEnd | GET result: rating_change、**forfeit表示**(現「K領域未対応」が解消) |

## 4. 改修ワークストリーム

- **WS-A 通信基盤**: `ApiClient` 新設 — `/api/v1`+snake_case+封筒+Bearer+**401→refresh→1回再試行**+429 Retry-After。`NetworkClient`(REST部分)を置換
- **WS-B 認証**: `AuthManager`(トークン保存=PlayerPrefs、refresh rotation、user_id/display_name)+登録/ログインUI。`LocalIdentity` 置換
- **WS-C WebSocket**: `MatchSocketClient` 新設(auth 5s/ready/progress 0.5s/opponent_progress/ping 15s/再接続30sグレース)。Unity標準 ClientWebSocket
- **WS-D PVPフロー**: `PvpFlowController` v2(state polling+phase状態機械)+§3の画面再編
- **WS-E 採点表示**: submit 形式(claim含む)、ミリポイント表示変換、margin/forfeit表示。Domain `MatchScoring` はサーバー正なので**表示専用に降格**
- **WS-F 譜面**: サーバー譜面の取得・形式変換(`time_ms`→ChartData)・キャッシュ・chart_hash 整合。リプレイ記録の chartHash をサーバー値に
- **WS-G 影響画面**: ランキング/ロビー統計/History(Ladder) — Phase 7 待ち(§6)
- **WS-H 開発環境**: Go サーバーのローカル起動手順(docker compose)、ServerConfig の URL/トークン設定、テスト用アカウント

## 5. マイルストーン(各々動作確認可能な単位)

| M | 内容 | 完了条件 |
|---|---|---|
| M1 | WS-A+B: 通信基盤+認証+ログインUI | 登録→ログイン→/users/me 表示→refresh 動作 |
| M2 | WS-F: songs 同期+譜面DL+hash 整合 | サーバー3曲がクライアントで選択・プレイ可能(ソロ) |
| M3 | WS-C+キュー+Prematch | マッチ成立→WS ready同期→start 受信 |
| M4 | WS-D+E: ドラフト+対戦+submit+result | alice vs bob で新フロー e2e 完走(レート変動まで) |
| M5 | 切断/forfeit/再接続/タイムアウト演出 | グレース再接続・不戦勝表示の実機確認 |
| M6 | WS-G+整理: ロビー/History整合、C#サーバー参照排除 | 全画面がGoサーバーのみで動作(Phase 7 部分はCOMING SOON) |

## 6. 要決定事項と推奨

1. **移行方式**: **big-bang 推奨**。新ドラフトは画面構造ごと別物で、C#/Go 両対応は実質二重実装。`Server/`(C#)はフォルダ残置のみ(参照用)、接続コードは削除
2. **認証UI**: **起動時ログイン画面 推奨**(Bootstrap→Login→Title、トークン有効なら自動スキップ、「オフラインで続行」でソロのみ可)。songs API 含めほぼ全APIが requireAuth のため
3. **譜面データ**: **PVP曲はサーバーDL+変換+ローカルキャッシュ 推奨**(chart_hash 一致が検証の前提)。音源/ジャケットは当面クライアント同梱(song_id で対応付け)。ソロ専用曲はローカル継続
4. **Phase 7 待ち画面**: **COMING SOON 表示+ローカルDB継続 推奨**(History はローカル10戦記録で動作継続、楽曲別ランキング/ロビー統計は準備中表示)。K に Phase 7 の予定時期を確認

## 6.5 ステージング環境(2026-06-07 K より受領: `client_integration_handoff.md`)

- **REST**: `https://pvpharmonics.duckdns.org`(TLS必須・Let's Encrypt正規証明書)/ **WS**: `wss://.../ws/match/{id}` / health: `/health`
- 疎通・register・login・/users/me・refresh(Rotation)を curl で**実地検証済み(2026-06-07)** — 本計画の DTO 想定と完全一致。テストアカウント: `castella-test@example.com`(u_5GQNQJXEM5DQ)
- クライアント既定URLをステージングに設定済み(`ServerConfig.DefaultBaseUrl`)
- テスト環境につき実ユーザーデータは載せない。ドメイン変更の可能性あり(本番確定時に再連絡)
- **K ガイドと設計書の差分(結合時に確認)**: ①submit タイムアウトをガイドは「30秒」、docs/08 は「120秒」と記載 ②WS 認証失敗をガイドは `auth_error` 型、docs/06 は `error` 型と記載 ③MissCount 定義(tap+holdヘッドのみ)は claim 生成時に合わせるか差分許容かすり合わせ

## 7. K への確認事項(連絡推奨)

1. ~~本番/ステージング URL~~ → **解決**: ステージング受領(§6.5)。残: 本番ドメイン確定時期
2. 楽曲登録の運用: 本番コンテンツの songs/charts 登録は誰がどう行うか(seeds 直書き? 管理API予定?)。**音源・ジャケット配信はAPI対象外**=クライアント同梱で良いか
3. Phase 7(ランキング/履歴/統計 API)の実装予定時期(ロビー/History/楽曲別ランキング画面の結線待ち)
4. prematch の `top5_charts` は現状 `[]` — 将来仕様か
5. 開始同期: WS は `start`(ready完了通知)のみで `start_at` 未来時刻方式は未採用と理解 — play_songN 突入後の各クライアント任意タイミング開始で正か(各自ローカル判定なので問題ないはずだが確認)
6. C# サーバー(`Server/`)の扱い: クライアント側は接続コード削除予定。リポジトリからの archive 退避時期は再協議

## 8. リスク・注意

- **WS-F のハッシュ整合が最大の罠**: クライアント改変譜面では submit が CHART_HASH_MISMATCH。リプレイ記録時の chartHash 源泉をサーバー値へ差し替える改修は ReplayEncoder/Validator/ReplayStorage に波及
- 現クライアントの ScoringEventCounter/PlayProgressSnapshot から `claim` の各フィールド(fast/late含む)が取れるか確認(FAST/SLOW は表示実装済なのでカウントあるはず)
- WebSocket は Play中ブリッジ問題(MCP)と無関係だが、**Editor 停止時の切断→グレース→forfeit** に注意(テスト時)
- 同一PC 2クライアント e2e: bob 側は curl 不可になる(JWT+WS+リプレイ必須)→ **検証用ヘッドレスbot(C#スクリプト or Goテストクライアント)が必要**。K のリポにテストクライアントがあるか確認
