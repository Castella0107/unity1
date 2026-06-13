# K への引継ぎ — 2026-06-13 判定ウィンドウ変更（確定版）

宛先: K（Go サーバー担当）/ 起票: 2026-06-13 / 種別: リプレイ再判定・スコアのパリティ

クライアント（本体 Domain）側で**判定ウィンドウ（Great / Good の許容幅）を変更**し、コンパイル・値確認済みです。
判定の振り分けが変わる＝スコアが変わるため、**サーバーのリプレイ再判定が同じ結果にならないと
PVP のスコア・順位・レートがズレます**。この1枚で完結します。

> **4/4・timesig 無しの譜面でも結果が変わります**（後方互換ではない）。
> パリティが揃うまで **ランク戦への投入は保留**でお願いします。公式・値に異論があれば早めに。

---

## TODO 一覧（K 側 `internal/engine/`）

- [ ] **① Great の許容幅を ±50ms → ±66.6667ms（4 フレーム）に**
- [ ] **② Good の許容幅を ±83ms → ±100.0ms（6 フレーム）に**
- [ ] **③ ①② を反映した golden vector でクライアントとパリティ確認**

P+（PerfectPlus ±16）と P（Perfect ±33）は**変更なし**。

---

## 1. 変更内容（判定ウィンドウ定数, ミリ秒・両側 `|inputTimeMs − noteTimeMs|`）

判定幅は **60fps のフレーム基準**。1 フレーム = `1000.0 / 60.0 ≈ 16.6667ms`。

| 判定 | フレーム | 式 | 旧値 | **新・確定 double 値** |
|---|---|---|---|---|
| PerfectPlus | ~1f | （整数維持） | 16 | `16` |
| Perfect | ~2f | （整数維持） | 33 | `33` |
| **Great** | **4f** | `1000.0/60.0 * 4` | 50 | **`66.666666666666671`** |
| **Good** | **6f** | `1000.0/60.0 * 6` | 83 | **`100`（厳密に 100.0）** |

判定アルゴリズム（不変）:

```
abs = |deltaMs|
abs <= 16                     -> PerfectPlus
abs <= 33                     -> Perfect
abs <= 66.666666666666671     -> Great     // ★変更
abs <= 100.0                  -> Good       // ★変更
それ以外                       -> Miss
```

境界は **`abs <= window`（境界値を含む）**。整数 delta なら 66=Great / 67=Good / 100=Good / 101=Miss。

## 2. 実装上の注意（最重要・bit-perfect の肝）

- **Great は丸めリテラル禁止。必ず式 `1000.0/60.0 * 4` で算出すること。**
  `66.6667` 等の丸め値を使うと、その差（~0.0003ms）の帯に落ちた delta で判定が分かれ、
  非同期な微小スコア乖離になります。クライアントは `const double GreatMs = FrameMs * 4`（`FrameMs = 1000.0/60.0`）。
- **Good は `(1000.0/60.0) * 6` が IEEE754 で厳密に `100.0`** になります（C# で `good == 100.0` が True を確認済み）。
  そのためリテラル `100.0` で書いても一致します。式・リテラルどちらでも可。
- C# / Go とも IEEE754 double。**同一の演算（`1000.0/60.0` を先に計算して整数倍）**であれば同一ビットになります。

## 3. クライアント側の正本

- `Assets/_Project/Scripts/Domain/JudgmentWindow.cs`（Unity 非依存・サーバー判定パイプラインと verbatim 共有の定数）
  - `FrameMs = 1000.0 / 60.0`
  - `GreatMs = FrameMs * 4`
  - `GoodMs  = FrameMs * 6`
- スコア係数は不変（P+ = 満点 / Great = 199/200 / Good = 3/4、各判定で個別 floor）。

## 4. パリティ確認

- クライアント本体 Domain と同一ソースを走らせる `Tools/ParityVectors`（net10 コンソール）で、
  この判定幅を反映した**新しい golden 期待値を再生成**します（2026-06-13_parity と同方式）。
  → 別途 `docs/handoff/2026-06-13_judgment_window/vectors/` に出力して引き渡します（本 md とは別便でも可）。
- 各ケースで `score` / `score_micro` / `total_notes` / `perfect_plus_count` / `great_count` / `good_count` /
  `miss_count` / `max_combo` / `sector_scores` が一致することを確認 → ランク戦投入可。

> 注: 判定幅変更は **total_notes（N）は変えません**（tick 密度は変えていない）。
> 変わるのは各 delta の Great/Good/Miss への振り分けと、それに伴う score だけです。
> （2026-06-13_parity のホールド tick 密度変更とは独立の変更です。）

## 5. 実測差分（`Tools/ParityVectors` で再生成済・`vectors/` に同梱）

ベースは `2026-06-13_parity`（tick 密度変更済）の値。判定幅変更で **score が動いたのは
off-timing リプレイを持つ実譜面ケースのみ**。満点ケース・全ミス・境界を跨がないケースは不変。

| case | N | score（密度後 → 判定幅後） | 効果 |
|---|---|---|---|
| real_castella_song1_001_extra | 17 | 367352 → **557941** | Miss/Good だった音が Good/Great に昇格 |
| real_castella_song2_003_extra | 15 | 982666 → **999000** | 〃 |
| real_castella_song3_003_extra | 15 | 699333 → **915333** | 〃 |
| all_pp_* / timesig_* / density_* | 各 | 1000000（不変） | 満点ケースは窓拡大の影響なし |
| all_miss_test_song | 32 | 0（不変） | 全 Miss は窓拡大でも範囲外のまま |
| ceil_n3 / judgment_mix / hold_guard_fail / hold_recover_* | 各 | 不変 | delta が新境界（66.6667 / 100）を跨がず |

`vectors/regenerated/`（既存ベクター再計算）+ `vectors/new_cases/`（仕様ケース）+ `vectors/summary.json`（旧→新差分）同梱。
全ケースで **Σ sector_scores == score** の不変条件を Program 内でチェック済み（不一致なら例外で停止）。
