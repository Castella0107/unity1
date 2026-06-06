# 画面遷移図（全画面・操作仕様つき）

> 正本フロー（PVP対戦）: `C:\Users\CaSte\OneDrive\デスクトップ\フロー.png`（ユーザー提供）
> 配色規約: **自分 = シアン `#2BD9E6`（左） / 相手 = レッド `#F24D6B`（右）**、基調は DJMAX 風
> 最終更新: 2026-06-01 — ソロ系を含む全画面・操作（ESC / ショートカット / ゲームパッド）を追記

このドキュメントは「画面遷移」と「各画面の操作（キーボード / ESC / ゲームパッド / ボタン）」を一体で定義する。
凡例: **(済)** = 現状コードに実装済み / **(新)** = 本仕様で新規に追加する操作。

---

## 0. 全体方針（横断ルール）

| 項目 | ルール |
|---|---|
| **ESC** | 原則「1画面戻る」。**離脱が相手に影響する画面（マッチング中・ドラフト中・対戦中）では確認ダイアログを挟む**。ルート画面（Title）は終了確認。 |
| **決定 / 前進** | **Space** に統一（Play / READY / NEXT / LOCK IN / START MATCH 等）。一部画面のみ例外（下表参照）。 |
| **ショートカット表示** | 各画面の**下部に操作説明を常時表示**する。 |
| **対象環境** | **PC（キーボード＋マウス）＋ ゲームパッド**。 |
| **ゲームパッド共通** | D-pad / 左スティック = 選択、**A = 決定（Space 相当）**、**B = 戻る（ESC 相当）**、**LB / RB = タブ・カテゴリ切替**。<br>**A/B の配置（Xbox 配置 ⇄ 任天堂配置）は Config で選択可**。 |
| **PVP 中の離脱** | マッチ成立後（Prematch 以降）の離脱は**不戦敗**。各画面 ESC は「辞退確認ダイアログ」。対戦プレイ中のみ ESC は**長押し6秒でリタイア**。 |

> Space 統一の例外: **Result**（Space=リトライ）、**PVPMatchEnd**（Space=リマッチ）、**ポーズメニュー**（Enter=決定のまま）。

---

## 1. 全体フロー（Mermaid）

> PNG版: `docs/pvp/screen_flow.png`（`screen_flow.mmd` から `mermaid-cli` で生成。再生成は下記コマンド）
>
> ```pwsh
> npx -y @mermaid-js/mermaid-cli -i docs/pvp/screen_flow.mmd -o docs/pvp/screen_flow.png -b white -s 2 -p docs/pvp/.puppeteer.json
> ```

![画面遷移図](screen_flow.png)

```mermaid
flowchart TD
    Boot["Bootstrap → _Persistent ロード"]
    Title["Title<br/>(メインメニュー / カルーセル)"]

    %% ── ソロ系 ──
    SongSel["SongSelect<br/>(選曲・難易度・速度)"]
    PlayS["GamePlay (ソロ)<br/>(ESC=ポーズ)"]
    Result["Result<br/>(スコア / リトライ)"]
    Hist["History<br/>(戦績・リプレイ)"]
    Conf["Config<br/>(7タブ設定)"]
    Replay["GamePlay (リプレイ再生)"]

    %% ── PVP系 ──
    Lobby["PVPLobby<br/>(オンラインロビー)"]
    MM["Matchmaking<br/>(検索 → MATCH FOUND)"]
    Pre["PVPPrematch<br/>(導入 READY)"]
    Pick["PVPSongPick<br/>(PICK / 20曲ブラインド)"]
    Ban["PVPBanPhase<br/>(BAN → MATCH LINEUP)"]

    subgraph Loop["各曲ループ ×3"]
        direction TB
        Setup["PVPSongSetup<br/>(難易度＋プレイ設定)"]
        PlayP["GamePlay (PVP)<br/>(ESC長押し6秒=リタイア)"]
        SongRes["PVPSongResult<br/>(セクター勝敗＋累計)"]
        Setup -->|Space=READY| PlayP -->|完走| SongRes
        SongRes -->|Space=NEXT| Setup
    end

    End["PVPMatchEnd<br/>(WIN/LOSE/DRAW + レート変動)"]

    Boot --> Title

    Title -->|FREE PLAY| SongSel
    Title -->|ONLINE| Lobby
    Title -->|CONFIG| Conf
    Title -->|HISTORY| Hist
    Title -->|EXIT / ESC| Exit(("終了"))

    SongSel -->|Space=Play| PlayS --> Result
    Result -->|Space=リトライ| PlayS
    Result -->|Enter=選曲へ| SongSel
    Result -->|ESC=タイトル| Title
    SongSel -.->|ESC| Title

    Hist -->|Submit=リプレイ| Replay --> Hist
    Hist -.->|ESC| Title
    Conf -.->|ESC| Title

    Lobby -->|Space=START| MM
    MM -->|MATCH FOUND（自動）| Pre
    Pre -->|自動| Pick
    Pick -->|両者 PICK 確定| Ban
    Ban -->|Space=START MATCH| Loop
    Loop -->|3曲完走| End
    Loop -. "2曲目後に8pt到達" .-> End

    Lobby -.->|ESC| Title
    MM -.->|ESC=キャンセル確認| Lobby
    Pre -.->|ESC=辞退確認(不戦敗)| Title
    Pick -.->|ESC=辞退確認| Title
    Ban -.->|ESC=辞退確認| Title
    Setup -.->|ESC=辞退確認| Title

    End -->|Enter=TO LOBBY| Lobby
    End -->|Space=REMATCH| MM
    End -->|ESC/TO TITLE| Title

    classDef solo fill:#143,stroke:#2BD9E6,color:#fff;
    classDef pvp  fill:#413,stroke:#F7C740,color:#fff;
    class SongSel,PlayS,Result,Hist,Conf,Replay solo;
    class Lobby,MM,Pre,Pick,Ban,Setup,PlayP,SongRes,End pvp;
```

