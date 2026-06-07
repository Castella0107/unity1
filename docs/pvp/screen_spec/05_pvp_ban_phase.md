# 画面: PVPBanPhase（BAN → MATCH LINEUP）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPBanPhase |
| 画面ID | SCR-PVP-BAN |
| 機能概要 | 抽選3候補から各自1曲をブラインド BAN。確定3曲（PickA/PickB/残存曲）を発表し本戦開始 |
| 対応シーン名 | PVPBanPhase.unity（`UI/Pvp/PvpDraftScreenController.cs` Phase=BanPhase） |

## 画面レイアウト
- 上部: ①ヘッダー "BAN PHASE" →（開示後）"MATCH LINEUP" ②YOU/OPP 名 ③タイマー
- 中央: ④BAN候補タイル ×3（3列）→（開示後は隠す） ⑤ラインナップカード ×3（開示後のみ）
- 下部: ⑥指示文 ⑦ステータス ⑧ロック状況 ⑨開示行 ⑩補足行 ⑪LOCK IN / START MATCH ボタン ⑫操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | ヘッダー | ラベル | "BAN PHASE" → 開示後 "MATCH LINEUP" | ○ |  |
| ② | YOU/OPP 名 | ラベル | 各 userId | ○ |  |
| ③ | タイマー | ラベル | 60秒カウントダウン（残10秒以下で赤、0で自動ロック） | ○ | PICK と同仕様 |
| ④ | BAN候補タイル ×3 | ボタン | 両 PICK 完了後にサーバーが残プールから抽選した3曲 | ○ |  |
| ⑤ | ラインナップカード ×3 | カード | 確定3曲 = [PickA, PickB, 残存曲]。出自タグ YOU PICK（シアン）/ OPP PICK（レッド）/ PICKED | ○ | 開示後のみ表示 |
| ⑥ | 指示文 | ラベル | "Ban 1 of the 3 candidates. Lowest survivor becomes song 3." | ○ | 英語文言 |
| ⑦ | ステータス | ラベル | "BAN 1 OF 3" → "LOCKING IN..." → "WAITING..." → "DRAFT COMPLETE" | ○ |  |
| ⑧ | ロック状況 | ラベル | YOU ●/○ OPP ●/○ | ○ |  |
| ⑨ | 開示行 | ラベル | "YOU banned X / OPP banned Y" | ○ |  |
| ⑩ | 補足行 | ラベル | "FIRST TO 8 POINTS WINS"（開示後） | ○ | 固定文言 |
| ⑪ | primary ボタン | ボタン | "LOCK IN" → 開示後 "START MATCH" | ○ |  |
| ⑫ | 操作説明バー | ラベル | PICK と同一 | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | 矢印 / D-pad / クリック | 3候補のカーソル移動・仮選択 | （同一画面） |
| E2 | Space / Enter / LOCK IN / パッド A | BAN 送信 | （同一画面 → Waiting） |
| E3 | 両者 BAN 完了（自動検出） | 候補タイルを隠し MATCH LINEUP（確定3曲カード）発表 | （同一画面 → Reveal） |
| E4 | Space / Enter / START MATCH（Reveal 中） | `SetDraftSongs` → `BeginSongs`（1曲目セットアップへ） | PVPSongSetup |
| E5 | ESC / パッド B | 辞退確認 → 確定で Title | Title |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| LOCK IN | 未選択時 | 非活性 |
| BAN候補タイル | Submitting 以降 | 非活性（開示後は非表示） |
| 全入力 | ConfirmDialog 表示中 | 無効 |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| 相手の PICK が未完了で入場 | draft.phase == "pick" | "WAITING FOR OPPONENT'S PICK..." で poll 待機 |
| BAN 二重送信 / 再入 | "already..." エラー | 状態を GET し直して復帰（PICK と同様） |
| ドラフト不成立で START | songs 空 | "Draft incomplete — cannot start." 表示 |
| BAN かぶり | サーバー側で残2曲から random | クライアントは結果を表示するだけ |

## 画面内状態遷移
PICK と同じ状態機械（Loading → Selecting → Submitting → Waiting(1.2s poll) → Reveal）。

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | PVPSongPick | 開示後 Space |
| OUT | PVPSongSetup | START MATCH（`BeginSongs`） |
| OUT | Title | ESC → 辞退確定 |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| 選択制限時間 | 60秒 | 未選択ならランダム自動ロック |
| 相手待ち | **上限なし** | — ※要相談（PICK と同様） |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| 画面表示時 | GET /api/pvp/match/{id}/draft | 状態復元 |
| LOCK IN 時 | POST /api/pvp/match/{id}/draft/ban | BAN 送信 |
| Waiting 中 1.2秒ごと | GET /api/pvp/match/{id}/draft | 相手ロック検出 |

## 補足 / 未確定事項
- 3曲目の難易度はドラフトでは確定せず、各曲の SongSetup で各自選択する（[06_pvp_song_setup.md](06_pvp_song_setup.md) 参照）。
