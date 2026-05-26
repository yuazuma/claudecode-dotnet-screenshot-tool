# 思考メモ 01: 要件分析と環境構築計画

## 日時
2026-05-26

## 要件定義書からの把握内容

### アプリケーション概要
- **種別**: Windows用タスクトレイ常駐型 自動スクリーンショット撮影ツール
- **目的**: 業務における作業証跡（エビデンス）の自動取得
- **ユーザー**: 技術的専門知識を持たない一般PC利用者

### 技術スタック
| 分類 | 採用技術 |
|------|---------|
| 言語 | C# 12以上 |
| ランタイム | .NET 8 (LTS) |
| UIフレームワーク | WPF |
| タスクトレイ | System.Windows.Forms.NotifyIcon |
| グローバルフック | Win32 API (WH_MOUSE_LL / WH_KEYBOARD_LL) |
| ウィンドウ切替検知 | Win32 API: SetWinEventHook |
| 画面キャプチャ | Graphics.CopyFromScreen / BitBlt |
| 設定保存 | System.Text.Json |
| ロギング | Serilog |
| 配布 | PublishSingleFile + SelfContained |

### 主要機能 (10機能)
1. **F-01**: マウスイベント連動撮影
2. **F-02**: キーボードイベント連動撮影
3. **F-03**: 画面変更検知撮影（アクティブウィンドウ切替 + 差分検知）
4. **F-04**: マルチモニタ対応
5. **F-05**: ファイル保存管理（PNG/JPEG/WebP、命名規則）
6. **F-06**: メタデータ記録（テキストログ、画像オーバーレイ、JSON/CSV）
7. **F-07**: プライバシー保護（パスワード欄マスキング、除外アプリ）
8. **F-08**: タスクトレイ常駐
9. **F-09**: 設定機能（タブ型UI）
10. **F-10**: 自動起動（レジストリ登録）

### 主要モジュール構成
| モジュール | 責務 |
|-----------|------|
| App | エントリポイント、二重起動防止、トレイアイコン管理 |
| HookService | マウス・キーボード・ウィンドウ切替イベント検知 |
| CaptureService | スクリーンキャプチャ処理（全モニタ対応） |
| DiffDetector | 画面差分検知（縮小画像比較） |
| TriggerOrchestrator | トリガー調停・クールダウン・除外判定 |
| MaskingService | UIAutomationによるパスワード欄マスキング |
| FileStorage | 画像ファイル保存・命名規則適用 |
| MetadataLogger | テキスト/JSON/CSV形式メタデータ出力 |
| ConfigStore | 設定の読み込み・保存・変更通知 |
| SettingsView | 設定画面 (WPF) |
| Notifier | 撮影時フィードバック（アイコン点滅・トースト・カウンター） |

### 開発フェーズ
| フェーズ | 対象 |
|---------|------|
| Phase 1 | コア機能（タスクトレイ、手動撮影、PNG保存、設定基盤） |
| Phase 2 | 自動撮影（グローバルフック、ウィンドウ切替） |
| Phase 3 | 画面差分検知 |
| Phase 4 | マルチモニタ・形式拡張 |
| Phase 5 | プライバシー機能 |
| Phase 6 | メタデータ・仕上げ |
| Phase 7 | 配布（自己完結型） |

## 環境構築の方針

### プロジェクト構造
```
AutoScreenshot/
├── src/
│   └── AutoScreenshot/
│       ├── AutoScreenshot.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── Services/
│       │   ├── HookService.cs
│       │   ├── CaptureService.cs
│       │   ├── DiffDetector.cs
│       │   ├── TriggerOrchestrator.cs
│       │   ├── MaskingService.cs
│       │   ├── FileStorage.cs
│       │   ├── MetadataLogger.cs
│       │   ├── ConfigStore.cs
│       │   └── Notifier.cs
│       ├── Views/
│       │   ├── SettingsWindow.xaml
│       │   └── SettingsWindow.xaml.cs
│       ├── Models/
│       │   ├── AppConfig.cs
│       │   └── TriggerEvent.cs
│       └── Resources/
│           └── (アイコン等)
├── documents/
├── thinkings/
├── requirements/
│   └── 要件定義書.docx
└── README.txt
```

### 必要なNuGetパッケージ
- `Serilog` - ロギング
- `Serilog.Sinks.File` - ファイル出力
- `Serilog.Sinks.Console` - デバッグ用コンソール出力

### 参照追加が必要なもの
- `UseWindowsForms=true` - NotifyIconのため
- `UIAutomationClient` - パスワード欄マスキング
- `UIAutomationTypes` - UIAutomation型定義

## 初期化手順
1. `dotnet new wpf` でWPFプロジェクト作成
2. .csprojに必要な設定追加（UseWindowsForms, NuGet参照）
3. App.xamlをタスクトレイ常駐用に修正（StartupUri削除）
4. 各モジュールのスケルトンコード作成
5. ビルド確認
