# 画面: PVPSongResult（曲リザルト）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPSongResult |
| 画面ID | SCR-PVP-SONGRES |
| 機能概要 | 各曲完走後にセクター単位（S1..S5）の勝敗・この曲の獲得 pt・累計 pt（8pt 先取）を表示 |
| 対応シーン名 | PVPResult.unity（`UI/Pvp/PvpSongResultController.cs`） |

## 画面レイアウト
- 上部: ①ヘッダー "SONG n / 3 RESULT" ②曲名 [難易度]
- 中央: ③YOU 獲得pt（シアン） ④OPP 獲得pt（レッド） ⑤セクター勝敗行 ⑥累計行 ⑦CLINCH 告知
- 下部: ⑧NEXT SONG / FINAL RESULT ボタン ⑨操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考 |
|---|---|---|---|---|---|
| ① | ヘッダー | ラベル | "SONG n / 3  RESULT" | ○ |  |
| ② | 曲名 | ラベル | "songId [diff]" | ○ |  |
| ③ | YOU 獲得pt | ラベル | "YOU +x.x"（難易度倍率込み） | ○ |  |
| ④ | OPP 獲得pt | ラベル | "OPP +y.y" | ○ |  |
| ⑤ | セクター勝敗行 | ラベル | "S1 WIN / S2 LOSE / S3 DRAW ..."（シアン/レッド/グレー） | ○ | ◆/◇ はフォント未収載のため ASCII 語+色 |
| ⑥ | 累計行 | ラベル | "MATCH TOTAL YOU x.x - y.y OPP (first to 8.0)" | ○ |  |
| ⑦ | CLINCH 告知 | ラベル | "CLINCH!  8 POINTS REACHED"（クリンチ時のみ） | - |  |
| ⑧ | primary ボタン | ボタン | "NEXT SONG" / matchOver 時 "FINAL RESULT" | ○ |  |
| ⑨ | 操作説明バー | ラベル | "Space / Enter: 次へ" | ○ |  |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | Space / Enter / ボタン / パッド A | `AfterSongResult`: 次曲セットアップ or 最終結果へ | PVPSongSetup / PVPMatchEnd |
| E2 | ESC | **無効**（試合進行中のため） | — |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| ⑧ボタンラベル | matchOver（クリンチ or 最終曲） | "FINAL RESULT" に切替 |
| ⑦CLINCH | sr.clinch | 表示（それ以外は空） |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| 結果データなしで表示 | LastSongResult null | "(no result)" + CONTINUE（Title へ） |
| 最終曲なのに matchOver でない異常系 | AfterSongResult 内 | GET match で最終結果を取り直し、それでも無ければ中断表示 |

## 画面内状態遷移
単一状態。

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | GamePlay (PVP) | 完走・両者提出成立 |
| OUT | PVPSongSetup | NEXT SONG（次曲あり） |
| OUT | PVPMatchEnd | FINAL RESULT（クリンチ含む）。**2曲目クリンチ時は3曲目をスキップ** |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| なし |  |  |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| （通常なし） | — | 表示は `LastSongResult`（提出時に取得済み）を使用 |
| 異常系のみ | GET /api/pvp/match/{id} | 最終結果の補完取得 |

## 補足 / 未確定事項
- セクター同点タイブレーク（Σ 2×P+ + P、難易度倍率込み）はサーバーの MatchScoring と同一ロジックで表示判定。レート影響があるため **K と同期済みであること**（Phase1 検証済み、DB 永続化は Phase2 保留）。
