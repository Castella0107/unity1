# パリティ期待値 — 2026-06-13 スコア定義変更（クライアント正本）

宛先: K（Go サーバー担当）/ 起票: 2026-06-13 / 種別: リプレイ再判定パリティ

2026-06-11 のスコア定義変更（①ホールド tick 密度 2→8 / ②ホールド復帰+復帰後 Great /
③変拍子 timesig）について、**クライアント本体 Domain で実際に判定を走らせた期待値**を出力しました。
これがサーバー再判定が一致すべき正本です。一致するまでランク戦投入は保留（`2026-06-11_for_K.md` の通り）。

> すべて 4/4・timesig 無しでも結果が変わります（後方互換ではない）。既存 golden 8 件が「fail」しているのは
> 旧期待値のままだからで、**新仕様で正しく変わった結果**です。下記で置き換えてください。

## 生成方法（再現可能）

`Tools/ParityVectors/`（net10 コンソール）が Unity と同一の Domain ソース
（`RhythmGame.Domain.csproj` が `Assets/_Project/Scripts/Domain/**` を直接リンク）を実行して出力します。

```
cd PVP/Tools/ParityVectors && dotnet run -c Release
```

- スコアは `ScoreCalculator`（x_micro = ⌈10^12 / N⌉、Great=199/200・Good=3/4 を各判定で個別 floor）。
- `total_notes`(N) は `ScoringEventCounter.Count`（tap=1 / hold=head+ticks+tail、tick は `measureMs/8`）でロード時再計算。
- 不変条件チェック済: 全ケースで **Σ sector_scores == score**。判定カウント合計は一部 N 未満になるが、
  これは**ホールド頭ミス→放棄で tick/tail が未判定**になる既存仕様（旧 golden も同様、密度増で未判定数が増えるだけ）。

## 成果物

- `vectors/regenerated/` … 既存 15 ベクターを新 Domain で再計算（`chart_data`/`replay_base64`/`input_events` はそのまま、
  `total_notes`・`x_micro`・`expected` のみ更新）。K の `testdata/replay_vectors/` に上書き突き合わせ可。
- `vectors/new_cases/` … 新規仕様ケース（下表）。`replay_base64` も `ReplayEncoder` で新規生成済み。
- `vectors/summary.json` … 旧→新の差分一覧（N・score・pp・great・miss）。

## 既存ベクターの新スコア（抜粋・summary.json が全件）

| case | N(旧→新) | score(旧→新) | 備考 |
|---|---|---|---|
| all_pp_test_song | 26→32 | 1000000→1000000 | 連続保持は密度増でも満点維持 |
| all_pp_test_song_2 | 82→114 | 1000000→1000000 | 〃 |
| all_pp_test_song_3 | 92→110 | 1000000→1000000 | 〃 |
| bpm_change_hold | 4→13 | 1000000→1000000 | bpm 変化で tick 間隔が追従するか |
| hold_guard_pass | 4→13 | 1000000→1000000 | ガード内離しは満点維持 |
| hold_guard_fail | 4→13 | 250000→230769 | 早離しで増えた tick が Miss |
| real_castella_song1_001_extra | 14→17 | 374642→367352 | hold×2 に tick +3 |
| real_castella_song2_003_extra | 14→15 | 981428→982666 | hold に tick +1 |
| real_castella_song3_003_extra | 14→15 | 749285→699333 | 〃 |
| ceil_n3 / ceiling_n7 / judgment_mix | 不変 | 不変 | tap のみ=密度非依存 |

## 新規ケース（仕様の肝を最小構成で固定）

| case | N | 検証点 |
|---|---|---|
| `density_4_4_baseline_hold` | 9 | 4/4 hold = head + (measureMs2000/8=250ms)×7 + tail、全 P+=満点 |
| `timesig_3_4_perfect_hold` | 17 | 3/4: measureMs=500×3×(4/4)=1500、tick=187.5 → 15 ticks |
| `timesig_6_8_perfect_hold` | 17 | 6/8: measureMs=500×6×(4/8)=1500（=3/4 と同密度）。分母経路の確認 |
| `timesig_4_4_to_7_8_cross` | 19 | hold が 4/4→7/8 を跨ぐ。tick 間隔を cursor ごとに再サンプル（250→218.75） |
| `hold_recover_after_release` | 13 | 離し>ガードで gap tick=Miss(コンボ断)、**復帰後の最初の1判定だけ Great**、以降 P+。pp10/great1/miss2/maxcombo8/score=845769 |
| `hold_recover_guard_inside` | 9 | tick@1250 が離し(1210)の40ms後=ガード内→P+。再押下はガード境界(50ms)で復帰扱いせず。全 P+=満点 |

## K 側で必要な対応（重要）

現状の `internal/engine/golden_test.go` の `parseChart` は **`type=="bpm"` のイベントしか読まず、
`jsonTempo` に `numerator/denominator` フィールドが無い**ため、timesig ケースをそのまま流すと 4/4 扱いになり N が合いません。

- `jsonTempo` に `numerator`/`denominator` を追加し、`type=="timesig"` を `engine.TempoEvent` へ取り込む。
- `engine` 側に小節長公式（`measureMs = 60000/bpm × num × 4/den`、`holdTickIntervalMs = measureMs/8`）と
  復帰状態機械（GUARD=50ms、復帰後 1 判定 Great）を実装（公式・状態機械は `2026-06-11_for_K.md` §2/§3 が正本）。
- 各ケースで `score`・`score_micro`・`total_notes`・`perfect_plus_count`/`great_count`/`miss_count`・`max_combo`・
  `sector_scores` が本ディレクトリの値と一致することを確認 → ランク戦投入可。
