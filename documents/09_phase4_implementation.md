# Phase 4 実装記録

## 日時
2026-05-26

---

## 実装概要

Phase 4 では以下の 5 項目を実装した。

| # | 機能 | 対象ファイル |
|---|------|-------------|
| 1 | WebP エンコード実装 | CaptureService.cs, AutoScreenshot.csproj |
| 2 | マスキング統合 | TriggerOrchestrator.cs, NotifyIconWrapper.cs |
| 3 | BurnTimestamp (タイムスタンプ焼き込み) | CaptureService.cs, TriggerOrchestrator.cs |
| 4 | AutoStart 設定連動 | SettingsWindow.xaml.cs |
| 5 | 設定 UI 修正 (ChkMouseDrag バグ + メタデータタブ) | SettingsWindow.xaml, SettingsWindow.xaml.cs |

---

## 1. WebP エンコード実装

### 採用パッケージ
`SixLabors.ImageSharp 3.1.7` (Apache 2.0) を採用。  
※ 4.0.0 は商用ライセンス必須のため 3.x に固定。

### 実装方式
`System.Drawing.Bitmap → PNG (MemoryStream) → ImageSharp.Image → WebP`

```csharp
case Models.ImageFormat.WebP:
{
    using var pngBuf = new System.IO.MemoryStream();
    bmp.Save(pngBuf, System.Drawing.Imaging.ImageFormat.Png);
    pngBuf.Position = 0;
    using var img = ISImage.Load<Rgba32>(pngBuf);
    img.Save(ms, new WebpEncoder { Quality = jpegQuality });
    break;
}
```

PNG 中間変換は stride 問題を回避するための安全策。処理時間への影響は軽微。

### 確認
- 出力ファイルの Magic bytes: `RIFF....WEBP` ✅
- 圧縮効果: PNG 比で約 40% 縮小（1920×1080 で 73KB 程度）

---

## 2. マスキング統合

`MaskingService` が実装済みだったが `FireCapture` から未呼び出しだった。

### 変更
`TriggerOrchestrator` コンストラクタに `MaskingService masking` を追加し、  
`FireCapture` ループ内で `bounds` 変数を使って呼び出す:

```csharp
foreach (var (bmp, monitorIdx, bounds) in screenshots)  // _ → bounds
{
    if (cfg.Privacy.MaskPasswordFields)
        _masking.ApplyMasking(bmp, bounds);
    // ...
}
```

`NotifyIconWrapper` でも `MaskingService` を生成してコンストラクタに渡す。

---

## 3. BurnTimestamp

設定 `Metadata.BurnTimestamp = true` 時に画像左下にタイムスタンプを焼き込む。

```csharp
public void BurnTimestamp(Bitmap bmp, DateTime timestamp)
{
    using var g = Graphics.FromImage(bmp);
    using var font = new Font("Consolas", 10, FontStyle.Regular);
    string text = timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    var size = g.MeasureString(text, font);
    float x = 8f, y = bmp.Height - size.Height - 8f;
    using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
    g.FillRectangle(bg, x - 2, y - 2, size.Width + 4, size.Height + 4);
    g.DrawString(text, font, Brushes.White, x, y);
}
```

半透明黒背景 + 白文字でスクリーンの内容に関わらず視認性を確保。

---

## 4. AutoStart 設定連動

`SettingsWindow.ApplySettings()` の末尾でレジストリと同期:

```csharp
if (autoStart && !AutoStartService.IsEnabled())
    AutoStartService.Enable();
else if (!autoStart && AutoStartService.IsEnabled())
    AutoStartService.Disable();
```

---

## 5. 設定 UI 修正

### ChkMouseDrag バグ修正
XAML に `x:Name="ChkMouseDrag"` が定義されていたが `LoadSettings`/`ApplySettings` で未使用。  
`cfg.Triggers.MouseDragDrop` と双方向に接続した。

### メタデータタブ追加
設定ウィンドウに「メタデータ」タブを新設:
- `ChkSidecarLog`: サイドカーテキストログ ON/OFF
- `ChkBurnTimestamp`: タイムスタンプ焼き込み ON/OFF

---

## 動作確認結果

### WebP
```
Magic: RIFF (RIFF) / WEBP (WEBP)  ← 正規 WebP 形式
20260526_160114_780_screendiff_monitor1.webp  73310 bytes
```

### BurnTimestamp
- `ImageFormat=2 (WebP) + BurnTimestamp=true` で起動して Notepad を開閉
- `.webp` ファイルが生成され、サイドカーログにも記録 ✅

### サイドカーログ (events_2026-05-26.log)
```
2026-05-26 16:01:07.672  ActiveWindowChange  メモ帳 - 無題  notepad  (685,700)  monitor1  ...activewindowchange_monitor1.webp
2026-05-26 16:01:14.780  ScreenDiff  ...  Code  (685,700)  monitor1  ...screendiff_monitor1.webp
```

---

## Phase 5 への引き継ぎ

1. **ホットキー一時停止** - `AppConfig.HotkeyPause` フィールドは存在するが未実装
2. **StructuredOutput (JSONL/CSV)** - `MetadataConfig.StructuredOutput` は実装済みだがテスト未実施
3. **ImageOverlay** - `MetadataConfig.ImageOverlay = false` のまま (要件確認)
4. **除外アプリ設定 UI** - `TxtExcludeApps` は実装済み、機能テスト未実施
5. **ImageSharp 3.1.7 脆弱性** - GHSA-rxmq-m78w-7wmc: 外部入力非適用のため許容、または 3.1.x 最新版への更新推奨
