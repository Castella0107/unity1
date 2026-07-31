# Velopack 更新フィード用 Caddy 設定案

| 版 | 日付 | 内容 |
|---|---|---|
| 0.1 | 2026-07-30 | 初版 (Claude 案 — 適用は K が ConoHa 上で手動実施) |

> **本ドキュメントは案。** 実 Caddyfile は ConoHa VPS 上 (ホスト systemd、Docker 外) にあり、
> 反映・reload は K が行う。既存の air-chart / chart-admin ブロックと同系統の静的配信。

## 1. 設計 — 認証方式の選定

**「推測されにくい非公開パス」方式を採用する。**

- パス: `/updates-x7q2mkv9tr4w/` (ランダム 12 文字入り。クライアントの
  `VelopackAutoUpdater.FeedUrl` と `pack_release.bat` の FEED_URL に同じ値を埋め込み済み)
- 理由:
  - Velopack クライアントの HTTP 取得は素の GET のため、`basic_auth` を掛けると
    exe に認証情報を埋め込まない限り自動更新が通らない (埋め込むなら秘密パスと強度は同等)
  - 更新フィードの中身はゲーム本体 (テスターに配るもの) であり、漏洩時の実害は
    「ゲームが無料で落とせる」程度。テスター 4 名の運用ではパス秘匿で十分と判断
  - 変更もクライアント定数 1 箇所+Caddy 1 行で済む
- パスを変えたくなったら: `VelopackAutoUpdater.FeedUrl` / `pack_release.bat` の
  `FEED_URL` / 本ドキュメントの 3 箇所を同時に変更して新ビルドを配る

## 2. Caddyfile 追加ブロック案

既存サイトブロック (pvpharmonics.duckdns.org) 内に追加:

```caddyfile
    # ── Velopack 更新フィード (静的配信・非公開パス) ──
    handle_path /updates-x7q2mkv9tr4w/* {
        root * /home/kani/updates
        file_server {
            browse off          # ディレクトリ一覧は出さない
        }
    }
```

- `browse off` で一覧非表示 (ファイル名を知っている場合のみ取得可能)
- Velopack が取得するのは `releases.win.json` と `.nupkg` — いずれも Content-Type 不問

## 3. 適用手順 (K 実施)

```bash
ssh kani@160.251.231.181
mkdir -p ~/updates
sudo cp /etc/caddy/Caddyfile /etc/caddy/Caddyfile.bak.$(date +%Y%m%d)
sudo nano /etc/caddy/Caddyfile          # §2 のブロックを追加
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

## 4. 動作確認

```bash
# フィード JSON が取れること (リリースを 1 度 scp した後)
curl -s https://pvpharmonics.duckdns.org/updates-x7q2mkv9tr4w/releases.win.json | head -c 200

# パス無しでは 404 になること
curl -s -o /dev/null -w "%{http_code}\n" https://pvpharmonics.duckdns.org/updates/
```
