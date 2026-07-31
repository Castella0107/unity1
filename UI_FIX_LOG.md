# UI_FIX_LOG — 安全修正の記録

各修正は EditMode テスト 293/293 緑を確認してから確定。

## 1. [SongSelect.unity] 旧フッター「ENTER > PLAY ESC < BACK」の二重表示
- 分類: ソース上意図している表示(ShortcutHintOverlay)と別に、置き換え済みの旧テキストが背後に残存・重なって表示 (§3.2 表示不一致/遺物)
- 対象: GameObject `KeyHint` (fileID 1921286903) — スクリプトからの外部参照 0 件を確認
- 変更: `m_IsActive: 1 → 0` (削除ではなく非活性化。復活可能)
- 備考: 旧文言は「ENTER > PLAY」だが現行ヒントは「Space: Play」で内容も古かった
- ゲート: EditMode 293/293 緑 ✓

## 2. [Title.unity] 右上 PlayerChip の「OFFLINE」と「RATING ----」が完全に重なって表示
- 分類: レイアウトずれ・収める方向が明白 (§3.2)
- 原因: 親 PlayerChip(360x64) 内で Name(上半区画アンカー)/Rating(下半区画アンカー) の
  sizeDelta.y (+28/+22) がアンカー区画に加算され両者が親中央へ溢れて重なっていた
- 変更: Name/Rating の RectTransform を anchoredPosition.y=0 / sizeDelta.y=0 に
  (それぞれ上半分32px・下半分32pxちょうどに収まる = 明らかに意図された2行積み)
- ゲート: EditMode 293/293 緑 ✓ (スクショ再確認は次バッチで実施)

## 3. [Config.unity + BuildConfigScene.cs] 下部ボタン (F9リセット/F5保存/ESC閉じる) がヒントバーに半分隠れる
- 分類: レイアウトはみ出し・収める方向が明白 (§3.2)
- 原因: フッターボタン y=38 (下端11) に対し、ShortcutHintOverlay の IMGUI バー(スクリーン30px)が下端を覆う
- 変更: 3ボタンの anchoredPosition.y 38 → 76 (シーン直接 + BuildConfigScene.cs の両方)
- 備考: IMGUI バーは固定スクリーンpxのため小さいエディタウィンドウでは依然わずかに近接する
  (フル解像度では完全にクリア)。バー自体のスケール対応は挙動変更になるため保留
- ゲート: EditMode 293/293 緑 ✓

## 4. [SongRanking.unity] 右上の曲名とアーティスト名が重なって表示
- 分類: レイアウトずれ・収める方向が明白 (§3.2) — Title PlayerChip (#2) と同一パターン
- 変更: SongTitle/SongArtist の RectTransform を anchoredPosition.y=0 / sizeDelta.y=0 に (上下半区画へ整列)
- ゲート: EditMode 293/293 緑 ✓

## 5. [Result.unity + ResultSceneBuilder.cs] 判定カウント数字の右端見切れ + 下部ボタンがヒントバーに沈む
- 分類: レイアウトはみ出し・収める方向が明白 (§3.2)
- 原因1: 判定行 (P+/P/Gr/Gd/M) の HorizontalLayoutGroup が childControlWidth=false のため
  Count の flexibleWidth が無効 → 数字の矩形がパネル右端の外へ延びて見切れ
- 変更1: 5行の HLG を m_ChildControlWidth=1 に (シーン + ビルダー)
- 原因2: ButtonRow y=30 が ShortcutHintOverlay バー (スクリーン30px) と重なる
- 変更2: ButtonRow y 30 → 72 (シーン + ビルダー)
- ゲート: EditMode 293/293 緑 ✓

## 6. [PVPLobby.unity + BuildPvpScenes.cs] 見出し見切れ / PRESS F5 誤記 / MENUボタン沈み
- 分類: レイアウトずれ(方向明白) + 表示と実装の不一致(一意に確定)
- 変更1: Kicker/LobbyTitle の x 180→530 — pivot(0.5) のまま x=180 に中心が置かれ
  「ONLINE · LOBBY」の左 340px が画面外だった (「BY」だけ見えていた)
- 変更2: STARTボタン内「PRESS  F5」→「PRESS  SPACE」— 実装 (spaceKey=OnStart) と
  ヒントバー (Space: START) の両方が Space で一致しており、F5 は実装に存在しない孤立誤記
- 変更3: BackButton(< MENU) y 20→64 (ヒントバー30pxとの重なり回避)
- いずれもシーン + BuildPvpScenes.cs の両方を更新
- ゲート: EditMode 293/293 緑 ✓

## 7. [ConfigController.cs] 2階層ナビゲーション (K指示書 2026-07-22)
- 不具合: ←→タブ切替が Colors タブでスライダーに奪われ右のタブへ進めない +
  ヒント「←→: タブ切替」と実装の不一致 (UI_FINDINGS 保留項の解消)
- 実装: タブレベル/項目レベルの2階層フォーカスを ConfigController に導入
  - タブレベル: ←→=タブ切替 (EventSystem 選択を空にして項目に奪わせない)、↓=項目レベルへ、
    L/R Shift・Tab/E/Q・パッド LB/RB は従来通り
  - 項目レベル: ↑↓=項目移動 (行背景をタブと同じ紫 #6B52D9(0.42,0.32,0.85,0.95) でハイライト)、
    ←→=値変更 (Slider=Slider.OnMove 経由 / Toggle=反転 / Dropdown=巡回)、最上項目で↑=タブへ
  - 項目 Selectable の Navigation は None (移動は ConfigController 管理。Slider の←→は Navigation 非依存で動作)
  - マウスクリックで項目を選ぶと項目レベルへ同期。ドロップダウン展開中は EventSystem に委譲
  - ヒントバーを階層別表示に更新 (§3.4)
- PlayMode 検証 (2026-07-22): ←→で Gameplay→…→Colors→Audio→Account 右端到達 ✓ /
  ↓降下+紫ハイライト ✓ / ↑↓移動 ✓ / ←→値変更 (Slider 224→255・Toggle・Dropdown、検証後復元) ✓ /
  最上↑でタブ復帰 ✓ / L/R Shift ✓
- スクショ: ui_check_config_nav_itemlevel.png / ui_check_config_nav_tablevel.png
  (Colors タブの薄紫行はピッカー対象行マーカー RowActive α0.35 = 既存機能で本件とは別物)
- ゲート: EditMode 293/293 緑 ✓
