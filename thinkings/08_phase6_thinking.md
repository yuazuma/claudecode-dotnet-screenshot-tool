# 思考メモ 08: Phase 6 実装の思考過程

## 日時
2026-05-26

---

## 設計判断

### Timer リーク修正のアプローチ

`System.Threading.Timer` は `IDisposable` だが、コールバック実行後に GC が回収するまで
生き続ける (ファイナライザで `Dispose` される)。通常の撮影頻度では問題ないが、
高頻度トリガー (画面差分検知 + マウスクリック連打) で積み上がる可能性がある。

修正方針:
- `_flashTimer` フィールドとして保持
- 新しいタイマーを作る前に `_flashTimer?.Dispose()` を呼ぶ
- これで「前のタイマーが 200ms コールバック実行前でも安全にキャンセルされる」
  ※ `Dispose()` はコールバックが進行中なら完了を待つわけではなく、
    次回コールバックのスケジュールをキャンセルする。
    `Timeout.Infinite` (one-shot) なので競合は問題にならない。

### JpegQuality UI の表示切替

`Visibility=Collapsed` (初期) → PNG 以外選択時に `Visible`。
`RdoFormat_Checked` ハンドラで `RdoPng.IsChecked == true` を確認。

なぜ `RdoPng.IsChecked` で全ラジオボタンをカバーできるか:
- GroupName="Format" で一つだけ選択される
- PNG が選択 → Collapsed
- JPEG or WebP が選択 → Visible
- ただし `Checked` イベントは「選択されたボタン」のみ発火するので、
  どのボタンが `Checked` になっても同じハンドラで判定できる

### PnlStructFmt の IsEnabled 制御

`Visibility.Collapsed` でなく `IsEnabled=false` を選んだ理由:
- ラジオボタンが見えることで「設定値が何か」をユーザーが把握できる
- 無効状態で表示することでグレーアウトによる「設定可能だが今は無効」を直感的に示す
- `Collapsed` にすると選択肢が消えてしまい「デフォルトで何が選ばれるか」が不明確になる

---

## StructuredOutput JSONL 動作確認

`events_YYYY-MM-DD.jsonl` はセッション横断で追記される設計。
同日に複数回アプリを起動しても同じファイルに追記されるため、
JSONL フォーマット (1行=1レコード) が適切。

JSONL の各フィールド:
- `timestamp`: ISO 8601 + タイムゾーンオフセット (DateTimeOffset)
- `trigger`: トリガー種別文字列
- `window_title`: アクティブウィンドウのタイトル
- `process`: プロセス名
- `cursor_x`, `cursor_y`: カーソル位置
- `monitor`: モニターインデックス (1-origin)
- `image_path`: 保存した画像の絶対パス

CSV モードは選択可能だが JSON Lines のほうが:
- 型情報が保持される (数値 vs 文字列)
- スキーマの追加が後方互換
- jq や Python pandas で直接処理可能

---

## Phase 7 への引き継ぎ

1. **PublishSingleFile**: `.csproj` に `<SelfContained>true</SelfContained>` + `<PublishSingleFile>true</PublishSingleFile>` コメント済み
   - `RuntimeIdentifier=win-x64` で Windows x64 専用 exe
   - ImageSharp + SixLabors.Core がすべてバンドルされる
   - 最終サイズ目安: ~50-80MB (trimming 無効の場合)

2. **ILLink (Trimming) の注意点**:
   - WinForms/WPF は trimming と相性が悪い
   - `[DynamicallyAccessedMembers]` アノテーションが必要な箇所が多い
   - まず trimming 無効で配布し、サイズ問題があれば検討する

3. **MSIX vs squirrel インストーラー**:
   - MSIX: Windows Store 対応、クリーンアンインストール、但しコード署名必要
   - squirrel: シンプル、自動更新あり、但し署名なしで SmartScreen 警告
   - 個人利用ツールなら単一 exe 配布 (コード署名なし) で十分