---

## 2. 画面別 操作表

### 2-1. 共通 / ソロ系

| 画面 | 移動・選択 | 決定 / 前進 | ESC | その他ショートカット | ボタン |
|---|---|---|---|---|---|
| **Title** | ←→ / A D：項目送り (済) | Space / Enter：決定 (済) | **終了確認ダイアログ** (新, 現状は即終了) | — | FREE PLAY / ONLINE / CONFIG / HISTORY / EXIT |
| **SongSelect** | ↑↓：曲選択 (済) / ←→：難易度 (済) | **Space** / Enter：Play (済) | Title へ戻る (済) | **HiSpeed = `[` / `]`（±0.5）(新)** / **Modifier 切替キー (新)** / F4：ソート (済) | 難易度4種 / Play / Back / HiSpeed / Offset / Modifier |
| **GamePlay (ソロ)** | (ポーズ時) ↑↓ / W S (済) | (ポーズ時) Enter (済) | **ポーズ ON/OFF** (済) | — | (ポーズ) Resume / Restart / Quit |
| **Result** | — | **Space＝リトライ (新)** | **タイトルへ (新)** | **Enter＝選曲へ (新)** ／ R・S・T は**廃止**、下部に操作説明を表示 | リトライ / 選曲へ / タイトル |
| **History** | ↑↓：行 (済) / ←→：曲カーソル(Ladder) (済) | Submit (Space/Enter)：リプレイ再生 (済) | Title へ戻る (済) | **Tab：Ladder/Free 切替 (新)** / **数字キー：難易度フィルター (新)** | Back / Ladder・Free / 難易度 / 各行 |
| **Config** | **←→（LB/RB）：タブ切替 (新)** / **↑↓：項目選択 (新)** | **←→ / Space：値変更 (新)** | Title へ戻る (済) | 7タブ：Audio / Devices / Display / Input / Game / Account / Data | Back / 各タブ / 各設定 |
| **リプレイ再生** | — | — | History へ戻る (済) | 1 / 2 / 4：再生速度 (済) | — |

> ポーズメニューは Space 統一の例外で **Enter 決定のまま**（現状維持）。

### 2-2. PVP系

