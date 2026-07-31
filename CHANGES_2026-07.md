# 変更まとめ (2026-07-16 〜 07-30) — コミット準備資料

前回コミット `2516464 視点等の修正` 以降の未コミット変更の全容。
集計: 変更 219 ファイル (+39,418 / −32,497)、新規 313 / 削除 184 (大半は StreamingAssets 楽曲整理とスクリーンショット)。

---

## 1. プレイフィールド刷新 (モック「画面設計モック(4)(5)」再現)

画面仕様 (1280×720 スペック座標) 駆動の床デカール方式で、モックのプレイ画面を再現。

- **新規**: `Scripts/Game/FxSectorGeometry.cs` — 幾何コア。スペック座標→実効スクリーン px→カメラ逆投影で床平面へ。中央レーン定数 (VP(640,160)/judgeY600/near147)、sRGB⇔リニア合成差補正 (DarkA/BrightA)、アスペクト比補正
- **新規**: `Scripts/Game/CenterTrackVisuals.cs` / `FxLaneVisuals.cs` — 中央トラック (床/側壁/縁/分割線/破線/判定ストリップ/判定線/デッキ影/DFJK 遠近台形キャップ) と FX レーン (扇形面/放射エッジ/判定弧二層/S/L 角丸円環ボタン/琥珀アクセント/押下フラッシュ/呼吸シーン) の ExecuteAlways 床デカール
- **新規**: `Scripts/Game/FxArcNote.cs` / `FxArcMeshBuilder.cs` / `FxLanePath.cs` — FX 円弧ノーツ (Glow+Core 二層、WACCA 方式で半径方向へ流れる)
- **新規**: `Shaders/PlayfieldAlpha.shader` / `PlayfieldAdditive.shader` — renderQueue 直指定の自作 unlit (URP 標準はキュー正規化されるため不可)。ColorMask RGB (フレームバッファα破壊防止)
- **新規**: `Editor/PlayfieldRedesignBuilder.cs` — Tools/Playfield/1〜6 の構築メニュー (カメラ数値ソルバ・背景ワールドクアッド・マテリアル群・HUD リスタイル)。`Editor/PlayfieldAssetImporter.cs` / `FxArcNotePreview.cs`
- **新規**: `Materials/Playfield*.mat` 群・`Art/Playfield/` テクスチャ群 (bg_city, key_S/L 等。約25枚は初期方式の名残で現在未使用)
- **変更**: `StageInitializer.cs` — カメラを授権フレーミング (pos(0,3.696,−4.118)/pitch15.48°/FOV79.02) に固定。旧 2D/3D プリセット廃止
- **変更**: `GameHud.cs` (MAX COMBO/SEC/セクターカウンター/ハロー追加、ゼロ埋め書式)、`JudgmentColors.cs`/`JudgmentDisplay.cs` (COLOR_SPEC 配色)、`GameColorSettings.cs`、`LaneVisuals.cs`、`JudgmentEffectsController.cs`、`ReplayHud.cs` (リプレイ時の子再有効化)
- **変更**: `Settings/InputActions.inputactions` — デフォルトキー変更: Lane=D/F/J/K、FX=S/L (旧: シフト系)
- **変更**: `Scenes/GamePlay.unity` — 上記の配線・HUD Canvas を高さ基準スケール (matchWidthOrHeight=1) に

## 2. FAR_WALL_SPEC (奥端処理: フェード廃止 / 透明遮蔽壁 / レーン長設定)

- 距離フェード全廃 (ノーツ・レーン縁は opacity=1 定数)。旧 FadeIn/CenterFog/InnerFade を削除
- 透明遮蔽壁: 中央=シェーダ clip (`_WallClipOn`+グローバル `_PlayfieldWallZ`)、FX=ジオメトリの半径クランプ。壁自体は不描画・ハードエッジ
- コンフィグ `laneLength` (0.25〜1.0、既定 1.0): Config ゲームプレイタブ「レーン長」スライダー (25〜100%)。壁位置が即時追従
- `LaneLayout.NoteSpawnZ` 22→34 (壁の裏からスポーンし、壁通過時に全不透明で出現)
- FX の同心ガイド弧 (r180/290/400) は紛らわしいため削除 (K 指示)

## 3. FX_ALIGN_SPEC (FX レーンと中央レーンの奥行き整合)

小節線・ノーツが中央/FX で折れ曲がる問題の根本修正。

