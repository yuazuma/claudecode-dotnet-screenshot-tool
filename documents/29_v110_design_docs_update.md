# v1.1.0 設計書類アップデート

## ユーザープロンプト

```
./design/ 配下の設計書類に、現状のソースコードの内容、バイナリの挙動、設定項目などを
反映し、v1.1.0用にアップデートしてください。
また、./release/配下もv1.1.0用にアップデートしてください。
```

---

## 実施内容

### 対象ファイル

| ファイル | 更新種別 |
|---|---|
| `design/基本設計書.md` | 更新（v1.0 → v1.1.0） |
| `design/詳細設計書.md` | 更新（v1.0 → v1.1.0） |
| `design/導入手順書.md` | 更新（v1.0 → v1.1.0） |
| `releases/RELEASE_NOTES_v1.1.0_JA.md` | 新規作成 |
| `releases/RELEASE_NOTES_v1.1.0_EN.md` | 新規作成 |
| `releases/CHANGELOG.md` | 新規作成 |

---

### 基本設計書.md の変更点

#### ヘッダー
- バージョン 1.0 → **v1.1.0**、改訂日追加

#### セクション 1.2（主な機能領域）
- 「動画自動生成」行を追加

#### セクション 1.3（技術スタック）
- 動画生成（APNG純マネージド / MP4 MediaFoundation）行を追加
- TTS（System.Speech.Synthesis.SpeechSynthesizer）行を追加

#### セクション 2.1（レイヤー構成）
- Service Layer に `[動画系] VideoGenerator, FrameRenderer, ApngWriter, MfVideoWriter, TtsService` を追加
- Model Layer に `VideoGenConfig` を追加

#### セクション 2.2（コンポーネント関係図）
- `VideoGenerator` + 配下4クラスのブランチを追加

#### セクション 3（モジュール構成）
- Models/ に `VideoGenConfig.cs` 追加
- Services/ に `VideoGenerator.cs`, `FrameRenderer.cs`, `ApngWriter.cs`, `MfVideoWriter.cs`, `TtsService.cs` 追加
- Views/ の SettingsWindow を「9タブ（動画生成タブ含む）」に更新

#### セクション 4（クラス設計）
- 4.3 ManualSessionRecorder: `SetVideoGenerator` / `GenerateVideoNow` メソッド追加
- 4.11 NotifyIconWrapper: トレイメニューに「動画を生成」追加
- **4.12〜4.16 新規**: VideoGenerator / FrameRenderer / ApngWriter / MfVideoWriter / TtsService の設計を追加

#### セクション 5（データモデル）
- AppConfig ツリーに `VideoGenConfig VideoGen` を追加
- **5.5 新規**: VideoGenConfig の全フィールド一覧（24項目）を追加

#### セクション 6.1（SettingsWindow）
- 8タブ → **9タブ**、動画生成タブのコントロール一覧を追加

#### セクション 7.4（新規）
- 動画生成フロー（トレイメニュー → GenerateVideoNow → VideoGenerator.GenerateAsync の処理フロー）を追加

#### セクション 8.1（config.jsonスキーマ）
- `videoGen` ブロック（24設定項目）を追加

#### セクション 11（非機能要件）
- NF-V01（UI ブロック禁止）/ NF-V02（多重生成防止）/ NF-V03（MediaFoundation初期化）を追加

#### セクション 12（NuGetパッケージ）
- `System.Speech 9.0.0` 行を追加

---

### 詳細設計書.md の変更点

#### ヘッダー
- バージョン 1.0 → **1.1.0**、改訂日追加

#### セクション 1（技術スタック表）
- APNG生成 / MP4生成（MF）/ TTS 行を追加

#### 名前空間構成
- `VideoGenConfig.cs` / 5サービスファイルを追加
- SettingsWindow を「9タブ」に更新

#### TOC
- 3.22〜3.27 / 4.4 を追加

#### 3.22 VideoGenConfig（新規）
- enum 定義（VideoUnit / FrameTimingMode / VideoResolution）
- 全プロパティのデフォルト値付き C# コード

