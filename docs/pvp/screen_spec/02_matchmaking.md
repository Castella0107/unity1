# 画面: Matchmaking（マッチング検索）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | Matchmaking |
| 画面ID | SCR-PVP-MM |
| 機能概要 | キューに参加し対戦相手を検索。成立で自動的に Prematch へ |
| 対応シーン名 | Matchmaking.unity（`UI/Pvp/MatchmakingController.cs`） |

## 画面レイアウト
- 中央: ①ステータス ②自分の名前 ③相手の名前 ④経過タイマー ⑤曲リスト行
- 下部: ⑥CANCEL ボタン ⑦操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | ステータス | ラベル | "SEARCHING FOR OPPONENT..."（ドット 0〜3 個を約2回/秒でアニメ）→ "MATCH FOUND" | ○ |  |
| ② | 自分の名前 | ラベル | LocalIdentity.UserId | ○ |  |
| ③ | 相手の名前 | ラベル | "???" → 成立時に相手ID | ○ |  |
| ④ | 経過タイマー | ラベル | "00:00"（mm:ss、unscaled） | ○ |  |
| ⑤ | 曲リスト行 | ラベル | ""（成立時 "♪ songId..."） | - | **queue 経由は曲なしマッチのため通常は空**（曲はドラフトで確定） |
| ⑥ | CANCEL ボタン | ボタン | — | ○ |  |
| ⑦ | 操作説明バー | ラベル | "ESC: マッチング検索をキャンセル" | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | ESC / CANCEL / パッド B | 確認ダイアログ「マッチング検索をやめますか？」表示 | （同一画面・モーダル） |
| E2 | ダイアログ「やめる」 | POST queue/leave → ロビーへ | PVPLobby |
| E3 | ダイアログ「つづける」 | ダイアログを閉じ検索継続 | （同一画面） |
| E4 | matched 検出（自動） | `PvpFlowController.StartMatch` 起動 | PVPPrematch |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| ESC / B | ConfirmDialog 表示中 | 無効（ダイアログ側が入力を取る） |
| タイマー/ドットアニメ | 検索待機中のみ | 動作（成立・キャンセル後は停止） |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| キャンセル後の matched 応答 | `_canceled` フラグ | 紛れ込んだ matched 応答は無視（Title 遷移と競合させない） |
| ポーリングの一時エラー | `!s.Ok` | UI を乱さず次周期で再試行 |
| キューから落ちた（status=idle） | 検出時 | 自動で再 join |
| 試合中の再入 | `PvpFlowController.IsActive` | 検索を開始しない（警告表示のみ） |

## 画面内状態遷移
| 状態 | 遷移元・契機 | 遷移先・契機 | 許可される操作 |
|---|---|---|---|
| Joining | 画面表示 | join 応答 | ESC |
| Searching | join 応答（waiting） | matched / キャンセル | ESC |
| Canceling | キャンセル確定 | Lobby 遷移 | なし |
| MatchFound | matched 検出 | 自動で Prematch | なし |

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | PVPLobby | Space / START |
| IN | PVPMatchEnd | Space（REMATCH） |
| OUT | PVPPrematch | MATCH FOUND（自動） |
| OUT | PVPLobby | キャンセル確定 |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| MATCH FOUND | 即時（自動） | PVPPrematch |
| 検索タイムアウト | **なし（無期限検索）** | — ※要相談 |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| 画面表示時 | POST /api/pvp/queue/join | キュー参加（即 matched の場合あり） |
| 1.5秒ごと | GET /api/pvp/queue/status?userId= | 成立判定ポーリング |
| キャンセル時 | POST /api/pvp/queue/leave | キュー離脱 |

## 補足 / 未確定事項
- 検索の上限時間（タイムアウト→自動キャンセル）が未実装。要相談。
- WebSocket 化は数ヶ月棚上げ中（ポーリング継続）。
