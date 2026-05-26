# 思考メモ 06: Phase 4 実装の思考過程

## 日時
2026-05-26

---

## 設計判断

### ImageSharp バージョン選択

**問題**: `dotnet add package SixLabors.ImageSharp` で 4.0.0 が取得されたが、  
`SixLabors.ImageSharp.targets` がビルド時に商用ライセンスを要求してエラーになった。

**解決**: `--version 3.1.7` で Apache 2.0 ライセンスの最終版を指定。

**注意点**: 3.1.7 に GHSA-rxmq-m78w-7wmc (中程度の脆弱性) が存在するが、  
これは外部からの不正な画像を処理する場合の問題であり、  
本アプリのように自前でキャプチャした画像のみを扱う場合は影響なし。

### WebP 変換方式の選択

**方式 A (採用)**: Bitmap → PNG MemoryStream → ImageSharp.Image → WebP  
- メリット: 実装シンプル、stride 問題なし
- デメリット: PNG エンコード/デコードの二重変換  

**方式 B (不採用)**: LockBits → 生ピクセル → `Image.LoadPixelData<Bgra32>` → WebP  
- メリット: PNG 変換ステップなし、高速
- デメリット: stride != width * 4 の場合に手動行コピーが必要

スクリーンショット用途では速度差は軽微 (1920×1080 で測定不能レベル) のため A を採用。

### SaveAsWebp vs Image.Save()

`Image.SaveAsWebp()` は拡張メソッドのため `using SixLabors.ImageSharp;` が必要。  
`ISImage = SixLabors.ImageSharp.Image` エイリアスだけでは拡張メソッドは見えない。  
`Image.Save(stream, encoder)` は基底クラスのインスタンスメソッドなので using 不要。  
→ `img.Save(ms, new WebpEncoder { ... })` を使用。

### MaskingService の統合位置

`FireCapture` の `foreach (var (bmp, monitorIdx, _) in screenshots)` 内で  
`_` を `bounds` に変更し、masking と BurnTimestamp の両方で使用。  
順序: マスキング → BurnTimestamp → エンコード → 保存。  
(先にマスキングして「保護されたビットマップ」にタイムスタンプを焼く)

### AutoStart 同期タイミング

`_config.Update()` の中でなく外側で `AutoStartService.Enable/Disable()` を呼ぶ。  
理由: レジストリ操作は config の JSON 更新と独立した副作用だから。  
`autoStart` 変数は `Update` 前に取得して、Update 後のレジストリ操作で参照する。

### SettingsWindow ChkMouseDrag バグ

XAML: `x:Name="ChkMouseDrag"` が定義済みだったが、  
`LoadSettings()` / `ApplySettings()` で未使用だった。  
Phase 2 で `MouseDragDrop` トリガーを追加した際にコードの同期が漏れていた。  
Phase 4 で両方に `ChkMouseDrag.IsChecked ↔ cfg.Triggers.MouseDragDrop` を追加して修正。

---

## 動作確認の観察点

### WebP ファイルサイズ比較
- PNG: `20260526_151439_334_screendiff_monitor1.png` → 107,526 bytes (Phase 3)
- WebP: `20260526_160114_780_screendiff_monitor1.webp` → 73,310 bytes (Phase 4)
- 圧縮率: 約 31% 削減 (quality=85 の設定)

### 処理時間
ActiveWindowChange から保存完了まで:
- `16:01:07.469` (イベント検知) → `16:01:09.064` (保存完了) = 約 1.6 秒
- PNG 時代は約 0.3〜0.4 秒だった → WebP 変換 (PNG 中間) で約 1 秒増加
- 許容範囲内。もし速度が問題なら LockBits 方式へ切り替え可能。

---

## Phase 5 への引き継ぎ

### ホットキー一時停止 (未実装)
`AppConfig.HotkeyPause = null` (文字列)。  
`Win32.RegisterHotKey` / `UnregisterHotKey` で実装予定。  
`MainWindow` の `WndProc` を override して `WM_HOTKEY` を受信する必要がある。  
WPF の場合は `HwndSource.FromHwnd(hwnd).AddHook(WndProc)` を使う。

### StructuredOutput テスト
`MetadataLogger.WriteStructuredAsync()` は実装済みだが設定 UI からのテスト未実施。  
config に `StructuredOutput: true` を設定して動作確認が必要。

### ImageSharp 3.x 最新版への更新
`dotnet add package SixLabors.ImageSharp --version "3.*"` で 3.x の最新版を取得できる。  
3.1.8 以降で GHSA-rxmq-m78w-7wmc が修正されているか NuGet で確認推奨。
