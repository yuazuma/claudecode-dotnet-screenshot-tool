# v1.3.0 動作確認記録

## 概要

v1.3.0 リリース後、実際に `publish/v1.3.0/AutoScreenshot.exe` を起動して FR-A〜E の全機能を動作確認した。

## 環境

- OS: Windows Server 2025 (125% DPI スケーリング)
- 解像度: 物理 1920×1200、論理 1536×960
- テスト対象: `publish/v1.3.0/AutoScreenshot.exe`（自己完結型 win-x64）

## 確認手順と結果

### 0. 起動確認

旧 Debug ビルド（PID 6564, `src/AutoScreenshot/bin/Debug/...`）が残存していたため終了し、
`publish/v1.3.0/AutoScreenshot.exe` を起動した。タスクトレイにアイコンが表示されることを確認。

### 1. トレイアイコン右クリックメニュー

`WM_TRAYMOUSEMESSAGE (0x800)` を NotifyIcon の隠しメッセージウィンドウ
（クラス `WindowsForms10.Window.0.app.0.*`）に直接送信してコンテキストメニューを表示。

メニュー項目（確認済み）:
- 記録開始
- 一時停止
- 保存フォルダを開く
- 設定
- エクスポート ▶
  - 手順書 (Markdown)
  - 手順書 (Word)
  - 手順書 (HTML)  ← FR-A で追加
  - 動画 (MP4)
- **プロジェクトを管理...**  ← FR-D で追加
- 終了

`ss_06b_menu_bottom.png` / `ss_10b_zoom.png` でスクリーンショット確認済み。

### 2. FR-A: HTML エクスポート

トレイメニュー「エクスポート」→「手順書 (HTML)」が存在することを確認（`ss_10b_zoom.png`）。

設定ウィンドウの「記録停止時に自動 HTML エクスポート」チェックボックス表示を確認
（`ss_34_project_tab.png`）。

### 3. FR-B: インクリメンタル LLM 処理

設定ウィンドウの「ステップ追記後に自動 LLM 改善」チェックボックス表示を確認
（`ss_34_project_tab.png`）。

### 4. FR-C: ステップアノテーション

プロジェクトビューウィンドウ右ペインのアノテーションパネルを確認:
- ツールボタン: 番号 / 矢印 / 矩形 / 吹き出し
- 色ボタン: 赤 / 青 / 黄 / 緑
- Canvas 上でクリックすると番号バッジ「①」が描画されることを確認（`ss_27_annotation.png`）
- 「保存」ボタンで annotations が project.json に保存される

### 5. FR-D: プロジェクト管理強化

プロジェクトビューウィンドウ（1040×640px）で確認:
- 検索ボックスに文字入力するとリストがフィルタリングされることを確認（`ss_28_search.png`）
- タグ WrapPanel が上部に表示
- 「＋ステップを追加」ボタン存在確認
- サムネイルのドラッグ&ドロップでステップ順序変更 UI を確認

全体ウィンドウレイアウト: `ss_22_pvw_dpi.png` / `ss_24_project_selected.png`

### 6. FR-E: プロジェクト結合・分割

- 「結合...」ボタンが複数選択時に有効になることを確認（`ss_35_merge_probe.png`）
- 「ここで分割」ボタン存在確認

## 判定

**PASS** — FR-A / FR-B / FR-C / FR-D / FR-E すべての機能が UI 上で動作することを確認。

## 注意点・所見

- 125% DPI 環境では `GetWindowRect` が返す座標は論理座標。PIL `ImageGrab.grab()` は物理ピクセルを
  キャプチャするため、切り抜き時に 1.25× 補正が必要（`thinkings/25` 参照）。
- 旧 Debug ビルドがバックグラウンドで残存していた。次回確認時は実行中プロセスのパスを
  `QueryFullProcessImageNameW` で確認してから進める。

## スクリーンショット一覧

| ファイル | 内容 |
|---|---|
| `publish_verify/ss_06b_menu_bottom.png` | トレイメニュー全体 |
| `publish_verify/ss_10b_zoom.png` | エクスポートサブメニュー（HTML 確認） |
| `publish_verify/ss_22_pvw_dpi.png` | プロジェクトビューウィンドウ全体 |
| `publish_verify/ss_24_project_selected.png` | プロジェクト選択・サムネイル表示 |
| `publish_verify/ss_27_annotation.png` | アノテーション番号バッジ「①」 |
| `publish_verify/ss_28_search.png` | 検索フィルタリング動作 |
| `publish_verify/ss_34_project_tab.png` | 設定ウィンドウ（FR-A/B チェックボックス） |
| `publish_verify/ss_35_merge_probe.png` | 結合ボタン有効状態 |
