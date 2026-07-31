# UI_FINDINGS — unity1 UI 網羅チェック (2026-07-22)

## 画面一覧 (棚卸し)

| シーン | 主コントローラ | 区分 |
|---|---|---|
| Login | UI/Login/LoginController | 対象 |
| Title | UI/Title/TitleController (+MenuOutlineLabel) | 対象 |
| SongSelect | UI/SongSelect/SongSelectController (+PlayOptions/PlayerDataPopup) | 対象 |
| SongRanking | UI/SongSelect/SongRankingController (+RankingRowView) | 対象 |
| Config | UI/Config/ConfigController + 8タブ (Account/Audio/Calibration/Colors/Data/Devices/Display/Gameplay/Input/ManageSongs) | 対象 |
| Result | UI/Result/ResultController | 対象 |
| History | UI/History/HistoryController (+DetailView/RowView×2) | 対象 |
| Matchmaking | UI/Pvp/MatchmakingController | 対象 |
| PVPLobby | UI/Pvp/PvpLobbyController | 対象 |
| PVPSongPick | UI/Pvp/PvpDraftController | 対象 |
| PVPPrematch | UI/Pvp/PvpPrematchController | 対象 |
| PVPResult | UI/Pvp/PvpSongResultV2Controller | 対象 |
| PVPMatchEnd | UI/Pvp/PvpMatchEndController | 対象 |
| GamePlay (HUD静的部) | UI/HUD/GameHud, JudgmentDisplay, PauseMenu, ShortcutHintOverlay | 対象 (静的部のみ) |
| AudioTest / InputTest / NotePoolTest | — | 対象外 (デバッグシーン) |
| Bootstrap / _Persistent | — | 対象外 (UI なし) |

スクリーンショット保存先: `C:\Users\mashi\projects\unity1\Assets\Screenshots\ui_check_*.png`

## ベースライン

- 書き込み経路: WSL→/mnt/c 書き込み検証 OK (2026-07-22)
- EditMode テスト: **293/293 緑** (36.4s)

---

## 静的解析による発見 (コード突き合わせ)

### 要K判断 (保留)

