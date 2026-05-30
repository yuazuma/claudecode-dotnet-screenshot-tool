# v1.7.1 実装記録 — MP4 エクスポート・RDP キャプチャ・Markdown 画像修正

## ユーザープロンプト（抜粋）

```
AVI形式ではなくMP4形式で出力することが必須です。要件通りに実装してください。
画像ファイルにPrtScr押下時のようなスクリーンショットが保存されていない原因を調査してください。
RDPセッションでもスクリーンショット撮影できる実装を提案してください。
DWMコンポジター直接取得に変更したためか、スクリーンショット撮影時に黄色い枠がデスクトップに
表示されるようになってしまいました。この黄色い枠を表示しないように修正してください。
Markdown形式でエクスポートした際に、IMGタグで指定された画像ファイルのパスが誤っているため、
画像が表示されません。
{mdファイル名}_images/ サブフォルダを作成して全画像をコピーする際には、画像の横幅が1200pxを
超える場合には横幅がちょうど1200pxになるようにリサイズするようにしてください。
```

---

## 1. MP4 エクスポート問題の調査・修正

### 1.1 問題の経緯

| バージョン | 動作 |
|---|---|
| v1.7.0 当初 | `MfVideoWriter`（IMFSinkWriter H.264）→ 失敗 → `MjpegAviWriter`（AVI MJPEG）フォールバック |
| 要件 | AVI は不可、MP4（H.264）のみ許容 |

### 1.2 診断結果（Azure Windows Server 2025）

```
ERR  IMFSinkWriter.Finalize 失敗 HRESULT=0xC00D4A44
ERR  H.264 エンコーダー MFT が見つかりません（count=0）
```

- `MFTEnum(MFT_CATEGORY_VIDEO_ENCODER, MFT_ENUM_FLAG_SYNCMFT, ...)` が count=0 を返す
- Azure Server には H.264 MFT（Media Feature Pack）が未インストール
- IMFSinkWriter 内部の MPEG-4 マルチプレクサーも muxer 自体が Finalize で失敗

### 1.3 解決策: 3 段階フォールバック

**Step 2a: MfVideoWriter（既存）**
- IMFSinkWriter + H.264 → Azure Server では Finalize が 0xC00D4A44 で失敗

**Step 2b: H264Mp4Writer（新規）**
- H.264 MFT を `MFTEnum` で検索して直接 `CoCreateInstance`
- `ProcessInput` / `ProcessOutput` でエンコード
- ISO BMFF コンテナ（ftyp / mdat / moov / avc1 / avcC）を自前実装で書き出し
- Annex-B NAL → AVCC 変換を実装
- Azure Server: MFT 自体が存在しないため失敗

**Step 2c: FfmpegMp4Writer（新規）**
- フレームを JPEG として一時フォルダへ書き出し
- ffconcat 形式のリストファイルを生成（可変フレーム時間対応）
- `ffmpeg -f concat ... -c:v libx264 -pix_fmt yuv420p` でエンコード
- FFmpeg を `winget install Gyan.FFmpeg` でインストール後に成功

### 1.4 検証結果

```
FfmpegMp4Writer: MP4 書き出し完了 (12 フレーム) → 操作手順書_xxx.mp4
```

`ffprobe` 確認:
- `codec_name: h264`
- `codec_tag_string: avc1`
- `profile: High`
- `width: 1512, height: 948`

### 1.5 バグ修正: dur 除算エラー

`CalcFrameDuration()` は**秒単位**を返すが、Step 2b で `dur / 10_000_000.0` という
誤った除算（100ナノ秒単位への変換を想定したもの）が残っていた。

修正: `double durationSec = dur;`（除算を除去）

### 1.6 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Services/H264Mp4Writer.cs` | 新規 | H.264 MFT 直接呼び出し + 自前 ISO BMFF 書き出し |
| `Services/FfmpegMp4Writer.cs` | 新規 | FFmpeg subprocess によるMP4生成 |
| `Services/VideoGenerator.cs` | 修正 | Step 2c 追加、AVIフォールバック削除、dur バグ修正 |
| `Services/MjpegAviWriter.cs` | 修正 | VideoGenerator からの参照を削除（クラス自体は残存） |

---

## 2. RDP スクリーンキャプチャ問題の調査・修正

### 2.1 問題の診断

診断スクリプトによる結果:

