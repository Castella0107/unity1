# 画面: PVPSongPick（PICK / 20曲ブラインド）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPSongPick |
| 画面ID | SCR-PVP-PICK |
| 機能概要 | シーズンプール20曲から各自1曲をブラインド PICK。両者ロックで開示し BAN へ |
| 対応シーン名 | PVPSongPick.unity（`UI/Pvp/PvpDraftScreenController.cs` Phase=SongPick） |

## 画面レイアウト
- 上部: ①ヘッダー "SONG PICK" ②YOU/OPP 名 ③制限時間タイマー
- 中央: ④曲タイル ×20（5列×4段、ジャケット+曲名）
- 下部: ⑤指示文 ⑥ステータス ⑦ロック状況 ⑧開示行 ⑨BAN候補行 ⑩LOCK IN ボタン ⑪操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | ヘッダー | ラベル | "SONG PICK" | ○ |  |
| ② | YOU/OPP 名 | ラベル | 各 userId | ○ |  |
| ③ | タイマー | ラベル | 60秒カウントダウン（m:ss、残10秒以下で赤） | ○ | 0 で未選択ならランダム自動ロック |
| ④ | 曲タイル ×20 | ボタン（DraftTileView） | サーバーの pool 20曲。曲名は ChartLoader、ジャケットは JacketLoader で非同期解決 | ○ | 現状プールは**20曲サンプル**（本番コンテンツ未投入） |
| ⑤ | 指示文 | ラベル | "Pick 1 song from the season pool (20)." | ○ | 英語文言（日本語化は未） |
| ⑥ | ステータス | ラベル | "PICK 1 SONG" → "LOCKING IN..." → "WAITING FOR OPPONENT..." → "PICKS REVEALED" | ○ |  |
| ⑦ | ロック状況 | ラベル | "YOU ○ . . . / OPP ○ . . ."（確定で ● LOCKED、シアン/レッド） | ○ |  |
| ⑧ | 開示行 | ラベル | "YOU picked X / OPP picked Y"（開示後） | ○ |  |
| ⑨ | BAN候補行 | ラベル | "BAN CANDIDATES: ..."（開示後、3曲） | ○ |  |
| ⑩ | primary ボタン | ボタン | "LOCK IN" →（開示後）"TO BAN PHASE" | ○ | 未選択時は非活性 |
| ⑪ | 操作説明バー | ラベル | "矢印: 選択 / Space/Enter: ロック / 次へ / ESC: 辞退（不戦敗）" | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | 矢印キー / D-pad | 5列グリッドのカーソル移動＋移動先を仮選択 | （同一画面） |
| E2 | タイルクリック | 該当曲を仮選択（枠ハイライト）、LOCK IN 活性化 | （同一画面） |
| E3 | Space / Enter / LOCK IN / パッド A（Selecting 中） | 選択曲を確定送信（未選択ならカーソル位置を選択してからロック） | （同一画面 → Waiting） |
| E4 | 両者ロック完了（自動検出） | ブラインド開示（YOU=シアン枠 / OPP=レッド枠 / 候補3曲=CANDIDATE タグ / 他=暗転） | （同一画面 → Reveal） |
| E5 | Space / Enter / TO BAN PHASE（Reveal 中） | BAN フェーズへ | PVPBanPhase |
| E6 | ESC / パッド B | 辞退確認 → 確定で `CancelMatch` | Title |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| LOCK IN | 曲未選択時 | 非活性 |
| 曲タイル | Submitting / Waiting / Reveal 中 | 非活性 |
| タイマー | Selecting 中のみ | 表示・カウント（ロック後・開示後は非表示） |
| 全入力 | ConfirmDialog 表示中 | 無効 |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| ロック二重送信 / シーン再入 | サーバーが "already..." エラーを返す | 状態を GET し直して Waiting / Reveal に復帰（再入安全） |
| ロック送信失敗（その他） | `!res.Ok` | エラー表示し Selecting に戻す |
| ポーリング一時エラー | fetch null | 無視して 1.2 秒後に再試行 |
| 制限時間切れ | 60秒経過・未選択 | ランダム1曲を自動選択してロック |
| マッチ未存在 | IsActive false | "(no active match)" → BACK で Title |

## 画面内状態遷移
| 状態 | 遷移元・契機 | 遷移先・契機 | 許可される操作 |
|---|---|---|---|
| Loading | 表示 | GET draft 完了 | ESC |
| Selecting | Loading | LOCK IN / タイマー0 | 矢印・クリック・Space/Enter・ESC |
| Submitting | LOCK IN | 応答受信 | ESC |
| Waiting | 送信成功（相手未ロック） | 両者完了検出（1.2秒poll） | ESC |
| Reveal | 両者完了 | Space/Enter | Space/Enter・ESC |
| NoMatch | マッチなし | BACK | BACK |

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | PVPPrematch | 自動進行 |
| OUT | PVPBanPhase | 開示後 Space / TO BAN PHASE |
| OUT | Title | ESC → 辞退確定 |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| 選択制限時間 | 60秒 | 未選択ならランダム自動ロック（画面遷移はしない） |
| 相手のロック待ち | **上限なし（無期限 poll）** | — ※要相談 |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| 画面表示時 | GET /api/pvp/match/{id}/draft | ドラフト状態復元（サーバーが真実） |
| LOCK IN 時 | POST /api/pvp/match/{id}/draft/pick | 自分の PICK 送信 |
| Waiting 中 1.2秒ごと | GET /api/pvp/match/{id}/draft | 相手ロック検出 |

## 補足 / 未確定事項
- 相手のロック待ちに上限がない（相手が放置すると無期限待ち）。**サーバー側のドラフトタイムアウト/forfeit 判定が必要（K 領域）**。
- 画面内テキストは英語。日本語化（DJMAX風ミックス）は未着手分の文言に含む。
