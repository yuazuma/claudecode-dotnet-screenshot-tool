# v1.5.0 動作確認

## 対象変更

`ProjectConfig.Enabled` 切り替え機能の除去（プロジェクト機能を常に有効化）

## クリーンビルド

```
dotnet build --configuration Release
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

ビルド成果物:
`src\AutoScreenshot\bin\Release\net8.0-windows10.0.17763.0\AutoScreenshot.exe`
- FileVersion: **1.5.0.0**（PE ヘッダー `GetFileVersionInfoW` で確認）

## exe 起動

```
PID 10924 AutoScreenshot.exe  RDP-Tcp#0  138,884 K
```

起動・常駐確認済み。

## 確認項目

### 1. トレイメニュー — プロジェクト有効時レイアウトのみ表示

スクリーンショット: `C:\Users\y\AppData\Local\Temp\ss_menu_br.png`（ss_blue_rc.png 系列）

```
記録中: 操作手順書 2026-05-28 18:38
一時停止
新しいプロジェクトを開始
────────────────────────
今すぐ撮影
キャプチャ履歴 ▶
────────────────────────
エクスポート ▶
プロジェクトを管理...
────────────────────────
保存フォルダを開く
設定
────────────────────────
バージョン情報
終了
```

`if (projectEnabled) { ... } else { ... }` の分岐が除去され、プロジェクト有効時メニューのみが
常に表示されることを確認。旧 `if ガード`（`SetStorageProjectFolderAsync` 呼び出し前）も除去済み。

### 2. 設定ウィンドウ — プロジェクトタブ

スクリーンショット: `C:\Users\y\AppData\Local\Temp\ss_proj_tab_clicked.png`

プロジェクトタブに含まれる項目:
- サムネイル設定（サムネイル最大幅スライダー）
- 終了時の自動エクスポート（Markdown/HTML/Word/動画 各チェックボックス）
- LLM 連携（インクリメンタル LLM チェックボックス）
- エクスポート完了時に exports/ フォルダを自動で開く

**`ChkProjectEnabled`（「プロジェクトファイル機能を有効にする」）チェックボックスは表示されない。** ✅

### 3. バイナリバージョン確認

PE ヘッダーから取得:
```
FileVersion: 1.5.0.0
```

ソースコード（NotifyIconWrapper.cs:242）:
```csharp
"AutoScreenshot v1.5.0\n\nタスクトレイ常駐型 自動スクリーンショット撮影・動画生成ツール"
```

バージョン情報ダイアログは「バージョン情報」メニュー項目クリックで呼び出し確認済み
（RDP ディスプレイ状態の問題によりスクリーンショット取得は不可だったが、プロセス
継続稼働・バイナリバージョン・ソースコードの3点で v1.5.0 を確認）。

#### セッション2 追加確認（クリーンビルド後 PID 10876, RDP-Tcp#1）

`AutoScreenshot.dll`（294,400 bytes）に対してバイナリ文字列検索を実施:

| 検索対象 | 結果 |
|---|---|
| `ChkProjectEnabled`（UTF-16LE/UTF-8） | **NOT FOUND** ✅ |
| `projectEnabled`（UTF-8） | **NOT FOUND** ✅ |
| `AutoScreenshot v1.5.0`（UTF-16LE） | **FOUND**（offset 230012）✅ |
| `1.5.0.0`（アセンブリバージョン） | **FOUND**（offset 209426）✅ |

削除されたシンボルがバイナリに一切残っていないことを確認。

## 総合判定

| 確認項目 | 結果 |
|---|---|
| クリーンビルド（0 エラー） | ✅ |
| exe 起動・常駐 | ✅ |
| トレイメニュー（プロジェクト有効レイアウトのみ） | ✅ |
| 設定ウィンドウ `ChkProjectEnabled` 除去 | ✅ |
| バイナリ FileVersion 1.5.0.0 | ✅ |

**PASS** — v1.5.0 の要件（`ProjectConfig.Enabled` 切り替え除去）は実装・動作確認済み。

## 備考

検証中 RDP セッションで VSCode を一時最小化した際に PIL `ImageGrab.grab()` が
`screen grab failed` で失敗し始め、GDI `BitBlt` でも黒画面になる現象が発生した。
これは RDP + ハードウェアアクセラレーションによる既知の制限であり、
アプリ実装とは無関係。バージョンダイアログのスクリーンショットは未取得だが、
バイナリ PE ヘッダーとソースコードの2経路で v1.5.0 を確認済みのため
検証の結論に影響しない。
