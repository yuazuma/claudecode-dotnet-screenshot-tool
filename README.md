# AutoScreenshot

業務における作業証跡（エビデンス）を自動取得する Windows タスクトレイ常駐型スクリーンショットツールです。

## 動作環境

- Windows 10 / 11 (x64)
- .NET 8 ランタイム不要（自己完結型）

## インストール

1. `publish/AutoScreenshot.exe` を任意のフォルダにコピーする
2. ダブルクリックで起動
3. タスクトレイにカメラアイコンが表示されれば起動成功

> **初回起動時**: `%TEMP%` に .NET ランタイムのネイティブ DLL が展開されます（数秒かかる場合があります）。

## 自動起動の設定

設定ウィンドウ → 「一般」タブ → 「Windows ログオン時に自動起動する」にチェック

## 使い方

### 基本操作

| 操作 | 内容 |
|------|------|
| トレイアイコン右クリック | コンテキストメニューを開く |
| 「設定」 | 設定ウィンドウを開く |
| 「一時停止 / 再開」 | 撮影を一時的に停止・再開 |
| 「終了」 | アプリを終了 |

### 撮影トリガー（既定値）

| トリガー | 説明 |
|---------|------|
| マウス左/右/中クリック | クリック後にスクリーンショット |
| ドラッグ完了 | ドラッグ＆ドロップ完了時 |
| ホイール操作 | ホイール停止後 |
| キーボード入力 | 入力アイドル後（既定2秒） |
| アクティブウィンドウ切替 | フォーカス変更時 |
| 画面差分検知 | 一定割合以上の画面変化を検知したとき |

### 保存先

```
%USERPROFILE%\Pictures\AutoScreenshot\YYYY-MM-DD\
```

### ファイル命名規則（既定: 日付フォルダ + タイムスタンプ名）

```
20260526_180613_580_mouseleftclick_monitor1.png
```

## 設定項目

### 一般タブ
- **自動起動**: ログオン時にバックグラウンドで起動
- **ホットキー**: 一時停止／再開のキー（例: `Ctrl+F9`）

### 撮影トリガータブ
- 各トリガーの有効/無効
- クールダウン・アイドル待機時間の調整

### 保存タブ
- 保存先フォルダ
- 画像形式: PNG / JPEG / WebP
- 画質 (JPEG/WebP のみ)
- フォルダ命名規則

### プライバシータブ
- パスワード欄の自動マスキング
- 除外アプリ（プロセス名またはウィンドウタイトル、ワイルドカード対応）

### メタデータタブ
- サイドカーログ（撮影ごとに `.log` ファイルを生成）
- 撮影日時の画像焼き込み
- 構造化ログ（JSONL または CSV 形式）

### 通知タブ
- トレイアイコン点滅
- トースト通知
- 撮影枚数のツールチップ表示

## ログ

```
%APPDATA%\AutoScreenshot\logs\app-YYYYMMDD.log
```

## 構成ファイル

```
%APPDATA%\AutoScreenshot\config.json
```

設定ウィンドウから変更した内容は自動的に保存されます。

## ビルド方法（開発者向け）

### 前提条件
- .NET 8 SDK
- Windows 10/11 x64

### 開発ビルド

```powershell
dotnet build src/AutoScreenshot
```

### 配布ビルド（自己完結型単一 exe）

```powershell
dotnet publish src/AutoScreenshot -p:PublishProfile=win-x64-release
# 出力先: publish/AutoScreenshot.exe (~156 MB)
```

## ライセンス

- アプリ本体: プライベート
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