- FX 内部座標系 (VP(640,256)/1.26 変換/near155) を**完全廃止**。FX の全円弧は中央レーンの消失点 (640,160) 中心の同心円 (制約A)
- 進行度 s(z)=147/(147+31.8z) を中央と共有し R(z)=JudgeRadius·s(z) (制約B)。JudgeRadius=440/sin(内側エッジ角) の関係で、任意の z で FX 弧の内側端点の高さ=中央線の高さ (実測 dy≦0.16px)
- 翼 (FX レーン) はトラック端+5° の隙間 (K 指示「くっつけない」)。SectorThetaMin=122.553°/JudgeRadius=522.01
- 小節線: 1小節ごとに「中央の水平線+FX 両翼の同心弧+隙間を渡るブリッジ」を 1 本の線として描画 (`BeatLineScroller.cs` 全面改修、ハイスピード/speed イベント同調、拍線は中央のみ)

## 4. 判定タイミングの重大バグ修正

- **`GamePlayController.cs`/`ReplayPlaybackController.cs`**: 音源シフトに `FirstOnsetMs` を誤加算していた問題を修正。firstOnsetMs は「音源内の最初のオンセット位置」というメタ情報であり、加算すると音楽だけが遅れて全ノーツが一律ズレる (song_013 で実測 706ms)。シフトは `AudioOffsetMs` のみに
- **`Network/Api/ServerChartConverter.cs`**: サーバー配信譜面の時刻キーを snake_case/camelCase 両対応に (air-chart アップロード譜面が全ノーツ timeMs=0 になり開始直後に全ミスするバグ)。可変 BPM/speed イベントの取り込みも追加
- `Prefabs/Effects/JudgmentParticle.prefab` + `Materials/Effects/JudgmentParticleMat.mat` (新規) — 判定パーティクルのマテリアル欠損 (マゼンタ矩形) を 自作 Playfield シェーダで修復

## 5. コンフィグ画面

- **2階層ナビゲーション** (`ConfigController.cs` +191/−24): タブレベル (←→=タブ切替・↓=項目へ) / 項目レベル (↑↓=項目移動+紫ハイライト・←→=値変更・最上で↑=タブへ)。Colors タブで←→がスライダーに奪われ右のタブへ進めない不具合の解消。L/R Shift・Tab/E/Q・パッド維持。ヒントバー階層別表示
- **レーン長スライダー追加** (`GameplayTabController.cs`/`BuildConfigScene.cs`、§2 参照)
- **キー設定タブ** (`InputTabController.cs`): キーキャップの長いキー名はみ出し修正 (Right Shift→R-SHIFT 等の短縮+TMP オートサイズ)。「任天堂配置」表記を「右=決定」に変更 (商標配慮、K 指示)
- `Scenes/Config.unity` はビルダー (`BuildConfigScene.cs`) から再生成

## 6. 選曲画面 (`SongSelectController.cs`)

- 曲リストを chart-admin 由来の楽曲のみに (sample_*/test_song* を除外)
- 既定ソートを「SONG ID」順に (F4 で TITLE/BPM 切替は維持)
- **StreamingAssets 整理**: `sample_01〜20`・`test_song_1〜3` を削除 (−184 ファイルの大半)。`test_song` は EditMode テストのフィクスチャのため**残置** (リストには出ない)。`song_004〜013` の譜面/メタを追加配置 (tools/sync_songs_to_unity.py による同期)

## 7. UI 網羅チェックの修正 (詳細: UI_FIX_LOG.md #1〜7)

- SongSelect: 旧フッター二重表示の非活性化
- Title / SongRanking: 上下テキスト重なり修正
- Config / Result / PVPLobby: フッターボタンのヒントバー沈み修正、判定カウント見切れ、見出し見切れ、「PRESS F5」誤記→SPACE
- 判定表示 (MISS 等): 判定ライン寄り (y−200) へ移動・縮小 (52→34pt)・半透過 (α0.6、`JudgmentDisplay.cs`)
- 対応シーン+ビルダー (`ResultSceneBuilder.cs`/`BuildPvpScenes.cs`) の両方を更新

## 8. その他

- `UI_FINDINGS.md` / `UI_FIX_LOG.md` (新規) — UI チェックの発見と修正記録
- `Assets/Screenshots/` に検証用スクリーンショット約 198 枚 (**コミット除外推奨** — 下記参照)
- `.vsconfig` (Visual Studio コンポーネント定義、Unity 自動生成)

## 9. 追加修正 (07-29 午後、K 指示)

### FX レーンの小節線を細く
- `BeatLineScroller.cs`: FX 円弧小節線の全幅 `FxArcWidthPx` 4.8 → 2.4px (判定弧基準。奥ほど細くなる遠近則は従来どおり)