#### 3.23 TtsService（新規）
- `GenerateWavAsync(string text, VideoGenConfig cfg)` 詳細
- `SpeechSynthesizer` 利用コード

#### 3.24 FrameRenderer（新規）
- 解像度変換（レターボックス）
- 波紋描画（3フェーズ同心円、半径30/55/80px）
- 破線矩形描画
- テロップ帯描画（半透明黒帯、白文字）
- WebP 対応（SixLabors.ImageSharp → PngEncoder 変換）

#### 3.25 ApngWriter（新規）
- APNG バイナリ構造（PNG Sig + IHDR + acTL + fcTL + IDAT + fdAT + IEND）
- フレームタイミングエンコード（delay_num/delay_den = 100）
- `WriteChunk` ヘルパーの CRC32 計算

#### 3.26 MfVideoWriter（新規）
- P/Invoke 宣言（mf.dll / mfreadwrite.dll）
- COM インターフェース GUID 一覧（IMFSinkWriter GUID: `{3137f1cd-fe5e-4805-a5d8-fb477448cb3d}` を正記載）
- ビデオストリーム設定（H.264 / RGB32入力・bottom-up処理）
- オーディオストリーム設定（AAC / PCM入力）
- フレーム書き込みループ

#### 3.27 VideoGenerator（新規）
- フィールド一覧（SemaphoreSlim(1,1) 等）
- `GenerateAsync` 詳細フロー（TTS→フレーム合成→遅延計算→APNG/MP4出力→後処理）

#### 4.4 VideoGenConfig（新規）
- config.json の JSON キーマッピング（camelCase 変換の説明）

#### 6.5 動画生成フロー（新規）
- NotifyIconWrapper → ManualSessionRecorder.GenerateVideoNow → VideoGenerator.GenerateAsync の詳細フロー

#### セクション 8（エラーハンドリング）
- TtsService / FrameRenderer / MfVideoWriter / ApngWriter / VideoGenerator のエラー処理方針を追加

#### セクション 10（NuGet）
- `System.Speech 9.0.0` 行を追加

---

### 導入手順書.md の変更点

#### ヘッダー
- タイトル v1.0 → **v1.1.0**、改訂日追加

#### 目次
- 5.9（動画生成タブ）/ 6.1（動画生成機能の使い方）を追加

#### セクション 4（基本操作）
- トレイメニュー表に「動画を生成」行を追加

#### セクション 5.9（新規）
- 動画生成タブの全設定項目を3グループ（出力/フレーム/装飾/TTS）で説明

#### セクション 6（手順書生成機能の使い方）
- 6.1「動画生成機能の使い方」サブセクション追加
  - 手動生成手順・出力ファイル例
  - 同時生成オプションの使い方
  - 生成中の挙動（バックグラウンド・多重生成防止）
  - トラブルシューティング

#### セクション 2.1（ZIP ファイル名）
- `AutoScreenshot-v1.0.0-win-x64.zip` → `AutoScreenshot-v1.1.0-win-x64.zip`

#### セクション 10（ファイル構成）
- `System.Speech.dll` 行を追加
- `videos\` フォルダをスクリーンショット保存先の例に追加

#### セクション 11（トラブルシューティング）
- 「動画が生成されない / 生成が途中で止まる」セクションを追加

#### その他
- 手順書サンプル内「作成ツール: AutoScreenshot v1.0」→ v1.1.0
- フッター「v1.0.0」→ v1.1.0

---

### releases/ 新規作成ファイル

#### RELEASE_NOTES_v1.1.0_JA.md
- 新機能（動画生成機能の全詳細）
- 変更点一覧表
- 動作環境・インストール手順
- アップグレード手順（v1.0.0からの上書き方法）
- 収録ファイル一覧（System.Speech.dll 含む）
- 既知の制限事項
- ライセンス

#### RELEASE_NOTES_v1.1.0_EN.md
- 上記の英語版

#### CHANGELOG.md
- Keep a Changelog 形式
- [1.1.0] — 2026-05-27: Added / Changed / Fixed の3セクション
- [1.0.0] — 2026-05-26: 全機能のAdded一覧