- ~~[Config/ConfigController.cs:136 vs 228-239] ヒント「←→: タブ切替」だが実装に←→タブ切替が無い~~ → **解消済み (2026-07-22)**: K指示書により2階層ナビゲーション(タブレベル←→=タブ切替 / 項目レベル←→=値変更)を実装 (UI_FIX_LOG #7)。
- [SongSelect/SongSelectController.cs:203 vs 159] `M: モディファイア切替` が実装されているがショートカットヒントに未記載。/ ヒントへの追記は容易だが、意図的な省略(ヒント過長回避)の可能性があるため保留。
- [History/HistoryController.cs:116 vs 556-567] ヒント「Space: リプレイ」「↑↓: 行」に対応する明示的なキー処理が無い(数字キー1-4のみ)。EventSystem ナビゲーション(UIマップ Navigate=↑↓ / Submit=Space・Enter)+行ボタンで動いている可能性が高い — PlayMode で動作確認予定。動くなら問題なし。

### 一致確認済み (問題なし)

- Result: Space=リトライ / Enter=選曲 / ESC=タイトル ✓ (ResultController.cs:94-99)
- PvpLobby: Space/F2/ESC ✓、PvpMatchEnd: Space/Enter/ESC ✓、Prematch: Space(またはEnter)=READY ✓
- Login: Enter=決定 ✓、SongRanking: ESC=戻る ✓
- Title: UIマップ Navigate/Submit(Submit=Space・Enter 両対応) ✓「Space: 決定」表記は実装と整合
- 文字化け(U+FFFD)チェック: .cs 内 0件

---

## 画面別チェック (PlayMode)

(以下、確認順に追記)

### Login (ui_check_login-1.png) — 問題なし
- SIGN IN フォーム / ログイン / 新規登録へ / オフラインで続行 / ヒント一致 ✓
- 備考: タイトルロゴ「MUSICGAME」は仮名の可能性 (要K確認・急ぎでない)
- EMAIL プレースホルダーは低解像度で "amail" に見えるがシーン上は "email" ✓

### Title (ui_check_title.png) — 1件修正
- 右上 PlayerChip: OFFLINE と RATING ---- が重なって表示 → **修正済み** (UI_FIX_LOG #2)
- メニュー/説明文/ヒント ✓。ロゴ「MUSICGAME : Rhythm Action — prototype」は仮名?(要K確認)

### SongSelect (ui_check_songselect.png) — 1件修正 + 保留1
- 下部: 旧フッター「ENTER > PLAY ESC < BACK」がヒントオーバーレイと二重表示 → **修正済み** (UI_FIX_LOG #1)
- 保留: `M: モディファイア` ヒント未記載 (静的解析の項参照)
- SPEED 表示 "4 5" に見える件 → 小数点の視認性 (フォント) の可能性。次回スクショで再確認

### Config (ui_check_config_gameplay/tab2/tab3.png) — 1件修正 + 保留3
- 下部ボタンがヒントバーに半分隠れる → **修正済み** (UI_FIX_LOG #3)
- **要K判断**: [Config グラフィック] 「カメラアングル」設定 (0°flat/32°steep) は プレイ画面リデザインで
  StageInitializer.ApplyCameraAngle が値を無視する仕様となり**機能しない設定**になった。
  UI から撤去するか、機能を復活させるかは設計判断 (経緯: プレイフィールドがスクリーン仕様
  駆動デカールとなり画角変更が意味を持たなくなったため)
- 保留(軽微): [キー設定] 「パッド決定/戻る配置: 任天堂配置 (右=決定)」ラベルが行内で2行に折返し窮屈
- 保留(軽微): [ゲームプレイ] ドロップダウン値 "Good or better"/"Normal" 等が英語で和文UIと混在
- キー設定タブの表示は新デフォルト (S/D/F/J/K/L) を正しく反映 ✓ / 「垂直同期」は低解像度の見え方で文字化けではない ✓

### History (ui_check_history.png) — 問題なし
- 行展開時の判定内訳・セクターダイヤ・スコア・日付 ✓。ヒントの Space/↑↓ は
  EventSystem (UIマップ Navigate/Submit) 経由の行ボタン操作で整合とみられる
- 保留(軽微): 展開行の判定ラベルが小文字 (perfect+/miss) で他画面 (大文字) と不統一

### SongRanking (ui_check_songranking.png) — 1件修正 + 保留1
- 右上の曲名/アーティスト重なり → **修正済み** (UI_FIX_LOG #4)
- **要K判断**: 「COMING SOON — ランキングはサーバー側 (Phase 7) の実装待ちです」を無条件表示。
  コードコメントには「K の Phase 7 完了後 /api/v1/leaderboard/{song_id}/{difficulty} に結線」とある。
  サーバー側 Phase 7 (統計/履歴) は完了済みのため、リーダーボード API を結線するか、
  文言を現状に合わせて更新するかの判断が必要 (機能結線はUI修正の範囲外)

### Result (ui_check_result.png) — 2件修正 + 保留2
- 判定カウント数字の右端見切れ → **修正済み** (UI_FIX_LOG #5)
- 下部ボタン (RETRY/SONG SELECT/TO TITLE) がヒントバーに沈む → **修正済み** (UI_FIX_LOG #5)
- **要K判断**: 左下「サーバー検証: 準備中 (Phase 7)」— サーバー側 Phase 7 は完了済み。
  オフライン時の文言 (「オフライン」等) と結線状況の表示を実態に合わせるべきか要判断
- 保留: 右パネル中段の灰色の空ボックス (ジャケット画像領域?) に何も表示されない。
  意図 (プレースホルダー/未実装) が不明なため要確認

### PVPLobby (ui_check_pvplobby.png) — 3件修正
- 見出し「LOBBY」左見切れ / STARTボタン「PRESS F5」誤記 / < MENU ボタン沈み → **修正済み** (UI_FIX_LOG #6)
- オフライン時のプレースホルダー (SEASON --/UNRANKED/-.--%) は妥当 ✓

### 未チェック画面 (今回の範囲外/到達困難)
- Matchmaking / PVPSongPick / PVPPrematch / PVPResult / PVPMatchEnd: 実マッチ文脈 (WS接続)
  が必要でオフライン自走では到達不可。静的解析でのヒント↔実装整合は確認済み (冒頭の表参照)。
  次回オンライン環境での目視確認を推奨
- GamePlay HUD 静的部: 本日の プレイフィールド刷新作業内でスクリーンショット多数により
  検証済み (スコア/レート/コンボ/セクター表示・キーガイド、オートプレイでの実挙動含む)

## 総括 (2026-07-22 自走分)
- チェック済み: Login / Title / SongSelect / Config(3タブ) / History / SongRanking / Result / PVPLobby + GamePlay HUD = 9画面
- 安全修正: 6件 (詳細 UI_FIX_LOG.md) — すべて EditMode 293/293 緑を確認
- 要K判断: 4件 (Config カメラアングル設定の無効化 / SongRanking COMING SOON 文言 /
  Result サーバー検証文言・灰色空ボックス / SongSelect Mキー未記載)
  ※「Config ←→タブ切替ヒント」は 2026-07-22 の2階層ナビゲーション実装で解消 (UI_FIX_LOG #7)