### コンフィグ: 増減の最小単位化
- **ハイスピード 0.1 刻み**: `GameplayTabController.cs` — スライダーを 10 倍整数 (5〜200) で保持し、←→キー 1 押下 = 0.1 に (旧: Unity Slider 仕様でレンジ 10% ≈ 2.0 も動いていた)。◁▷ステッパーも 0.1 に統一 (`SliderStepper.SetDeltaMagnitude` 新設)。PlayerPrefs "HiSpeed" の値域・型は従来どおり 0.5〜20 の float で他コードへの影響なし
- 曲選択プレイオプションの速度も 0.5 → **0.1 刻み** (`PlayOptionsController.cs`、float 誤差防止の丸め付き)
- 判定/表示タイミング補正は元から 1ms 刻み (整数スライダー) のため変更なし

### コンフィグ: 行単位ナビゲーション
- `ConfigController.cs`: 項目レベルの↑↓を「行移動」に変更 (行 = パネル直下コンテナ単位)。キー設定タブの 6 キーキャップが 1 行扱いになり、↓で隣のキーではなく次の行へ。←→は複数項目行では行内移動 (キー間の移動)、Slider・単独項目行では従来どおり値変更
- **Space = スライダー複数行で「決定して行内の次へ」** (色タブの R→G→B→R 循環)。←→を値変更に使う色行で G/B にキーボード到達できなかった問題の解消。ボタン/トグルの Space 決定は従来どおり
- ヒントバー表記を「↑↓: 行移動 / ←→: 値変更・行内移動 / Space: 決定・次へ」に更新

### コンフィグ: プリセットパレット移設
- `BuildConfigScene.cs`: 色タブのパレット (旧: 左カラム最下部 12 列×2 段) がコンテンツ下端に食い込みスウォッチが重なって選べなかったため、右カラム・2D ピッカー直下の 6 列×4 段へ移設。ラベルは「プリセット (クリックで選択中の行に適用)」に短縮。`Scenes/Config.unity` はビルダーで再生成済み

### ハイスピードの体感補正
- `NoteScroller.cs`: HiSpeed 設定値→ワールド速度の係数 `HiSpeedScale = 20/12` を導入 (「設定 20 が他ゲームの 12 相当で効果が弱い」K 実感の補正)。設定値の保存域 (0.5〜20) は不変。ノーツ・小節線は共に `ScrollSpeed` 参照のため同期維持

### ホールドの「取れていない」視覚表示 + 判定文字のふち
- 取れていないホールド (頭ミス、またはガード超過の離上中) を**若干グレー**表示に。復帰 (再押下) した瞬間に通常色へ戻る
  - `NoteController.cs`: `IsHoldDropped` 状態と `EffectiveLaneColor` (レーン色→グレー 55% lerp)。中央レーンは毎フレームの色適用、FX 円弧は状態変化時に `FxArcNote.SetColor`
  - `JudgmentSystem.cs`: 頭ミス/Miss ティックでグレー化、取れたティックで解除。押下した瞬間にも即時解除 (次ティック確定を待たない)
  - `JudgmentEngine.cs`: ビジュアル用の `GetActiveHoldNoteId` を追加 (判定には無影響)
  - 判定意味論は不変 (PVP パリティ維持)。なお途中復帰ロジック自体は既存実装が機能していることをコード検証済み (頭を取っていれば離上→再押下で復帰、復帰後最初の 1 判定のみ Great。頭ミスのホールドは放棄 = 復帰不可、これはサーバーと同一意味論)
- `JudgmentDisplay.cs`: 判定文字 (PERFECT+/MISS 等) と FAST/LATE に SDF アウトラインのふち (ほぼ黒、幅 0.22) を追加。フェードアウトにふちの透明度も追従

### 音量設定が楽曲に効かないバグ修正
- 原因: 音量経路が AudioMixer 前提 (`AudioVolumeBinder` → dB パラメータ) だが、`_Persistent.unity` の binder は `_mainMixer` 未割当・`NewAudioMixer.mixer` も公開パラメータ空で、楽曲 AudioSource にはどこからも音量が適用されていなかった (常に 1.0 = 最大)
- `AudioVolumeBinder.cs`: ミキサー未割当時の直接制御を実装。静的 `CurrentMusic01`/`CurrentSfx01` (= マスター% × 各%、PlayerPrefs 基準で生成順非依存) と `VolumeChanged` イベントを追加。効果音のフォールバックにもマスターが効くように
- `AudioConductor.cs`: Awake で実効音量を適用し `VolumeChanged` を購読 (再生中でも即時反映)。OnDestroy で購読解除
- `HitSoundPlayer.cs`: 生成時に実効音量を適用 (binder との生成順に依存しない)
- ミキサーを将来配線した場合は従来の dB パラメータ経路がそのまま本線になる

