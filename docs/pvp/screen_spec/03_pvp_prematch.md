# 画面: PVPPrematch（対戦カード導入）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPPrematch |
| 画面ID | SCR-PVP-PRE |
| 機能概要 | マッチ成立直後の対戦カード演出。約2.5秒後に自動で SongPick へ |
| 対応シーン名 | PVPPrematch.unity（`UI/Pvp/PvpDraftScreenController.cs` Phase=Prematch） |

## 画面レイアウト
- 上部: ①ヘッダー "MATCH READY"
- 中央: ②自分の名前（シアン） ③相手の名前（レッド） ④試合形式 ⑤ステータス
- 下部: ⑥TO SONG PICK ボタン ⑦操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | ヘッダー | ラベル | "MATCH READY" | ○ |  |
| ② | YOU 名 | ラベル | 自分の userId | ○ |  |
| ③ | OPP 名 | ラベル | 相手の userId（不明時 "???"） | ○ |  |
| ④ | 試合形式 | ラベル | "BEST OF 3  3 SONGS x 5 SECTORS" | ○ | 固定文言 |
| ⑤ | ステータス | ラベル | "Ready when you are." | - |  |
| ⑥ | primary ボタン | ボタン | "TO SONG PICK" | ○ |  |
| ⑦ | 操作説明バー | ラベル | "まもなく PICK へ… / Space: すぐ進む / ESC: 辞退" | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | 自動（表示から約2.5秒） | PICK へ自動進行（READY 操作不要） | PVPSongPick |
| E2 | Space / Enter / ボタン / パッド A | 自動進行を待たず即スキップ | PVPSongPick |
| E3 | ESC / パッド B | 辞退確認「対戦を辞退しますか？（不戦敗扱い）」 | （モーダル） |
| E4 | 辞退確定 | `CancelMatch`（状態破棄） | Title |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| 全入力 | ConfirmDialog 表示中 | 無効 |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| マッチ未存在で表示 | `PvpFlowController.IsActive` false | "(no active match)" 表示、BACK で Title |

## 画面内状態遷移
| 状態 | 遷移元・契機 | 遷移先・契機 | 許可される操作 |
|---|---|---|---|
| Intro | 表示 | 2.5秒経過 or Space | Space, ESC |
| NoMatch | マッチなしで表示 | BACK | BACK |

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | Matchmaking | MATCH FOUND（`PvpFlowController.StartMatch`） |
| OUT | PVPSongPick | 自動（2.5秒）/ Space |
| OUT | Title | ESC → 辞退確定 |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| 導入演出終了 | 約2.5秒 | PVPSongPick へ自動進行 |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| 画面表示時 1回 | GET /api/pvp/match/{id} | A/B どちらが自分かを解決（ResolveSidesAsync） |

## 補足 / 未確定事項
- **辞退はクライアント側で Title に戻るだけ。サーバーへの forfeit 通知 API は未実装（K 領域）**。相手側は相手の提出待ちタイムアウトで「MATCH INCOMPLETE（勝敗なし中断）」になる。不戦勝/不戦敗の確定はサーバー対応待ち。
