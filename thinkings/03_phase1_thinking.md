# 思考メモ 03: Phase 1 実装の思考過程

## 日時
2026-05-26

---

## アイコン実装の設計判断

### 選択肢の検討

| 案 | メリット | デメリット |
|----|---------|-----------|
| 外部 .ico ファイルを埋め込みリソースとして追加 | 視認性が高い本格的なデザイン可能 | デザインツールが必要、バイナリ管理が必要 |
| **コード生成 (IconFactory)** | 外部ファイル不要、差分が読みやすい | シンプルな形状に限定 |
| System.Drawing.Icon の既存システムアイコン流用 | 実装ゼロ | 外観がアプリ固有でない |

→ **コード生成を採用**: Phase 7 (配布) のタイミングで本格的なデザインに差し替えやすい

### HICON メモリリークの理解

`Bitmap.GetHicon()` は Win32 HICON を作成する。これを:
- `Icon.FromHandle(hIcon)` に渡すと「借り物ハンドル」の Icon が作られる。
  → ハンドルを後で `DestroyIcon()` しても Icon 自体はすでに使えなくなる
- `Icon.FromHandle(hIcon).Clone()` すると、内部でビット列をコピーした独立した Icon が得られる。
  → その後 `DestroyIcon(hIcon)` でHICONを解放しても Clone した Icon は使い続けられる

正しい手順:
```csharp
IntPtr hIcon = bmp.GetHicon();
var icon = (Icon)Icon.FromHandle(hIcon).Clone();  // コピー
DestroyIcon(hIcon);  // 元のHICONを解放
// icon はここ以降も安全に使用可能
```

---

## 動作確認の分析

### なぜ差分検知が「変化モニタ数=0」になるか

Phase 1 確認時に `[DBG] 差分チェック: 変化モニタ数=0` が連続した。原因を分析:

1. **閾値が高い (30%)**: 30% 以上のピクセルが変化しないと発火しない設計。VS Code の UI 更新や端末出力は局所的変化なので閾値に達しない。→ **仕様通りの動作**

2. **縮小画像の解像度**: 320×180 に縮小して比較するため、局所変化の影響がさらに薄まる。→ **設計通り、意図的な負荷軽減**

3. **差分計算の閾値**: `dr + dg + db > 30` で1ピクセルを「変化あり」と判定。ノイズ除去のために十分な値。

→ 差分検知は正しく動いている。スクリーン全体が大きく変わるウィンドウ切替後などで発火する。

### なぜグローバルフックがターミナルから発火しないか

`WH_MOUSE_LL` / `WH_KEYBOARD_LL` は OS 全体のイベントを捕捉するが、Git Bash (MinGW) のターミナル入力は以下の経路を通る可能性がある:

- POSIX レイヤーで処理 → Win32 のキーボードメッセージになる前に処理される場合がある
- ターミナルマルチプレクサのような中間レイヤーが入る場合もある

→ Phase 2 では実際の Windows デスクトップ操作で動作確認する必要がある

### System.Threading.Timer の例外の扱い

`System.Threading.Timer` のコールバックで uncaught exception が発生した場合:
- .NET Framework: プロセスがクラッシュする
- **.NET Core/.NET 5+**: 例外は **飲み込まれる** → タイマーは止まらないがエラーが無音になる

対策: コールバック全体を try-catch でラップして `Log.Error` で記録する。

---

## Phase 1 で作成・修正したファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Resources/IconFactory.cs` | 新規作成: コード生成アイコン |
| `Services/NotifyIconWrapper.cs` | IconFactory 使用に変更、一時停止時アイコン切り替え、Dispose 整理 |
| `Services/TriggerOrchestrator.cs` | `OnDiffTimer` に try-catch + Debug ログ追加 |
| `Services/FileStorage.cs` | 保存ログを Debug → Information に昇格 |

---

## Phase 2 への引き継ぎ事項

1. **ドラッグ&ドロップ検知**: HookService に `WM_LBUTTONDOWN/UP` の時間差判定を追加する
2. **ホイールアイドル**: HookService でホイールの最終イベントから N ms 後に撮影するタイマーを実装する
3. **実機テスト**: デスクトップ上でマウス操作・キー入力・ウィンドウ切替を行い、PNG が保存されることを確認する
4. **AutoStart パス問題**: 現在 Debug パスが登録されている。配布ビルド後に上書き登録が必要
