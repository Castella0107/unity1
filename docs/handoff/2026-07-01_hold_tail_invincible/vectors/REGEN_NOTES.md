# real_castella golden vector 再生成（新仕様: ホールド終端 無敵ゾーン） — 2026-07-04

K の依頼（`castella_golden_vector_regen_request.md`）に対する回答。対象 3 本を新仕様
（`2026-07-01_hold_tail_invincible`）のクライアント Domain で再生成しました。

## 成果物

`regenerated/` に 3 本（K の `testdata/replay_vectors/` 直下へ上書き差し替え想定）:

| case | N | 旧 score（2026-06-13 パリティ基準）| 新 score | pp | p | great | good | miss | maxCombo |
|---|---|---|---|---|---|---|---|---|---|
| real_castella_song1_001_extra | 17 | 557941 | **499117** | 3 | 1 | 3 | 2 | 8 | 3 |
| real_castella_song2_003_extra | 15 | 999000 | **999000** | 9 | 3 | 3 | 0 | 0 | 15 |
| real_castella_song3_003_extra | 15 | 915333 | **982000** | 7 | 3 | 4 | 1 | 0 | 15 |

- song1: 終端ゾーン手前でコンボが切れた状態で終端離上 → 尾が直前実ティックの **Miss を複製**（救済せず）。
  旧仕様より pp が 1 減り miss が 1 増える（K 実測の pp 4→3 / miss 7→8 と一致）。
- song2: 元々クリーン（miss=0）。新仕様でも満点近く維持、score は 2026-06-13 と同値（999000）。
- song3: 終端で押下継続 → 尾が直前実ティック（P+）を複製し **救済**。旧 miss=1 が解消、maxCombo 9→15。

## 生成方法（2026-06-13 と同一プロセス）

- 生成器 = `Tools/ParityVectors/`（`RhythmGame.Domain.csproj` 経由でクライアントと同一 Domain を実行）。
  `chart_data` + `input_events` を現行 Domain に流し `expected` を再計算。
- `chart_data` / `input_events` / `replay_base64` は **不変**（差し替えは `expected` のみ。`total_notes` / `x_micro` も不変）。
  ※ `replay_base64` 内の埋め込み result は旧値のままだが、2026-06-13 でも同様で K 側再判定は `expected` と照合するため問題なし。
- 全 3 本で **Σ sector_scores == score** を検証済み。score はスナップショットと式の両計算が一致（不一致なら生成器が例外）。
- 判定ウィンドウは標準プロファイル（`WideActive=false`）。WIDE 判定（2026-07-03 追加）は生成に非関与。

## パリティ確認

- song1 の新値（pp=3 / miss=8）は K の engine 実測と **一致**。→ 3 本とも新仕様で client=server 一致見込み。
- 新仕様で値が変わったのは **この 3 本のみ**。他のホールド系ベクター
  （hold_guard_*, hold_recover_*, bpm_change_hold, timesig_*_hold, density_4_4_baseline_hold）は
  現行 Domain で再生成しても全て不変＝影響なし。

## 注意（K のローカル testdata の齟齬）

手元の `pvpharmonics-server/testdata/replay_vectors/` の 3 本の `expected` は、2026-06-13 基準
（song1=557941/pp4）でも新値（499117/pp3）でもない第三の値（song1=367352/pp2）でした。
`chart_data`/`input_events`/`replay_base64` は基準と byte 一致なので、生成源は健全です。
この齟齬は生成には無影響ですが、K 側 testdata がどの状態か一度確認推奨（本再生成分で上書きすれば解消）。