### セクター未定義曲の自動 5 分割
- `ScoringEventCounter.SectorDefsFromChart` 新設 — 譜面の最終ノーツ終端から時間 5 等分の SectorDef (S1〜S5) を生成。境界値は既存の採点フォールバック = Go サーバー `engine.SectorEndsFromChart` と**同一値**のため PVP セクター集計とのパリティ維持
- `GamePlayController.cs` / `ReplayPlaybackController.cs`: 譜面読込後、meta.Sectors が空なら自動生成をセット → sectors 未設定曲 (song_001〜004 等) でも HUD のセクターダイヤ S1〜S5 が表示される。meta.json に定義がある曲はそちらが優先
- `JudgmentRunner.cs`: リプレイ再判定のセクター境界も同フォールバックに統一 (旧: null でセクター集計なし)

---

## 10. 追加修正 (07-30、K 指示)

### CAMERA_SPEC: 消失点を画面上部へ (screen y=120)
- カメラを数値ソルバで再解 (`Tools/Playfield/1`、固定 16:9 数式射影に変更 — Game ビューの
  アスペクト比に依存しない): y=4.4344 z=-3.1658 pitch=28.847。**FOV (79.016) と判定線距離
  (5.174) は旧解と同一** = ズーム/transform ハック無し、判定ライン・キーの画面位置は不変 (Δ≤0.07px)
- スペック定数連動更新 (`FxSectorGeometry.cs`): CVpY 160→19.412 (screen 240→120)、
  CDepth 580.59 (導出化)、SectorThetaMin 116.782°、JudgeRadius 650.35、CNear 161.82
  (実射影から再導出+可視域最適化 — s(z) モデル誤差 max 0.26px で FX/中央の継ぎ目維持)
- 受け入れ検証: 全奥行き線の延長交点 = (640.00, 119.5)、判定線ビフォーアフター一致、FX 弧は
  スペック座標の真円のまま (デカール方式に変更なし)
- 背景 (Tools/Playfield/2 再実行): 消失点グローを screen y≈113.5 へ再配置
- `StageInitializer.ApplyCameraAngle` の絶対値も同期済み

### レーン長の上限拡張 (100% → 200%)
- `FxSectorGeometry.LaneLengthMax` 1.0→2.0、コンフィグのレーン長スライダー 25〜200%
  (`GameplayTabController` + `BuildConfigScene`、Config.unity 再生成済み)
- `LaneLayout.NoteSpawnZ` 34→50 (LL=2 の壁 z=45 より奥からスポーンさせ壁貫通ポップイン防止)

### HEAD_RECOVERY_SPEC: ホールドの頭ミスでも途中復帰可能に (**Unity + Go 両側**)
- 頭オートミスでホールドを放棄せず**アクティブ化**: 以降のティックは Miss で発火し、
  再押下で復帰 (復帰後最初の 1 判定 Great → 以降 P+)。短ホールドの「無条件 P+」は頭を取った
  場合のみ維持 (頭ミス時は 押下中=復帰判定 / 離上中=Miss)
- Unity: `HoldJudgmentTracker.cs` / `JudgmentEngine.cs` (IsAbandoned 廃止)。
  Go: `internal/engine/hold.go` / `runner.go` (頭ミスループを NoteID 昇順で決定化+レーンアクティブ化)
- **採点の可視変化**: 取れなかったホールドのティック/尾も Miss としてカウントされる
  (旧: 頭 Miss 1 個のみ)。Go 期待値ベクター更新: all_miss_test_song miss 20→32、
  short_hold_head_miss miss 1→2 (SPEC.md も改定)。**Go 側リポジトリのコミットと同時デプロイ必須**
  (片側だけ入れると PVP 再判定のスコアパリティが壊れる)
- テスト: Go 全緑 / Unity EditMode 294/294 緑 (頭ミス復帰の新テスト 2 件追加)

### ノーツ音: air-chart のサンプルを標準化+選択可能に
- air-chart 埋め込みのノーツ音 WAV を抽出し `Resources/SFX/note_hit_airchart.wav` として同梱。
  タップ時の効果音の**既定をこのサンプルに変更** (`HitSoundPlayer`、読込失敗時はシンセへフォールバック)
