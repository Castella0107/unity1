# スコア比率変更: Great 90% / Good 50% + ランク表改訂(SS新設) — 2026-07-08

## 仕様変更 1: 判定スコア比率

判定ごとのスコア比率(マイクロポイント `x_micro` に対する係数)を変更した。

| 判定 | 旧 | 新 |
|---|---|---|
| PerfectPlus / Perfect | 1 (満点) | 1 (変更なし) |
| Great | 199/200 (99.5%) | **9/10 (90%)** |
| Good | 3/4 (75%) | **1/2 (50%)** |
| Miss | 0 | 0 (変更なし) |

整数演算の定義(演算順序そのまま、比率のみ差し替え):

```
Great: scoreMicro += xMicro * 9  / 10   // 旧: xMicro * 199 / 200
Good:  scoreMicro += xMicro / 2         // 旧: xMicro * 3 / 4
```

- `x_micro = ceil(10^12 / N)`(切り上げ除算)は不変。`total_notes` 算出も不変。
- 判定ウィンドウ・ホールドtick密度・終端無敵ゾーン・WIDE判定は今回すべて非変更。
  **純粋にスコア係数のみの変更**。
## 仕様変更 2: ランク表改訂(SS 新設)

`ScoreCalculator.ComputeRank` の閾値を全面改訂。最上位に **SS** を新設。

| ランク | 閾値(以上) | 旧 |
|---|---|---|
| SS | **995,000** | (なし) |
| S+ | 990,000 | 997,000 |
| S | **975,000** | 990,000 |
| A+ | 950,000 | 950,000 |
| A | 900,000 | 900,000 |
| B | 800,000 | 800,000 |
| C | 700,000 | 700,000 |
| D | それ未満 | 同左 |

- rank はリプレイバイナリで 4 バイト固定幅のため "SS"(2 文字)は既存フォーマットに収まる。
  エンコード/デコードの変更は不要。
- golden vector の `expected.rank` もこの表で再計算済(例: 1,000,000→"SS"、980,000→"S"、
  940,000→"A")。

## クライアント側変更ファイル

- `Assets/_Project/Scripts/Domain/ScoreCalculator.cs`(マスター: 比率+ランク表)
- `Tools/ChartEditor/.../Domain/ScoreCalculator.cs`(sync_domain.py で同期済)
- `Assets/_Project/Scripts/UI/RankColors.cs`(SS 色追加)
- `Assets/_Project/Scripts/UI/Result/ResultController.cs`(エディタ用ダミーの rank)
- `Assets/_Project/Tests/EditMode/ScoreCalculatorTests.cs`
  (等価テスト改訂: 2 Good = 1 Miss、5 Great = 1 Good / ランク境界値テスト新表化)
- `Assets/_Project/Tests/EditMode/PlayRecordFactoryTests.cs`(満点 rank "S+"→"SS")
- `Tools/ParityVectors/Program.cs`(期待値式を新比率に)

## サーバー側 (K) 対応依頼

1. `internal/engine/score.go` の Great/Good 係数を上記の通り差し替え
   (整数演算・演算順序はクライアントと同一に: `x*9/10`、`x/2`)。
   あわせて `internal/engine/runner.go` の `computeRank` を新ランク表(SS 新設)に差し替え
   (golden_test が `expected.rank` を照合するため、rank 未対応だと全件 FAIL する)。
2. `testdata/replay_vectors/` の全ベクターの `expected` を
   本ディレクトリ `vectors/regenerated/` で**全件上書き**
   (chart_data / input_events / replay_base64 / total_notes / x_micro は不変、expected のみ更新)。
   `vectors/new_cases/` は既存 new_cases と同名の更新版。
3. golden 全件 PASS → ステージングデプロイ → ライブe2e で bit-perfect 確認するまで
   本比率でのランク戦投入は不可(2026-06-13 と同じ関門)。

## 再生成結果サマリ (vectors/summary.json)

Great/Good を含むベクターのみ score が変化。all-PP / all-Miss 系は当然不変。

| case | score(K手元testdata基準) | 新 score |
|---|---|---|
| ceil_n3_pp_great_miss | 665000 | 633333 |
| judgment_mix_test_song_1 | 668750 | 607142 |
| hold_recover_after_release | 845769 | 838461 |
| real_castella_song1_001_extra | 367352 | **452941** |
| real_castella_song2_003_extra | 982666 | 980000 |
| real_castella_song3_003_extra | 699333 | 940000 |

- real_castella 3本の「旧」列は K 手元 testdata の値(2026-07-04 REGEN_NOTES で指摘した
  第三の値のまま)。新値はホールド終端無敵ゾーン(2026-07-01仕様)+新スコア比率の
  現行クライアント Domain 実測。counts(pp/great/good/miss/maxCombo)は 2026-07-04 再生成分と
  同一で、score のみ比率変更分ずれる(例: song1 pp3/p1/gr3/gd2/miss8 →
  7.7 × x_micro = 452941)。
- 生成器は式計算とスナップショット(ScoreCalculator実測)の二重計算一致を検証済
  (不一致なら例外で停止する。全件通過)。

## 生成方法

2026-06-13 / 2026-07-04 と同一プロセス:

```
dotnet run --project Tools/ParityVectors -c Release -- ^
  C:\Users\CaSte\pvpharmonics-server\testdata\replay_vectors ^
  docs\handoff\2026-07-08_score_ratio\vectors
```