```
RDP session: True
GDI CopyFromScreen:  size=1707x1067, distinctColors=23, avgBrightness=38, corner=24,24,24
BitBlt+CAPTUREBLT:   size=1707x1067, distinctColors=23, avgBrightness=38, corner=24,24,24
WGC IsSupported: True
```

- 両 GDI 方式とも RGB(24,24,24) = デスクトップ壁紙色を返す
- 実際のウィンドウ内容（WGC で distinct=42、タスクバー等のピクセルを確認）は取得できていない

### 2.2 根本原因

| レイヤー | 動作 |
|---|---|
| GDI `BitBlt` | GDI レイヤーを読む → RDP + DWM 環境では DWM コンポジット結果が GDI に露出しない |
| WGC API | DWM コンポジターの出力を直接読む → RDP でも正しく取得可能 |

### 2.3 WgcCapture 実装の要点

**スレッドモデルの問題と解決**:
- `Direct3D11CaptureFramePool.Create` → FrameArrived はディスパッチャースレッドで発火 → スレッドプール上では永遠に発火しない
- `Direct3D11CaptureFramePool.CreateFreeThreaded` → 任意スレッドから安全 → `Task.Run` 内での使用に適合

**SoftwareBitmap 変換**:
- `IDXGISurface1::GetDC()` は GDI 互換フラグが必要 → WGC フレームには付与されないため使用不可
- `IMemoryBufferByteAccess` の COM QI → CsWinRT ラッパー上では `Marshal.QueryInterface` が失敗
- → `SoftwareBitmap.CreateCopyFromSurfaceAsync` + `BitmapEncoder`（BMP）経由で変換（確実動作）

**HSTRING の扱い**:
- `[MarshalAs(UnmanagedType.HString)]` は .NET 8 P/Invoke で非対応
- → `WindowsCreateString` / `WindowsDeleteString` で手動管理

### 2.4 変更ファイル

| ファイル | 種別 | 内容 |
|---|---|---|
| `Services/WgcCapture.cs` | 新規 | Windows.Graphics.Capture API キャプチャ実装 |
| `Services/CaptureService.cs` | 修正 | RDP 検出 + WGC 優先フローの追加 |

### 2.5 黄色ボーダー非表示

`GraphicsCaptureSession.IsBorderRequired` は Windows 11 (Build 22000+) 追加。
TFM `windows10.0.17763.0` では型定義に存在しない。

対策: `IGraphicsCaptureSession3`（GUID: `BFEE7A93-...`）を `[ComImport, InterfaceIsIInspectable]`
で定義し、`(object)session is IGraphicsCaptureSession3 s3` でランタイム QI を試みる。
QI 失敗時は `IWinRTObject.NativeObject.ThisPtr` + vtable[7] 直接呼び出しでフォールバック。

---

## 3. Markdown 画像パス修正

### 3.1 問題

MD ファイルは `exports/` 配下、画像は `images/`（プロジェクトルート直下）にあるため、
パスが `../images/step_001.png` という親ディレクトリ参照になる。

VS Code・GitHub 等は `..` を含む画像パスをセキュリティ上拒否する。

### 3.2 修正

`{MDファイル名}_images/` を `exports/` 内に作成し、画像をコピーして参照。

```
Before: ../images/step_001.png
After:  20260530_143022_操作手順書_images/step_001.png
```

副次効果: アノテーション済み一時ファイルもここにコピーされるため、
一時ファイル削除後も参照が維持される。

### 3.3 1200px リサイズ

横幅 > 1200px の場合に `HighQualityBicubic` でアスペクト比維持リサイズ。

検証: 1512×948 → 1200×752（正しくリサイズ）

### 3.4 重複コピー防止

before/after が同一ファイルを参照する場合にキャッシュ（`Dictionary<string, string>`）で
重複コピーを防止。修正前は `step_001.png` と `step_001_1.png` が生成されていた。

---

## 4. コミット一覧

| コミット | 内容 |
|---|---|
| `cdc6140` | v1.7.1リリース — MP4 FFmpegフォールバック追加、AVIフォールバック廃止 |
| `d09b123` | fix: Markdown画像パスを_imagesサブフォルダ参照に変更 |
| `3021068` | feat: Markdown画像出力を1200px幅にリサイズ、重複コピー修正 |
| `a78d1f9` | fix: RDPセッションでのスクリーンショット取得にWindows.Graphics.Capture APIを使用 |
| `286f98b` | fix: WGCキャプチャ中の黄色ボーダーを非表示 |