| 画面 | 移動・選択 | 決定 / 前進 | ESC | 備考 |
|---|---|---|---|---|
| **PVPLobby** | — | **Space＝START (新)** | Title へ戻る（確認なし）(新) | **F5 は廃止 (新)**。戦績/レート/シーズン表示 |
| **Matchmaking** | — | （MATCH FOUND で**自動** Prematch へ）(新) | **検索キャンセル確認ダイアログ → Lobby (新)** | 検索中 SEARCHING…→ MATCH FOUND |
| **PVPPrematch** | — | （**自動**で PICK へ・READY 不要）(新) | **辞退確認ダイアログ（不戦敗）(新)** | 導入 READY 演出 |
| **PVPSongPick** | **矢印キー：5列グリッド カーソル移動 (新)** | **Space / Enter：LOCK IN (新)** | **辞退確認ダイアログ (新)** | 20曲ブラインド / 60秒タイマー（0で未選択ならランダム自動ロック） |
| **PVPBanPhase** | **矢印キー：3候補カーソル移動 (新)** | **Space / Enter：LOCK IN → 開示後も Space/Enter で START MATCH (新)** | **辞退確認ダイアログ (新)** | 残り1曲が3曲目 → MATCH LINEUP 発表 |
| **PVPSongSetup** | **←→：難易度 (新)** / **修飾キー＋←→：速度・オフセット (新)** | **Space / Enter：READY (新)** | **辞退確認ダイアログ (新)** | 相手の難易度は可視 |
| **GamePlay (PVP)** | — | — | **長押し6秒＝リタイア（不戦敗）(新)**。ポーズ不可 | PVP差分HUD（相手box / VSバー） |
| **PVPSongResult** | — | **Space / Enter：次へ（NEXT / FINAL）(新)** | **無効 (新)** | セクター S1..S5 勝敗 ◆/◇/— ＋累計（8ptで決着） |
| **PVPMatchEnd** | — | **Space＝REMATCH (新)** / **Enter＝TO LOBBY (新)** | **TO TITLE (新)** | ボタン: TO TITLE / REMATCH / TO LOBBY（SAVE REPLAY は当面なし） |

> LOCK IN・START・READY は **Space と Enter を共通**にして「ロック」と「次へ」をテンポ良く進められるようにする（ドラフト確定）。

---

## 3. ゲームパッド・マッピング（全画面共通）

| 入力 | 既定（Xbox 配置） | 任天堂配置（Config で切替） | 役割 |
|---|---|---|---|
| D-pad / 左スティック | — | — | 選択・カーソル移動（↑↓←→） |
| **A（下ボタン）** | 決定 | **右ボタン**＝決定 | Space 相当（決定 / 前進 / LOCK IN / READY） |
| **B（右ボタン）** | 戻る | **下ボタン**＝戻る | ESC 相当（戻る / キャンセル / 辞退確認の起動） |
| **LB / RB** | — | — | タブ・カテゴリ切替（Config タブ / History モード / 難易度） |

---

## 4. 早期決着（8pt クリンチ）

- 1試合 = 3曲 × 5セクター = **最大15pt**、**8pt = 過半数クリンチ**。
- **2曲目リザルト時点でどちらかが 8pt 到達** → 3曲目（PVPSongSetup・GamePlay・PVPSongResult）を**スキップ**して `PVPMatchEnd` へ直行。
- 3曲目リザルトでクリンチ発動時は黄色 "CLINCH!" 告知 + NEXT ボタンが "FINAL RESULT" 化。

---

## 5. ドラフトの内部遷移（PICK / BAN のブラインド状態機械）

各ドラフト画面は再入安全（サーバーが真実、毎回 `GET /draft` で復元）。

```
 Loading ─► Intro/Selecting ─► Submitting ─► Waiting(poll 1.2s) ─► Reveal ─► (Space/Enter で前進)
            ▲ 矢印カーソル選択   Space=LOCK IN   相手のロック待ち      ブラインド開示
            │                                                       (YOU/OPP タグ)
            └─ 60秒タイマー：0で未選択ならランダム自動ロック
```

サーバードラフト仕様: キュー成立→**曲なし**マッチ作成→ PICK（各自プール20から1曲）→両PICK完了で残プールから **3曲BAN候補抽選** → BAN（候補3から各自1曲）→ 残り1曲が3曲目（BANかぶり時は残2曲からrandom）。試合3曲 = `[PickA, PickB, 3曲目]`。

---

## 6. 横断（モーダル / トランジション）

- **辞退 / キャンセル確認ダイアログ**: 「辞退しますか？（不戦敗扱い）」「検索をやめますか？」等、画面に応じて文言可変。
- **エラー / 相手切断モーダル**: 文言可変（不戦勝 / 不戦敗 / 再接続中）。
  - クライアント側: `PVPMatchEnd` の中断表示を分類（MATCH INCOMPLETE / CONNECTION ERROR / MATCH ABORTED）（済）。
  - **不戦勝/不戦敗・再接続自動復帰はサーバーの forfeit/切断判定が必要で未対応（K 領域）**。
- **画面遷移トランジション**: `SceneRouter` が additive load + `_isTransitioning` ガードで直列化（多重 GoTo は握り潰し）。

---

## 7. シーン一覧と操作実装状況

