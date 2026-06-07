# 画面: PVPLobby（オンラインロビー）

## 改訂履歴
| 版 | 日付 | 担当 | 変更内容 |
|---|---|---|---|
| 0.1 | 2026-06-07 |  | 初版（実装コードからのリバース起こし） |
| 0.2 | 2026-06-07 |  | Config を本画面から開けるよう遷移追加（F2、ESC で本画面に復帰。**未実装**） |

## 画面概要
| 項目 | 内容 |
|---|---|
| 画面名 | PVPLobby |
| 画面ID | SCR-PVP-LOBBY |
| 機能概要 | オンライン対戦の入口。自分の戦績を表示し、START でマッチング検索へ進む |
| 対応シーン名 | PVPLobby.unity（`UI/Pvp/PvpLobbyController.cs`） |

## 画面レイアウト
- 左パネル: ①SEASON ②YOUR RANKING
- 中央: ③ティア大バッジ
- 右パネル: ④LADDER TIER ⑤TOTAL MATCH ⑥MATCH WIN ⑦WIN RATIO
- 下部: ⑧START ボタン ⑨BACK ボタン ⑩操作説明バー

## 画面項目定義
| No | 項目名 | 種類 | 内容 / 初期値 | 必須 | 備考（ダミー/未確定など） |
|---|---|---|---|---|---|
| ① | SEASON | ラベル | "SEASON --" | - | **プレースホルダー**（K のシーズン/ラダー API 待ち） |
| ② | YOUR RANKING | ラベル | "-.--% OF TOP" | - | **プレースホルダー**（K 領域） |
| ③ | ティア大バッジ | ラベル | "UNRANKED" | - | **プレースホルダー**（K のレーティング設計待ち） |
| ④ | LADDER TIER | ラベル | "UNRANKED" | - | **プレースホルダー**（K 領域） |
| ⑤ | TOTAL MATCH | ラベル | 0 → 実値 | ○ | 実データ（stats API） |
| ⑥ | MATCH WIN | ラベル | 0 → 実値 | ○ | 実データ（stats API） |
| ⑦ | WIN RATIO | ラベル | 0.00% → 実値 | ○ | 実データ（stats API） |
| ⑧ | START ボタン | ボタン | "START (RANKED MATCH)" | ○ |  |
| ⑨ | BACK ボタン | ボタン | — | ○ |  |
| ⑩ | 操作説明バー | ラベル | "Space: START (RANKED MATCH) / ESC: タイトルへ" | ○ | ShortcutHintOverlay |

## 操作 / イベント定義
| No | 操作 / イベント | アクション（結果） | 遷移先 |
|---|---|---|---|
| E1 | Space / START ボタン / パッド A | マッチング検索を開始 | Matchmaking |
| E2 | ESC / BACK ボタン / パッド B | タイトルへ戻る（**確認なし**。未マッチのため） | Title |
| E3 | 画面表示時 | 戦績取得（非同期） | （同一画面） |
| E4 | F2 | Config を開く（ESC で本画面に戻る）**※新規仕様・未実装** | Config |

## 表示制御 / 活性制御
| 対象項目/操作 | 条件 | 制御 |
|---|---|---|
| ⑤⑥⑦戦績 | API 取得前 | 0 / 0 / 0.00% を表示（取得後に上書き） |
| ⑤⑥⑦戦績 | API 失敗時 | 初期値のまま（エラー表示なし） |

## 入力チェック / ガード条件
| 対象 | チェック内容 | エラー時の挙動 |
|---|---|---|
| Space/ESC 連打 | SceneRouter の `_isTransitioning` ガード | 遷移中の多重 GoTo は握り潰し |

## 画面内状態遷移
単一状態。

## 画面遷移（入出）
| 方向 | 相手画面 | 契機 |
|---|---|---|
| IN | Title | ONLINE 選択 |
| IN | PVPMatchEnd | Enter（TO LOBBY） |
| IN | Matchmaking | 検索キャンセル確定 |
| IN | Config | ESC（呼び出し元復帰）**※新規仕様・未実装** |
| OUT | Matchmaking | Space / START |
| OUT | Title | ESC / BACK |
| OUT | Config | F2 **※新規仕様・未実装** |

## タイムアウト / 自動遷移
| 契機 | 時間 | 遷移先・動作 |
|---|---|---|
| なし |  |  |

## サーバー通信
| タイミング | API | 概要 |
|---|---|---|
| 画面表示時 1回 | GET /api/pvp/user/{id}/stats | totalMatches / wins / winRatio 取得 |

## 補足 / 未確定事項
- **Config 遷移（F2）は 2026-06-07 の仕様追加で未実装**。Config の ESC は呼び出し元（Title / SongSelect / PVPLobby）へ戻る方式に変更（`screen_flow.md` 参照）。
- ティア・LP・シーズン・ランキング・難易度別スタッツ表は **K のラダー/ティア API 完成後に結線**。
- UI 未結線時は OnGUI フォールバックで動作（全 PVP 画面共通の仕組み）。