- コンフィグ「オーディオ」タブに「ノーツ音」ドロップダウンを追加
  (スタンダード (エディタと同じ) / クリック (シンセ)。変更時に即試聴。PlayerPrefs "NoteSoundIdx")

## 11. 追加修正 第2弾 (07-30 午後、K 指示)

### プレイフィールド
- **中央レーン線を実線化**: 破線 (dash 4/7) を廃止し、判定線から壁まで途切れない実線に (`CenterTrackVisuals.cs`)
- **カメラをさらに遠くへ**: FieldScale 0.85→0.76 (固定点 (640,690) 縮小、判定幅×0.894 = 距離×1.118)。
  VP は screen 120 を維持 (ローカルでは (640,-60))。θmin 114.440° / JudgeRadius 724.96。
  カメラ再ソルブ確定: y=5.0411 z=-3.4361 pitch=28.8534 fov=79.016 (判定線距離 5.174→5.834)、
  CNear=181.33。スクリーンショット: Assets/Screenshots/playfield_after_camera_far.png
- **FX レーンの巨大化を補正**: VP 上昇で JudgeRadius が 522→725 に拡大し翼が大きく見えたため、
  `SectorThetaMax` 159°→143.61° (判定弧の弦長をリデザイン時のスクリーン 277px に一致)
- **消失点グロー (VanishGlow) を廃止** (K 指示)。ビルダーから削除 — Tools/Playfield/2 再実行で反映