| 区分 | シーン (SceneId) | 役割 | 操作実装 |
|----|------------------|------|------|
| 起動 | Bootstrap / _Persistent | 起動・常駐（SceneRouter） | 済 |
| ソロ | Title | メインメニュー（カルーセル）| キー済 / ESC=終了確認は要追加 |
| ソロ | SongSelect | 選曲・難易度・速度 | 基本キー済 / HiSpeed・Modifier キー要追加 |
| ソロ | GamePlay | プレイ（ポーズ）| 済 |
| ソロ | Result | スコア・リトライ | キー再設計要（Space/Enter/ESC、R/S/T廃止）|
| ソロ | History | 戦績・リプレイ | 基本キー済 / Tab・数字フィルター要追加 |
| ソロ | Config | 7タブ設定 | タブ←→化・項目キー操作要追加 |
| PVP | PVPLobby | オンラインロビー | Space化・F5廃止・ESC要追加 |
| PVP | Matchmaking | マッチング | ESC=キャンセル確認要追加 |
| PVP | PVPPrematch | 導入 READY | 自動進行・ESC辞退確認要追加 |
| PVP | PVPSongPick | PICK（20曲）| キーボード全面要追加 |
| PVP | PVPBanPhase | BAN + LINEUP | キーボード全面要追加 |
| PVP | PVPSongSetup | 難易度＋設定 | キーボード全面要追加 |
| PVP | GamePlay (PVP) | プレイ（差分HUD）| ESC長押しリタイア要追加 |
| PVP | PVPSongResult | 曲リザルト | Space/Enter前進・ESC無効要追加 |
| PVP | PVPMatchEnd | 対戦結果 / レート | ボタン3種・キー要追加 |

ビルド: PVP シーン再生成は `Tools/PVP/Build PVP Scenes`（`Editor/BuildPvpScenes.cs`）／ batch は `-executeMethod BuildPvpScenes.BuildAll`。

---

## 8. 実装ステータス（2026-06-01 コード実装）

本仕様の操作を実装した。入力ロジックはコントローラー内のコードのみで完結（新規シーン配線は原則不要）。

**新規共通部品**
- `Scripts/UI/Common/ConfirmDialog.cs` — 自己生成・OnGUI 確認ダイアログ。`IsOpen`（閉じたフレームも true）で裏画面の入力を抑止。
- `Scripts/UI/Common/ShortcutHintOverlay.cs` — 画面下部の操作説明バー（自己生成・OnGUI）。`SceneRouter` 遷移時に自動クリア、各画面 Start で再設定。
- `Scripts/Input/GamepadLayout.cs` — A/B 配置（Xbox ⇄ 任天堂）を PlayerPrefs で切替。`ConfirmPressed/BackPressed/Prev/NextTabPressed`。

**画面別（実装済）**
Title(ESC=終了確認) / SongSelect([ ]速度・M Modifier) / Result(Space=リトライ・Enter=選曲・ESC=タイトル、R/S/T廃止) / History(Tab・数字フィルター) / Config(←→/Tab/LB RB タブ・↑↓項目) / PVPLobby(Space/ESC・F5廃止) / Matchmaking(ESC=キャンセル確認→Lobby) / Prematch(自動進行・ESC辞退) / SongPick・BanPhase(矢印カーソル・Space/Enterロック&前進・ESC辞退) / SongSetup(←→難易度・修飾+←→設定・Space READY・ESC辞退) / GamePlay PVP(ESC長押し6秒リタイア) / SongResult(Space次へ・ESC無効) / MatchEnd(Space=Rematch・Enter=Lobby・ESC=Title)。

**要・追加作業（Unity 上で確認/配線）**
1. **ゲームパッド配置トグル**: `InputTabController._nintendoLayoutToggle`（任意参照）を Config の Input パネルに Toggle として配置・結線（ランタイム生成はレイアウト破壊回避のため避けた）。未配置でも既定 Xbox 配置で動作。
2. **Config の項目キー操作（↑↓）**: EventSystem + InputSystemUIInputModule の Selectable ナビゲーションに依存。Config シーンに UI モジュールと各 Selectable の navigation 設定が必要。タブ切替・ESC・マウスは UI モジュール非依存で動作。
3. **Result のボタン補助ラベル**: 旧 R/S/T 表記がシーンに残る場合は表示更新（機能は新キーに移行済、下部ヒントが正）。
4. 目視確認: OnGUI オーバーレイ（ヒント/ダイアログ）の表示位置と日本語フォント（`GUI.skin` 既定）で問題ないか。気になる場合は TMP 版へ差し替え。
