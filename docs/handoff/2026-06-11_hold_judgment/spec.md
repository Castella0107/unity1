# ホールド判定の変更 — Go サーバー実装仕様

作成: 2026-06-11 / 宛先: K(Go サーバー担当)/ 起票: 友人テスターからの体感調整

クライアント(本体 Domain)は実装・ユニット検証済み。サーバーのリプレイ再判定が同一結果に
ならないと PVP のスコア/順位がズレるため、`internal/engine/` に同じ変更が必要です。

## 変更1: ホールド tick 間隔を 1/2 → 1/8 に(密度4倍)

`HoldTicksPerMeasure` を **2 → 8** に変更。

```
holdTickIntervalMs(t) = measureMs(t) / 8        // 旧: / 2
```

- 4/4・120bpm で tick 間隔 1000ms → **250ms**(八分音符刻み)。
- tick 生成ループ・末尾ガード(`HoldTailGuardMs = 1.0`)・小節長公式(timesig)は不変。割る数だけ変更。
- これにより **4/4 を含む全譜面**でホールド tick 数が増え、`total_notes` と満点(1,000,000)正規化が変わる。
  クライアントは `total_notes` をロード時に `ScoringEventCounter` で再計算するので JSON 改変は不要。
  Go 側も同じ数え方なら自動追従。**満点= total_notes 由来なので増えても 1,000,000 のまま**。
- timesig 公式の正本も更新済み: `docs/handoff/2026-06-11_timesig/spec.md` §2(/2→/8)。

## 変更2: ホールド「復帰」+ 復帰後の最初の1判定を Great に

**旧仕様**: 押下を離してガード(50ms)を超えると、そのホールドは**放棄**(残り tick を全部 Miss・以降回復不能)。

**新仕様**: 放棄しない。離している間の tick は Miss(コンボは断たれる)になるが、**押し直せば継続**できる。
復帰後の**最初の1判定(tick または尾)だけ Great**、それ以降は通常通り PerfectPlus に戻る。

### 状態機械(クライアント `HoldJudgmentTracker` と完全一致させること)
保持する状態: `isHeld`(押下中か)、`lastReleaseMs`(最後に離した時刻, 無し=-1)、
`recovering`(復帰直後フラグ)、`headJudged`、`tailJudged`、`abandoned`。定数 `GUARD_MS = 50`。

- **離上(release, time)**: `headJudged && !abandoned` のとき `isHeld=false; lastReleaseMs=time`。
- **再押下(press, time)**: `headJudged && !abandoned` のとき
  - `!isHeld && lastReleaseMs>=0 && (time - lastReleaseMs) > GUARD_MS` なら `recovering=true`(ガード超の離上から戻った=復帰)。
  - その後 `isHeld=true; lastReleaseMs=-1`。(ガード内の押し直しは復帰扱いせず無ペナルティ)
- **各 tick の判定**(時刻 tickTime, `headJudged && !abandoned` のときのみ進行):
  - `isHeld` のとき: `recovering` なら **Great**(そして `recovering=false`)、でなければ **PerfectPlus**。
  - `!isHeld` のとき: `sinceRelease = tickTime - lastReleaseMs`(lastReleaseMs<0 は 0 扱い)。
    `sinceRelease <= GUARD_MS` なら **PerfectPlus**(ガード内の短い離しは許容)、超なら **Miss**。
    ※ ここで放棄(abandon)はしない。再押下が来れば次 tick から復帰する。
- **尾(tail, currentMs>=endMs の最初の解決)**:
  - `isHeld` のとき: `recovering` なら **Great**(`recovering=false`)、でなければ **PerfectPlus**。
  - `!isHeld` のとき: `sinceRelease = endMs - lastReleaseMs`。`<=GUARD_MS` なら PerfectPlus、超なら Miss。
- **ヘッド未ヒットの放棄(abandoned)** は従来通り(頭の Good 窓を逃したらそのホールドは死亡)。
  ガード超の離上では abandoned にしない点だけが変更。

判定スコア重み(既存): PerfectPlus/Perfect=1.0、**Great=199/200**、Good=3/4、Miss=0。
コンボ境界=Good なので Great はコンボ継続、Miss は断。tiebreak(Σ2×P+ +P)に Great は寄与0。

## 依頼
- `internal/engine/` の tick 間隔を /8 に。`ScoringEventCounter` 相当の数え方が同ループなら自動追従。
- ホールド再判定に上記「復帰 + 復帰後 Great」を実装。リプレイ入力には離上/再押下イベントが
  記録されているので、再生時に同じ状態機械を回せば一致するはず。
- golden vector に **復帰ケース**(途中で離して押し直し)を追加し、`Score`/`x_micro`/`total_notes`/
  各判定カウント(PerfectPlus/Great/Miss)がクライアントと一致するか突き合わせたい。期待値 JSON は当方で出せます。
- これが揃うまでランク戦投入は保留(client/server でスコア定義が割れるため)。
