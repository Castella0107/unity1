# 【K向け】engine 再計算のスコア乖離 — 原因特定: ホールドティック間隔の定義違い

報告: Castella / 2026-06-08
対象: ステージングの WARN `suspicious replay submission: claim does not match server recomputation`(match 6e53ca69、player u_OF5VPBL4UZAA=Castella実プレイ 3曲)

## 結論

**サーバー engine のホールドティック間隔が Unity 実装と異なる。**

| | 実装 | ティック間隔 (175bpm時) |
|---|---|---|
| Unity (正) | `BpmTimeline.GetHoldTickIntervalMs` = **measure / 2**(1小節=4拍あたり2ティック) | ≈ 686ms |
| Go engine | `BpmTimeline.GetTickIntervalMs` = **beat / 16** | ≈ 21ms |

- Unity 側にも `GetTickIntervalMs`(beat/16)は存在するが、**ホールド判定には使っていない**
  (`HoldJudgmentTracker` と `ScoringEventCounter` は `GetHoldTickIntervalMs` を使用)。
- docs/08_battle.md §8.3 / match_format.md §12 の「ticks (BPM の 1/16 拍ごと)」は
  **同名類似関数の取り違えによる設計書側の誤記**とみられる。Go engine は誤記どおりに実装されている。
- 総採点イベント数 N が乖離するため(例: song_001/extra = Unity 14 / Go 約38)、
  `X_micro = ceil(10^12 / N)` が全イベントで変わり**スコア全体が乖離**する。
  ティック=自動 PerfectPlus 扱いのため pp/miss カウントも大きくズレる。

## 実測データ (match 6e53ca69、claim=Unity実測値 / engine=サーバーログ)

| 曲 | claim (Unity) | engine (Go) |
|---|---|---|
| Song1 song_001/extra | raw=374642, D, pp=1, p=1, gr=1, gd=3, miss=7, combo=2, **events=14** | score=212169, D, pp=7, miss=23 |
| Song2 song_003/extra | raw=981428, A+, pp=8, p=3, gr=2, gd=1, miss=0, combo=14, events=14 | score=965000, A+, pp=28, miss=0 |
| Song3 song_003/extra | raw=749285, **C**, pp=4, p=3, gr=2, gd=2, miss=2, combo=6, events=14 | score=423333, **D**, pp=8, miss=1 |

- engine の pp=28 は Unity の総イベント数 14 を超えており、ティック過剰生成の直接証拠。
- Song3 はランク自体が変わる (C↔D) ため、セクター勝敗・試合結果に影響しうる。

## 修正提案

1. Go engine のホールドティックを Unity 準拠に変更:
   - `tickInterval = measureMs / 2`(measure = 4拍 = `4 × 60000 / bpm` → interval = `2 × 60000 / bpm`)
   - Unity 側には**末尾ガード**もある: `EndMs から HoldTailGuardMs 以内のボディティックは生成しない`
     (tail との二重加算防止)。`BpmTimeline.cs` の `HoldTailGuardMs` 定数と
     `HoldJudgmentTracker.cs` のループを正として移植してほしい
2. docs/08_battle.md §8.3 / match_format.md §12 の該当記述を修正
3. golden テストベクタの再生成 — **本物の Unity リプレイ+期待値を同梱した**:
   - `replays/song1_song_001_extra.replay`(base64前の生バイナリ、gzip+CRC32形式)
     → 期待値: raw=374642 rank=D pp=1 p=1 gr=1 gd=3 miss=7 maxCombo=2 (miss=tap+holdヘッドのみの定義で7)
   - `replays/song2_song_003_extra.replay` → raw=981428 A+ pp=8 p=3 gr=2 gd=1 miss=0 combo=14
   - `replays/song3_song_003_extra.replay` → raw=749285 C pp=4 p=3 gr=2 gd=2 miss=2 combo=6
   - セクタースコア合計=raw(5セクターは譜面末尾×0.2/0.4/0.6/0.8 区切り、こちらはサーバーと一致確認済み)

## 補足

- 案B(不一致でも engine 値採用・続行)のおかげで試合自体は完走できており、結合は継続可能。
  ただし**修正までは engine スコアが Unity 表示と食い違う**(プレイヤーには自分の画面と違う点数で採点される)。
- クライアント側を beat/16 に合わせる案は不採用としたい: 既存の全リプレイ・記録・スコアバランスが壊れるため。
  C# Domain を仕様の正とする従来合意(2026-05-24)どおりでお願いしたい。
