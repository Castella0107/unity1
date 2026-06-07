# PVP 画面仕様書（画面別・現状実装ベース）

> **2026-06-07 時点の実装コード**から起こした画面仕様書。K・メンバーとの画面遷移すり合わせ用。
> 1画面 = 1ファイル。遷移全体図は [`../screen_flow.md`](../screen_flow.md) / `../screen_flow.png` を正とする。

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし、画面別ファイルに分割） |

## 画面一覧（ファイル索引）

| # | 画面 | 画面ID | シーン | ファイル | 主な実装ポイント |
|---|---|---|---|---|---|
| 1 | PVPLobby | SCR-PVP-LOBBY | PVPLobby.unity | [01_pvp_lobby.md](01_pvp_lobby.md) | 戦績=実データ(stats API) / ティア・シーズンは**プレースホルダー**(K領域) |
| 2 | Matchmaking | SCR-PVP-MM | Matchmaking.unity | [02_matchmaking.md](02_matchmaking.md) | join→1.5秒poll、キャンセル後のmatched応答破棄ガード |
| 3 | PVPPrematch | SCR-PVP-PRE | PVPPrematch.unity | [03_pvp_prematch.md](03_pvp_prematch.md) | **2.5秒で自動進行**、Spaceスキップ可 |
| 4 | PVPSongPick | SCR-PVP-PICK | PVPSongPick.unity | [04_pvp_song_pick.md](04_pvp_song_pick.md) | 20曲5列・60秒タイマー(0でランダム自動ロック)・1.2秒poll・再入安全 |
| 5 | PVPBanPhase | SCR-PVP-BAN | PVPBanPhase.unity | [05_pvp_ban_phase.md](05_pvp_ban_phase.md) | 3候補→MATCH LINEUP発表、BANかぶりはサーバーがrandom |
| 6 | PVPSongSetup | SCR-PVP-SETUP | PVPSongSetup.unity | [06_pvp_song_setup.md](06_pvp_song_setup.md) | ←→難易度(倍率0.75〜1.00)、Shift/Ctrl/Alt+←→で設定、相手難易度は**ダミー"—"** |
| 7 | GamePlay (PVP) | SCR-PVP-PLAY | GamePlay.unity(共用) | [07_gameplay_pvp.md](07_gameplay_pvp.md) | ESC長押し6秒リタイア・進捗0.5秒POST・**相手提出待ち最大180秒** |
| 8 | PVPSongResult | SCR-PVP-SONGRES | PVPResult.unity | [08_pvp_song_result.md](08_pvp_song_result.md) | S1..S5勝敗(タイブレーク込)・8ptクリンチ・**ESC無効** |
| 9 | PVPMatchEnd | SCR-PVP-END | PVPMatchEnd.unity | [09_pvp_match_end.md](09_pvp_match_end.md) | 勝敗/曲別内訳/レート変動、中断3分類表示 |

## 全体フロー（正常系）

```
Title ─ONLINE→ PVPLobby ─Space→ Matchmaking ─MATCH FOUND(自動)→ PVPPrematch
  ─自動2.5s→ PVPSongPick ─両者PICK→ PVPBanPhase ─START MATCH→
  ┌─[各曲ループ ×3]──────────────────────────────┐
  │ PVPSongSetup ─READY→ GamePlay(PVP) ─完走→ PVPSongResult │
  └─NEXT SONG（2曲目クリンチ時は3曲目スキップ）──────────┘
  ─FINAL RESULT→ PVPMatchEnd ─Space→ Matchmaking / ─Enter→ PVPLobby / ─ESC→ Title
```

- 離脱系: マッチ成立後（Prematch 以降）の ESC は辞退確認 → Title（不戦敗扱い、ただしサーバー forfeit 未実装 → 下記サマリ #1）
- 中断系: リタイア / 提出失敗 / 相手タイムアウト → PVPMatchEnd（中断表示）

## 共通事項（全画面横断ルール）

| 項目 | 内容 |
|---|---|
| 配色規約 | **自分 = シアン `#2BD9E6`（左） / 相手 = レッド `#F24D6B`（右）**、基調は DJMAX 風 |
| 決定 / 前進 | **Space**（一部 Enter 併用）。例外: PVPMatchEnd（Space=REMATCH / Enter=TO LOBBY） |
| ゲームパッド | D-pad/左スティック=選択、**A=決定（Space相当）**、**B=戻る（ESC相当）**。Xbox⇄任天堂配置は Config で切替（`Input/GamepadLayout.cs`） |
| 確認ダイアログ | `UI/Common/ConfirmDialog.cs`。表示中は裏画面の入力を抑止（`IsOpen` は閉じたフレームも true） |
| 操作説明バー | `UI/Common/ShortcutHintOverlay.cs`。各画面下部に常時表示、シーン遷移で自動クリア |
| 遷移ガード | `SceneRouter` が additive load + `_isTransitioning` ガードで直列化（多重 GoTo は握り潰し） |
| バックグラウンド | 全 PVP 画面で `Application.runInBackground = true`（非フォーカス時もポーリング継続） |
| UI フォールバック | baked UI 未結線時は各コントローラーの OnGUI フォールバックで操作可能 |
| 試合状態の保持 | `PvpFlowController`（常駐シングルトン）がシーンを跨いでマッチ状態を保持。ドラフト状態の真実は**サーバー**（GET /draft で再入復元） |

## 横断的な未確定事項（K・チーム相談用サマリ）

| # | 項目 | 現状 | 相談相手 |
|---|---|---|---|
| 1 | 辞退/離脱の forfeit 通知 | クライアントは Title に戻るだけ。サーバーに辞退 API なし → 相手は180秒待ちの末「中断」 | K（サーバー） |
| 2 | 不戦勝/不戦敗・切断判定・再接続復帰 | 未対応。中断3分類の表示のみ | K（サーバー） |
| 3 | プレイ開始同期（READY 20秒待ち→不戦敗） | 仕様確定済みだが未実装（現状は非同期進行＋提出時同期） | K（サーバー）+ クライアント |
| 4 | 相手スコアのライブ HUD（VSバー右/リード/セクタータグ） | progress API は実装済み・取得済み。HUD 結線が未 | クライアント |
| 5 | ドラフトの相手待ち上限 | 無期限 poll（サーバー側タイムアウトなし） | K（サーバー） |
| 6 | マッチング検索のタイムアウト | 無期限検索 | チーム判断 |
| 7 | ティア/LP/シーズン/ランキング表示 | 全てプレースホルダー | K（レーティング設計） |
| 8 | 相手の難易度表示（SongSetup） | ダミー "OPP —"（公開 API なし） | K（サーバー） |
| 9 | 画面内文言の日本語化 | ドラフト系・リザルト系は英語のまま（DJMAX風ミックス方針は確定済み） | クライアント |
| 10 | REMATCH の意味 | 再キューイング（同一相手指名ではない） | チーム判断 |
