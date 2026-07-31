# PVPharmonics リリース手順書 (Velopack 自動更新)

| 版 | 日付 | 内容 |
|---|---|---|
| 0.1 | 2026-07-30 | 初版 (Claude 作成)。組み込み: `Scripts/Boot/VelopackAutoUpdater.cs` |

テスターは **一度 Setup.exe でインストールすれば、以後は起動するだけで自動更新** される。
K が毎回やるのは「ビルド → pack_release.bat → scp」の 3 ステップ。

---

## 0. 初回セットアップ (一度だけ)

1. **.NET 8 SDK** を Windows に導入 (未導入なら https://dotnet.microsoft.com/download )
2. **vpk CLI** を導入:
   ```
   dotnet tool update -g vpk
   ```
3. **Caddy 設定** を適用 ([caddy_updates.md](caddy_updates.md) §3) — `~/updates/` 作成含む

## 1. 毎回のリリース手順

### ① Unity ビルド
- File > Build Settings > Windows で通常どおりビルド (出力例: `C:\builds\pvp_win\PVP.exe`)

### ② パッケージ生成
```
cd C:\Users\mashi\projects\unity1\release-tools
pack_release.bat C:\builds\pvp_win 0.2.0
```
- バージョンは **semver で毎回上げる** (0.2.0 → 0.2.1 → …)。同じ番号を再利用しない
- 初回は「既存リリース取得失敗」と出るが無視して OK (full のみ生成される)
- 出力: `release-tools\releases\` に
  `PVPharmonics-<ver>-full.nupkg` / `-delta.nupkg` / `PVPharmonics-win-Setup.exe` /
  `releases.win.json` など一式

### ③ ConoHa へアップロード (WSL から)
```bash
scp -r /mnt/c/Users/mashi/projects/unity1/release-tools/releases/* kani@160.251.231.181:~/updates/
```

### ④ 改竄検知の基準を更新 (コマンドプロンプトで)
```
powershell -ExecutionPolicy Bypass -File C:\Users\mashi\projects\unity1\release-tools\security\update_baseline.ps1
```
- アップロードした一式のハッシュを「正」として記録し、直後にフィードを照合して
  アップロードの完全性も確認する (§6 参照)。**これを忘れると次の定期チェックが
  「改竄疑い」の誤報を出す** (リリース直後の誤報はこれの実行忘れ)

以上。**テスターは次回ゲーム起動時に自動で新版になる** (起動直後に「Updating... n%」表示 →
自動再起動)。オフラインや VPS 停止時は 10 秒でスキップして普通に起動する。

## 2. テスターへの初回配布 (一度だけ)

`release-tools\releases\PVPharmonics-win-Setup.exe` を渡してもらい、実行してインストール。
(Setup.exe は毎リリースで最新版に更新されるので、新規テスターにはいつでも最新の Setup.exe を渡せばよい)

> 既存の「生 zip」配布からの移行: 生 zip 版には Update.exe が無く自動更新できないため、
> 各テスターに一度だけ Setup.exe を実行してもらう (旧フォルダは手動削除で OK)。

## 3. ロールバック (不具合版を配ってしまったとき)

Velopack のフィードは `releases.win.json` が正。**旧版を新しいバージョン番号で再パッケージ**
するのが安全確実:

1. 直前の正常版のビルドフォルダで `pack_release.bat <旧ビルド> <新しい番号>` (例: 0.2.2)
2. scp — テスターは次回起動で「新番号の旧内容」へ自動更新される

(フィードから不具合版の nupkg と json 内エントリを手で消す方法もあるが、
既に更新済みのテスターが取り残されるため非推奨)

## 4. 検証手順 (ローカルで更新フローを試す)

サーバー不要でローカル確認できる (`UpdateManager` は URL の代わりにローカルパスを受ける):

1. バージョン A をビルド → `pack_release.bat <buildA> 0.9.0`
2. `releases\PVPharmonics-win-Setup.exe` を実行してインストール・起動 (0.9.0 が入る)
3. 何か変更してバージョン B をビルド → `pack_release.bat <buildB> 0.9.1`
4. **一時的な確認方法**: `releases\` を `~/updates/` へ scp する代わりに、
   ローカル HTTP で配信して確認する:
   ```
   cd release-tools\releases && python -m http.server 8765
   ```
   `VelopackAutoUpdater.FeedUrl` を `http://localhost:8765/` にしたビルドで 2→3 を行うと
   「0.9.0 起動 → Updating... → 0.9.1 で再起動」が確認できる
5. 確認後は FeedUrl を本番 URL に戻すこと

## 5. 組み込みの挙動 (実装済み仕様)

- `VelopackAutoUpdater.cs` (`Assets/_Project/Scripts/Boot/`):
  - 起動最初期に `VelopackApp.Build().Run()` (インストールフック処理)
  - シーンロード後に更新チェック (タイムアウト 10 秒)。新版があれば DL → 適用 → 自動再起動
  - **エディタ実行・vpk 経由でない生ビルド・オフライン時はすべて静かにスキップ**
- フィード URL は `VelopackAutoUpdater.FeedUrl` の 1 箇所で管理
- 追加 dll: `Assets/Plugins/Velopack/` (Velopack 1.2.0 netstandard2.0 + 依存 3 dll)

## 6. 供給網の改竄検知 (2026-07-31 導入)

**脅威モデル**: VPS に侵入され、更新フィードに悪意あるビルドを置かれると、
テスター全員が次回起動時にそれを実行してしまう (自動更新を持つソフト共通の宿命)。

**対策**: 検知はあえて **VPS の外 (K の PC)** に置く。侵入者は VPS 上の監視は
消せるが、外部からの照合は消せない。

- `release-tools/security/baseline.json` — リリース時に記録した「正」のハッシュ
- `check_feed_integrity.ps1` — フィードを取得して baseline と照合。
  不一致なら `ALERT.log` 記録+ポップアップ+ (設定時) Discord webhook 通知
  - 通常モード: `releases.win.json` のみ (クライアントが何をインストールするかを
    決めるファイル。Velopack は nupkg のハッシュをこの json と照合するため、
    ここが無傷ならパッケージ差し替え攻撃はクライアント側で拒否される)
  - `-Full`: `Setup.exe` 本体 (約 280MB) もダウンロードして照合
    (新規インストーラーは json 照合の保護外のため)
- `update_baseline.ps1` — リリース手順 ④。新リリースを「正」として記録
- **タスクスケジューラ登録済み**: 毎日 12:00 通常チェック / 毎週日曜 12:30 フル
- Discord 通知: `release-tools/security/discord_webhook.txt` に webhook URL を
  1 行置くと、改竄検知時に自動投稿される (任意)

**アラートが出たら**: 直前に自分でリリースして ④ を忘れただけなら誤報
(④ を実行すれば消える)。それ以外は本物の可能性 — テスターに起動を止めるよう
連絡し、VPS を調査する。