### ノーツ / 判定
- **同時押しノーツを黄色に**: 同一時刻 (ms 丸め) に 2 ノーツ以上あるものを `GameColorSettings.ChordColor`
  (既定 #FFD740) で表示 (`NoteScroller` 検出 + `NoteController.SetChord`)。色タブに「同時押しノーツ」行を追加

### UI / メニュー
- **コンボ表示を実装** (`GameHud.cs` 実行時生成): 3 コンボ以上で現在コンボを表示、増加時ポップ。
  コンフィグ「ゲームプレイ」に「コンボ表示」(ON/OFF) と「コンボ表示位置」(中央/上部中央/判定ライン下) を追加
- **「継承済」表記を削除**: プレイ画面曲情報のモック用フレーバーテキスト「器 REC.FRAGMENT — 継承済」を廃止
  (Tools/Playfield/5 再実行で既存オブジェクトも削除)
- **アカウント作成のパスワード要件表示**: 新規登録モードで規則 (8〜128 文字・半角英数記号・
  大文字/小文字/数字/記号の 2 種以上 = サーバー validatePasswordStrength と同一) を常時案内+ローカル事前検証

### PVP
- **テストソングをドラフト候補から除外**: title が "Test Song" で始まる曲を PVP プールに出さない
  (`ServerSongLibrary.PvpSongIds` / `PvpDraftController`)
- **リザルト非表示バグ修正**: 先に提出した側は submit レスポンスに song_result が無く「相手の提出待ち」の
  まま詳細が出なかった (K 報告「song3 だけ出た」= 後攻提出だった曲だけ表示)。サーバーに既設の
  `GET /matches/{id}/songs/{order}/result` を叩いて相手提出後に詳細を表示+累計を後攻側と同一規則で加算
  (`PvpApi.GetSongResultAsync` / `SongResultFetchDto` / `PvpSongResultV2Controller`)

### 追加 (07-30 夕)
- **FX レーンの立体感を復元**: `SectorThetaMax` 143.61°→150° (「少し大きく見えても OK」の K 確認済み。
  底面の整合は θmax 非依存 — VP 同心円+s(z) 共有のため内側端点は常に中央線と厳密一致)
- **レーンの縁・中央線・判定ストリップを「判定線」色に統一** (`CenterTrackVisuals.cs`):
  固定の琥珀 (P-Core/P-Glow) をやめ、色タブの JudgmentLineColor (既定 #7DEEFA シアン) を RGB ソースに。
  FX 判定弧と同系になり、色タブから一括変更可能
- **タップ音の連打消音を修正** (`HitSoundPlayer.cs`): PlayOneShot 積み重ねをやめ、専用 8 ボイスの
  ラウンドロビンプールに (最古ボイスを止めて再生 = 最新の打鍵が必ず鳴る+同時発音上限で音割れ防止。
  priority 64 で間引き耐性も向上)
- ※上記 3 件はエディタ終了のためコンパイル/テスト未検証 — Unity 再起動後に EditMode 一式を要実行

## 12. リーダーボード実装 (07-30 深夜、K 指示「設計書を作りつつ両側実装」)

- **設計書**: サーバー `pvpharmonics-server/docs/leaderboard_design.md` / クライアント
  `docs/design_doc/leaderboard_client.md` (いずれも仮案 v0.1 — K レビュー待ち。実装は設計書どおり)
- **サーバー (Go)**: migration 011 `chart_best_scores` (一意キーは (user,song,diff,season) —
  譜面差し替えの版違い二重表示を防ぐため spec から変更 §7-1)、
  `GET /leaderboard/{song}/{diff}` + `/personal-best` (phase7 DTO spec §2-a/2-b どおり)、
  `POST /score/validate` にベスト取り込み (検証済みサーバー再計算値のみ UPSERT、
  レスポンスに best_updated / personal_best_score 追加)。**go test ./... 全緑** (leaderboard ハンドラ
  単体テスト 6 件追加。DB 一気通貫は integration タグで K 環境実行)
- **クライアント (Unity)**: `LeaderboardApi` + DTO 群、`ScoreSubmitService` (ソロプレイ終了時に
  リプレイを score/validate へ非同期提出 — 条件: ソロ・非オート・ログイン済・サーバー配信譜面。
  ワイド判定時は claim 省略)、`ServerSongLibrary.TryGetChartId`、SongRanking 画面の COMING SOON を
  実 API 結線 (上位20件+YOUR RANK、自分行ハイライト)。提出条件は Domain `ScoreSubmitPolicy` に
  切り出し EditMode テスト 6 件追加

## 13. Velopack 自動更新の導入 (07-30、K 指示書に基づく)

テスター 4 名への配り直しを不要にする自動更新基盤。**追加ファイルはすべて新規パスで、
既存の ui-overhaul 未コミット変更への編集はゼロ** (コミット分離可能)。

- **事前検証**: Mono バックエンド + .NET Standard 2.1 に対し Velopack 1.2.0 の
  netstandard2.0 ビルドが適合 (公式リポ確認 — 非対応は IL2CPP のみ)。API は nupkg 同梱の
  XML ドキュメントから実シグネチャを確認して使用 (憶測なし)
- **組み込み** `Assets/_Project/Scripts/Boot/VelopackAutoUpdater.cs`: 起動最初期に
  VelopackApp.Run (フック処理)、シーンロード後に更新チェック → DL (「Updating... n%」表示) →
  適用 → 自動再起動。エディタ/生ビルド/オフライン/タイムアウト 10 秒/例外はすべて
  静かにスキップして通常起動。フィード URL は FeedUrl 定数 1 箇所
- **dll** `Assets/Plugins/Velopack/`: Velopack 1.2.0 + Microsoft.Win32.Registry +
  System.Security.AccessControl + System.Security.Principal.Windows (netstandard2.0)
- **パッケージング** `release-tools/pack_release.bat`: vpk download (デルタ基準) → vpk pack。
  出力 releases\ を ConoHa へ scp するだけ
- **配信設計** `docs/deployment/caddy_updates.md`: 非公開ランダムパス
  `/updates-x7q2mkv9tr4w/` + browse off の静的配信 (basic_auth は自動更新が通らないため不採用 —
  選定理由をドキュメントに明記)。適用は K が手動
- **リリース手順書** `docs/deployment/velopack_release.md`: 毎回の 3 ステップ
  (ビルド → bat → scp)、初回配布 (Setup.exe)、ロールバック、ローカル検証手順
- 検証: コンパイル緑・EditMode **300/300 緑**・エディタプレイでフィードチェックが
  スキップされることを確認。実ビルドでの更新一気通貫は手順書 §4 に従い K が実施

### 未対応 (要相談)
- 「FX レーンの判定線の色の光り方とノーツの光り方を変えたい」 — どう変えたいか (色/強さ/点滅など) の
  具体イメージ待ち (縁色の統一で解消していれば close)

## 14. PVP ソークテストと発見バグの修正 (07-30 深夜、K 指示「Bot と連続対戦して全画面検査・問題修正を長時間ループ」)

ローカルソーク環境 (Docker の postgres/redis + 修正版サーバーを WSL で直接起動、
Unity クライアントは PlayerPrefs で localhost:8080 へ切替、専用アカウント soak-kani / BOT-1)
を構築し、自走ドライバ (`Dev/PvpSoakDriver.cs`) と対戦 Bot (`tools/playtest_bot.py`) で
フルマッチを連続実行。以下の**本物のバグ**を検出し修正した。

### サーバー側 (pvpharmonics-server — 別リポジトリ、要デプロイ)
1. **演奏中タイムアウト誤判定** (`internal/match/timeout.go`, `handler_pick.go`, `handler_match_state.go`, `handler_submit.go`)
   - 旧: Play フェーズ突入時に期限 30 秒 (`TimeoutPlaySong`) → 曲は 2 分以上あるため、
     片方が送信済みの状態で誰かが `/state` を叩くと演奏中の相手が不戦敗になっていた。
     **本番で kani が不戦敗を連発した根本原因**。
   - 新: 突入時は `TimeoutPlayMax` (10 分、曲全体を覆う上限)。先行スコア送信時に
     `TimeoutPlaySong` (30 秒) へ張り替え = 本来の「演奏終了後の送信猶予」。
   - 併せて Song3 突入時の期限なし (両者放置で永久残留) も TimeoutPlayMax に統一。
2. **WS ready タイマーが REST-READY を認識しない** (`internal/transport/ws/hub.go`)
   - 旧: 接続 20 秒後に WS レベルの ready フラグだけを見て不戦敗判定。READY は REST
     フォールバックでも成立するため、その場合**演奏中でも 20 秒で不戦敗**になっていた。
   - 新: タイマー発火時に正本フェーズを確認し、pre_match を抜けていれば何もしない。
3. **対戦 Bot のリアルタイム化** (`tools/playtest_bot.py`)
   - 旧: 1 曲を約 3 秒で送信 + 毎秒 `/state` ポーリング → 対人間で必ず不戦敗を誘発。
   - 新: 相手の WS 進捗を受信し「ほぼ完走 (≥98.5%) or 進捗停止 12 秒」まで待って送信。
     Bot 同士 (進捗なし 20 秒) は従来の高速モード。run_match タイムアウト 300→1500 秒。

### クライアント側 (unity1)
4. **WS 進捗の単位バグ** (`Game/GamePlayController.cs`): `percent_x1000` (0–1000‰) に
   `percent×100000` を送信していた (100 倍過大)。相手側の進捗判定・表示を破壊。→ ×1000。
5. **終了済みマッチの待機画面スタック** (`UI/Pvp/PvpPrematchController.cs`):
   `finished` を「開始」と誤認してドラフト遷移を試み固まっていた → 終端フェーズ
   (finished/aborted/cancelled) はロビーへ復帰。
6. **SceneRouter 遷移中の GoTo 取りこぼしでマッチフロー停止** (系統的問題):
   遷移中の `GoTo` は警告だけ出して無視される仕様のため、`_leaving=true` を先に立てる
   マッチフロー各画面 (Prematch start 直後 / リザルト NEXT / ドラフト→GamePlay /
   Matchmaking 復帰 / PvpResultBridge) が取りこぼし時に**復帰不能**になっていた
   (ソークで実測: リザルトから song2 へ進めず 150 秒停滞→不戦敗、等)。
   → 該当遷移をすべて既存の恒久対策 API `GoToWhenIdle` (遷移終了を待って実行) に切替。
7. **存在しない難易度をドラフトで選択可能** (`UI/Pvp/PvpDraftController.cs`):
   サーバーに譜面がない難易度 (例: hard 未登録曲) を選べてしまい、play フェーズで
   譜面取得に失敗して永久スタック→不戦敗 (サーバーも文字列しか検証しないため素通り)。
   → 難易度ボタンをサーバー譜面の有無で interactable 制御 (後出し/ブラインドは確定曲基準)。
8. **曲終了時 NRE でリザルト遷移ごと死ぬ** (`Game/NotePool.cs`, `GamePlayController.cs`):
   ホットリロードで非シリアライズの `_pools` が消えると `Release` が NRE →
   async void の `TriggerResultAsync` が丸ごと中断しスコア送信不能。
   → NotePool を null 防御 (+Awake 再構築)、終了時クリーンアップを try/catch で分離。
9. **SQLite ファイナライザによるエディタクラッシュ** (`Save/RepositoryService.cs`):
   ドメインアンロード時に `PreparedSqlLiteInsertCommand.Finalize` がネイティブクラッシュ。
   → OnDestroy で両リポジトリの `CloseAsync()` + `SQLiteAsyncConnection.ResetPool()`。

### ソーク基盤 (開発専用、ビルドに影響なし)
- `Dev/PvpSoakDriver.cs` (新規): エディタ+`PlayerPrefs SoakTest=1` の二重ガードで起動する
  自走ドライバ。タイトル→ロビー→キュー→READY→ランダムドラフト→オートプレイ→
  リザルトのスコアパリティ検査 (`[SoakParity]`)→次試合をループ。Bootstrap 停滞や
  シーン停滞 (150 秒) の自己回復、`Application.runInBackground=true` 強制
  (エディタ非フォーカス時のプレイヤーループ凍結対策) を含む。
- `UI/Pvp/PvpDraftController.cs`: PVP オートプレイは `Application.isEditor && SoakTest==1`
  のときのみ (ビルドでは常に無効 = チート防止)。

### 検証 (ローカルソーク)
- 修正後のフルマッチで確認済み: READY 同期 / ドラフト UI (曲プール・難易度・カウントダウン) /
  オートプレイ演奏 (VS バー・相手ライブスコア・セクターダイヤ) / SONG リザルト
  (pt 表示・セクターダイヤ・累計・難易度傾斜) のスクリーンショット取得。
- 注意: オートプレイはリプレイを保存しないため PVP 送信は直接スコア経路
  (replay=なし) になり、サーバー再判定パリティはソークでは通らない。
  エンジンパリティ自体はゴールデンベクタ+実機リプレイ再判定で別途検証済み。

### 本番への影響・要対応
- サーバー修正 1・2 は**本番デプロイ必須** (現行本番は演奏中不戦敗が発生し得る)。
- ソーク中に本番 kani のレートが不戦敗で 1428→約 1309 まで低下 (バグの実害。
  シーズンリセットまたは手動補正は K 判断)。
- ローカル検証用アカウント soak-player@example.com / claude-verify@example.com は削除可。

## 15. コンフィグ数値のクリック直接入力 (07-31、K 指示)

- `UI/Common/ClickToEditValue.cs` (新規): 数値ラベルをクリックするとその場に入力欄を
  動的生成して重ねる汎用コンポーネント。Enter/フォーカス喪失で確定、ESC でキャンセル。
  半角数字・小数点・マイナスのみ受理 (全角数字・記号は半角へ自動変換、他は拒否)。
  確定値はスライダーへ書き戻す方式のため、保存・ラベル更新・即時反映の経路は従来と同一。
  範囲外の値は既存の min/max に自動クランプ。
- 適用箇所: ゲームプレイタブ (ハイスピード 0.5〜20.0 / レーン長 25〜200% /
  判定・表示オフセット ms / 背景エフェクト %)、オーディオタブ (マスター/楽曲/効果音 音量 %)
- `ConfigController`: 入力中は画面キー操作 (ESC/Tab/Q/E/F5/Space/矢印) を停止する
  ガードを追加 (`ClickToEditValue.IsEditing`)。ESC は「入力キャンセル」として消費され
  画面退出には流れない
- 検証: EditMode 300/300 緑。実機クリック操作は K 確認待ち

## コミット時の注意 (K 判断)

1. **スクリーンショット**: `Assets/Screenshots/verify_*.png`, `ui_check_*.png` 等 198 枚は検証用の一時産物。コミット不要なら削除するか `.gitignore` に `Assets/Screenshots/` を追加 (既存の fx_*.png 等も含め要否判断)
2. **未使用テクスチャ**: `Art/Playfield/` の約 25 枚 (lane_*/judgeline_*/note_* 等) は初期方式の名残で未参照。整理するならコミット前に
3. **Domain 未修正の既知事項**: `Domain/Pvp/MatchPool.cs` が PVP フォールバックで sample_* を参照 (Domain 聖域のため未修正。sample 削除により該当フォールバック時は音源欠落)
4. **audio.wav の増減**: StreamingAssets の楽曲音源は容量が大きい。LFS 運用でなければ song_* の wav (13 曲分) のサイズに注意
5. サーバー側リポジトリ (`pvpharmonics-server`) の変更は**別リポジトリ**のため別途コミット:
   `tools/sync_songs_to_unity.py` (snake/camel 両対応 + meta.json キー単位マージ)、
   `tools/chart-admin.html` (⬇ Unity 同期機能 — ブラウザからビルド/プロジェクトへ全曲配置、
   meta.json はサーバーに無いキーをローカルから保持し audioOffsetMs を保護)、`.gitignore`

## 検証状況

- EditMode テスト **293/293 緑** (全変更の各段階でゲート維持。§9 追加修正後の 07-29 にも再実行して緑を確認)
- PlayMode 実機確認: プレイフィールド描画・ノーツ/小節線同期・壁クリップ (LL=1.0/0.5)・Config ナビゲーション・キー設定表示・曲リスト、いずれもスクリーンショット付きで確認済み
- §9 は現状コンパイル+EditMode のみ。実機での要確認: FX 小節線の見た目 (2.4px)・コンフィグの行ナビ/Space 循環/パレット配置・sectors 未設定曲の HUD セクター表示
