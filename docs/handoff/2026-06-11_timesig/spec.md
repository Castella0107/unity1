# 変拍子（time signature）対応 — Go サーバー実装仕様

作成: 2026-06-11 / 宛先: K（Go サーバー担当）/ 起票: ChartEditor 変拍子対応に伴い

## 0. 背景と要点
ChartEditor に「変拍子の入力」と「基準拍（小節の頭）アンカー設置」を追加する。仕様決定で
**ホールド tick（=スコア）も拍子に追従**させることになった。tick 間隔は小節長に依存するため、
**クライアント(本体Domain)・ChartEditor・Go サーバーが同一公式で小節長を計算しないと PVP のスコア
（tick 数・満点 1,000,000）がズレる**。本書はその契約と Go 側の変更点をまとめる。

**後方互換**: `timesig` イベントが無い譜面は全編 4/4 として従来と完全に同一の結果になる。
既存の seed 譜面（全て 4/4・timesig 無し）は影響を受けない。

## 1. データ形式（3リポ共通の契約）
`chart.events[]` に新しいイベント種別 `timesig` を追加する。サーバー JSON は snake_case:

```json
{ "type": "timesig", "time_ms": 12345, "numerator": 7, "denominator": 8 }
```

- `time_ms`: その拍子が始まる **小節の頭（バーライン起点 / 基準拍）**。複数置ける。
- `numerator`: 1 小節の拍数（例 7/8 → 7）。
- `denominator`: 1 拍の音価（4=四分音符, 8=八分音符）。
- `timesig` イベントが 1 つも無ければ **全編 4/4**。最初の `timesig` 以前の区間も 4/4。
- 既存の `bpm` / `speed` イベントと同じ `events[]` 配列に混在。`bpm` 変化と `timesig` 変化は独立。

（ChartEditor の保存はキャメルケース `{"type":"timesig","timeMs":...,"numerator":...,"denominator":...}`。
サーバー配信 JSON は従来通り snake_case。両者の差は既存の note フィールドと同じ扱い。）

## 2. 小節長とホールド tick の公式（最重要・全実装で一致必須）
時刻 t における実効 BPM・拍子を用いて:

```
quarterMs(t)      = 60000 / bpm(t)                       // 四分音符長
measureMs(t)      = quarterMs(t) * numerator(t) * (4 / denominator(t))
holdTickIntervalMs(t) = measureMs(t) / 2                 // HoldTicksPerMeasure = 2 据え置き
```

例（bpm=120, quarterMs=500）:
| 拍子 | measureMs | tick間隔 |
|---|---|---|
| 4/4 | 500×4×(4/4)=2000 | 1000 |
| 3/4 | 500×3×(4/4)=1500 | 750 |
| 6/8 | 500×6×(4/8)=1500 | 750 |
| 7/8 | 500×7×(4/8)=1750 | 875 |

4/4 は従来値（measureMs=4拍, tick=2拍）と一致 → 後方互換。

ホールドの tick 生成ループ（既存と同じ。間隔だけが拍子依存になる）:
```
cursor = startMs
loop:
  cursor += holdTickIntervalMs(cursor)     // ← measureMs が拍子依存に変わる
  if cursor >= endMs - HoldTailGuardMs: break   // HoldTailGuardMs = 1.0 据え置き
  emit tick at cursor
```
`bpm(t)` / `numerator(t)` / `denominator(t)` は **その時刻以前の最後の該当イベント**（時刻昇順、
無ければ bpm=120 / 4/4）。クライアント実装と完全に同じサンプリングにすること。

## 3. Go 側の変更点（`internal/engine/`）
1. **チャート/イベントの取り込み**（`chart.go` などチャート JSON → engine 入力）
   - `events[]` から `type=="timesig"` を読み、`(time_ms, numerator, denominator)` のリストを時刻昇順で保持。
2. **`bpm.go`（BpmTimeline 相当）**
   - `GetTimeSignatureAt(timeMs) -> (num, den)`: timesig が無ければ (4,4)。
   - `GetMeasureIntervalMs(timeMs)`: §2 の `measureMs` 公式に変更（現状 `beatMs*4` 固定 → `beatMs*num*(4/den)`）。
   - `GetHoldTickIntervalMs(timeMs) = GetMeasureIntervalMs/2`（呼び出しはそのまま、中身が拍子依存に）。
3. **`hold.go` の `computeTicks` / `count.go`（ScoringEventCounter 相当）**
   - 既に `GetHoldTickIntervalMs` を使っているなら **変更不要**（自動追従）。ベタ書きしている箇所があれば公式へ寄せる。
4. **満点(x_micro)計算**: tick 数が拍子で変わるため、`total_notes`/採点イベント総数の再計算が
   クライアントと一致することを golden で確認（§4）。

## 4. パリティ検証（golden vector）
`testdata/replay_vectors/` 形式に **拍子付きの新規ケース**を追加し、クライアントと同値を確認したい:
- ケース例: 3/4 のみ / 6/8 のみ / 途中 4/4→7/8 切替 / ホールドを跨ぐ変拍子。
- 各ケースで `chart_data.events[]` に `timesig` を含め、`PerfectPlusCount` 等・`Score`・`x_micro`・
  `total_notes` がクライアント（本体Domain）と一致すること。
- クライアント側の期待値は、Domain の `ScoringEventCounter`/`HoldJudgmentTracker`（拍子対応済み）で生成可能。
  必要なら当方で期待値 JSON を出力して共有する。

## 5. クライアント側の実装状況（2026-06-11 時点）
本体 Domain は実装済み・コンパイル確認済み（4/4 完全後方互換）:
- `TempoEvent` に `Numerator` / `Denominator` 追加。
- `BpmTimeline`: `GetTimeSignatureAt` 追加、`GetMeasureIntervalMs` を §2 公式へ変更、
  `GetHoldTickIntervalMs` は自動追従。`ScoringEventCounter` / `HoldJudgmentTracker` は本クラス経由で追従。
- `ChartParser`（ローカル/エディタ保存の読み込み）と `ServerChartConverter`（サーバー配信 JSON）で
  `timesig` を取り込み済み。
- ChartEditor 側の小節線描画・小節移動・メトロノーム・拍子マーカー設置 UI は実装中（別途）。

## 6. 依頼
- §1 の JSON 形式と §2 の公式で問題ないか確認・合意してほしい。
- Go `engine` に §2/§3 を実装し、§4 の golden で当方とパリティを取りたい。
- 形式に異論（フィールド名・配置・denominator の扱い等）があれば早めに教えてほしい。実機投入前に揃えたい。
