# 2026-05-24 WebSocket + S3 移行協議 (棚上げ)

## 結論

WebSocket + S3 PUT への移行タスクは **数ヶ月棚上げ**。Go サーバー (PVPharmonics) の Phase 5-6 完成後に再着手する。

- 当面 Unity 通信は **HTTP polling + base64 JSON 継続**
- 接続先は現リポの `Server/` (ASP.NET Core) を維持
- `Server/` の archive 退避は不要

## 経緯

| # | 文書 | 発信 | 内容 |
|---|---|---|---|
| 01 | [01_original_task_from_K.md](01_original_task_from_K.md) | K → Castella | WebSocket + S3 PUT 化の実装指示書 (6 Step) |
| 02 | [02_questions_to_K.md](02_questions_to_K.md) | Castella → K | Step 1 として未確定要素を A〜H 8 セクションで質問 |
| 03 | [03_response_from_K.md](03_response_from_K.md) | K → Castella | 全項目棚上げ回答、現状維持指示 |

## 今後の参照タイミング

Go サーバー (PVPharmonics、`github.com/mashikanimashi-commits/pvpharmonics-server`) が Phase 5-6 完成し、WebSocket + S3 PUT 移行が再開された際に、本フォルダの 3 文書を出発点として再協議する。
- 質問 md (02) の B〜G は Go サーバー設計時の論点リストとして K も保管中
- 回答 md (03) の長期方針 (snake_case / `/api/v1` / `{data}/{error}` 形式 / 整数演算ベース + 許容誤差) は確定済み

## 関連 memory

`[[pvp_server_handoff]]` (auto-memory) に決定事項と PVPharmonics リポ情報を保管済み。
