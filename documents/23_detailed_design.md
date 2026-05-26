# 詳細設計書 作成

## 日時
2026-05-27

## 作成ファイル

`design/詳細設計書.md` (2232 行)

## 内容

全ソースファイル（21 ファイル）を読み込み、実装レベルの詳細を記述した詳細設計書を作成した。

### 構成

1. システム全体概要（技術スタック・名前空間構成）
2. 起動・終了フロー（Mutex → ロギング → フック → セッション）
3. クラス詳細設計（21 クラス）
   - App, NotifyIconWrapper, HookService, TriggerOrchestrator
   - CaptureService, FileStorage, DiffDetector, MaskingService
   - MetadataLogger, Notifier, ConfigStore, DpapiHelper
   - AutoStartService, HotkeyService, UiaService, OcrService
   - ManualSessionRecorder, RuleBasedDescriber, LlmService
   - MarkdownManualWriter, DocxManualWriter
4. データモデル詳細（TriggerEvent, ManualSession/ManualStep, AppConfig 系）
5. Win32 API / P/Invoke 詳細
6. 処理フロー詳細（マウス撮影・キーボード撮影・差分検知・手順書生成）
7. 設定・永続化詳細（config.json・ログ・サイドカーログ）
8. エラーハンドリング方針（全サービスのフォールバック方針）
9. セキュリティ設計詳細（DPAPI・LLM 制約）
10. NuGet パッケージ詳細
