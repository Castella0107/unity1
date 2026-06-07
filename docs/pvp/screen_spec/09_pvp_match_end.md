# 画面: PVPMatchEnd（対戦結果）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |
| 0.2 | 2026-06-07 |  | IN 遷移の契機を「3曲目完了」「2曲目後8ptクリンチ」の2系統で明記 |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPMatchEnd |
| 画面ID | SCR-PVP-END |
| 機能概要 | 試合の最終結果（VICTORY/DEFEAT/DRAW）、曲別内訳、レート変動を表示。中断時は分類済み文言を表示 |
| 対応シーン名 | PVPMatchEnd.unity（`UI/Pvp/PvpMatchEndController.cs`、`PvpMatchEndParameters` を受領） |

## 画面レイアウト
- 上部: ①結果ヘッダー
- 中央: ②合計スコア行 ③曲別内訳（3行） ④レート変動（自分/相手）
- 下部: ⑤TO TITLE ボタン ⑥操作説明バー（REMATCH / TO LOBBY はキー操作）

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | 結果ヘッダー | ラベル | "VICTORY / DEFEAT / DRAW  vs {相手ID}"。中断時 "MATCH INCOMPLETE / CONNECTION ERROR / MATCH ABORTED" | ○ | 中断分類はメッセージ文字列から判定 |
| ② | 合計スコア | ラベル | "You x.x - y.y Opponent"（最大15pt、8pt先取） | ○ |  |
| ③ | 曲別内訳 | ラベル | "n. songId [DIFF x倍率]  a.aa - b.bb" ×3 | ○ | Domain.MatchScoring で再構成（サーバー集計と一致） |
| ④ | レート変動 | ラベル | "Your rating: a → b (+d)" / "Opponent: ..." | ○ | 実データ（サーバー finalize 結果） |
| ⑤ | TO TITLE ボタン | ボタン | — | ○ | baked UI で結線済みのボタンはこれのみ。REMATCH / TO LOBBY は**キー/パッド操作のみ**（OnGUI フォールバックには3ボタンあり） |
| ⑥ | 操作説明バー | ラベル | "Space: REMATCH / Enter: TO LOBBY / ESC: TO TITLE" | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | Space / パッド A | リマッチ（再びキューへ。試合状態は ResetState 済） | Matchmaking |
| E2 | Enter / パッド Y | ロビーへ | PVPLobby |
| E3 | ESC / TO TITLE ボタン / パッド B | タイトルへ | Title |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| ③曲別内訳・④レート | 中断時 | 非表示（detail に生メッセージを小さく表示） |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| パラメータなしで表示 | _params null | "PVP Result (no data)" 表示（操作は可能） |
| 多重遷移 | SceneRouter `_isTransitioning` | 握り潰し |

## 画面内状態遷移
単一状態。

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | PVPSongResult（3曲目） | **3曲目完了**（通常終了）→ FINAL RESULT |
| IN | PVPSongResult（2曲目） | **2曲目後にどちらかが 8pt 到達**（早期決着クリンチ、3曲目スキップ）→ FINAL RESULT |
| IN | GamePlay (PVP) | リタイア / 提出失敗 / 相手タイムアウト（中断表示） |
| OUT | Matchmaking | Space（REMATCH） |
| OUT | PVPLobby | Enter（TO LOBBY） |
| OUT | Title | ESC（TO TITLE） |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| なし |  |  |

## サーバー通信
なし（結果は遷移パラメータで受領済み）。
※遷移前に PvpFlowController が直近10戦の記録＋自分の3曲リプレイをローカル履歴へ保存（古い試合は自動削除）。

## 補足 / 未確定事項
- **不戦勝/不戦敗（walkover）・再接続自動復帰は未対応**。サーバーの forfeit / 切断判定が必要（K 領域）。現状の相手タイムアウトは「勝敗なしの中断（MATCH INCOMPLETE）」。
- SAVE REPLAY ボタンは当面なし（リプレイはローカル自動保存）。
- REMATCH は「同じ相手との再戦」ではなく**再キューイング**（仕様通りか要確認）。
