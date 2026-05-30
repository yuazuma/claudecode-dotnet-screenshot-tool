# v1.7.1 リリース作業記録

## ユーザープロンプト

```
v1.7.1での変更内容を ./requirements/追加要件_1.7.1.md に書き出してください。
./release/配下のバイナリ・ハッシュ・リリースノートをv1.7.1としてクリーンビルドして
アップデートしてください。
./design/ 配下、 ./documents/ 配下、 ./thinkings/ 配下のドキュメント類も更新してください。
```

---

## 実施内容

### 1. 要件定義書作成

| ファイル | 内容 |
|---|---|
| `requirements/追加要件_1.7.1.md` | FR-I1〜FR-I4（MP4/CLI/Markdown/RDP）+ BF-I1〜BF-I4（バグ修正）を記述 |

### 2. リリースビルド

- `dotnet publish -c Release -r win-x64 --self-contained true`
- 480 ファイル、81,862,065 bytes
- SHA-256: `68b96b11ef6ae2c161d36ff2fe7274e65a26e854d6ff94ca975b70adc4abece2`

### 3. リリースノート更新

| ファイル | 変更内容 |
|---|---|
| `releases/AutoScreenshot-v1.7.1-win-x64.zip` | 再ビルド（WGC・Markdown修正を含む） |
| `releases/AutoScreenshot-v1.7.1-win-x64.zip.sha256` | 新 SHA256 に更新 |
| `releases/RELEASE_NOTES_v1.7.1_JA.md` | RDP・Markdown・重複コピー・dur バグ修正を追記 |
| `releases/RELEASE_NOTES_v1.7.1_EN.md` | 同上（英語） |
| `releases/CHANGELOG.md` | [1.7.1] セクションを完成、日付を 2026-05-31 に更新 |

### 4. 設計書更新

| ファイル | 変更内容 |
|---|---|
| `design/基本設計書.md` | バージョン 1.7.1 に更新、技術スタック（WGC・MP4フォールバック）更新 |
| `design/詳細設計書.md` | バージョン 1.7.1 に更新、3.28〜3.31（H264Mp4Writer/FfmpegMp4Writer/WgcCapture/ExportCliRunner）追加、3.5 CaptureService・3.20 MarkdownManualWriter 更新 |
| `design/導入手順書.md` | バージョン 1.7.1 に更新、配布ファイル名・作成ツール文字列更新 |

### 5. 文書追加

| ファイル | 内容 |
|---|---|
| `documents/68_v171_mp4_rdp_implementation.md` | MP4エクスポート・RDPキャプチャ・Markdown画像修正の実装記録 |
| `documents/69_v171_release.md` | 本リリース作業記録（本ファイル） |
| `thinkings/39_v171実装の思考.md` | 各実装での設計判断・試行錯誤の記録 |

---

## v1.7.1 コミット一覧

| コミット | 内容 |
|---|---|
| `e454e6d` | v1.7.1 バージョン番号更新 |
| `cdc6140` | MP4 FFmpeg フォールバック追加、AVI フォールバック廃止 |
| `d09b123` | Markdown 画像パスを `_images/` サブフォルダ参照に変更 |
| `3021068` | Markdown 画像出力を 1200px 幅にリサイズ、重複コピー修正 |
| `a78d1f9` | RDP セッションでのスクリーンショット取得に WGC API を使用 |
| `286f98b` | WGC キャプチャ中の黄色ボーダーを非表示 |
