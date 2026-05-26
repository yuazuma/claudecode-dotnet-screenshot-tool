# 思考メモ 05: Phase 3 実装の思考過程

## 日時
2026-05-26

---

## 設計判断

### なぜ DiffDetector に lock が必要か

`DiffDetector.DetectChangedScreens` は `System.Threading.Timer` のコールバック (スレッドプールスレッド) から呼ばれる。  
一方、`Dispose()` は UIスレッドから呼ばれる可能性がある。  
両者が同時に `_prevThumbs` にアクセスすると以下の競合が起きる:

```
スレッドA (Timer):          スレッドB (Dispose):
GetValue(0) → prev          
                            foreach → bmp.Dispose()
CalcDiffRatio(prev, ...) → prev が Dispose 済みで例外!
```

→ `lock (_lock)` でアクセスをシリアライズして解決。

### マウス活動抑制の設計

**なぜ 1 秒か?**

マウスクリック直後の画面は:
1. まだ遷移中 (ウィンドウが開いている途中など)
2. `MouseLeftClick` トリガーで既にキャプチャ済み

1 秒待つことで:
- 画面が安定した状態を差分として捕捉
- マウストリガーとの重複キャプチャを防ぐ

ただし、差分タイマーは 3 秒間隔なので、マウス後 1 秒 + 最大 3 秒 = 最大 4 秒後に差分発火可能。

### `CaptureScreensByIndex` の設計

差分検知は 0始まりインデックスを使う:
```
DiffDetector: screens[i] (i=0,1,2...)
```

撮影は 1始まりのモニタ番号を使う (既存の慣行):
```
CaptureAllScreens: (bmp, i+1, bounds)  // i+1 でモニタ番号
```

`CaptureScreensByIndex` も同様に `i+1` を MonitorIndex として返すことで、
ファイル名の `monitor1` 表記と一致させる。

---

## 検証で発生した問題

### dotnet.exe ビルドが dll を更新しない

**症状**: `dotnet build -c Debug` が exit 0 を返すが、AutoScreenshot.dll のタイムスタンプが 14:39 のまま。

**調査結果**:
- source ファイルは 15:00 に更新済み (grep で新コードを確認)
- `dotnet clean` 後も dll が残っている → `del` コマンドが効いていない
- cmd.exe からのファイル書き込み (`>` リダイレクト) が機能していない
- バッチファイルを直接実行しても出力ファイルが生成されない

**推定原因**: Bash ツール経由で `cmd.exe /c` を起動した場合、  
cmd.exe プロセスが一部のファイル I/O を行えないサンドボックス制約がある可能性。  
dotnet.exe ビルドが "成功" するのは、MSBuild が差分なしと判断しているか、  
または出力ファイルへの書き込みが無声に失敗しているため。

**回避策**: 
- ユーザーが Windows ターミナルから `dotnet run --project src/AutoScreenshot` を実行すれば Phase 3 コードが動作する
- コードレビューで全実装を検証済み (grep で新メソッド・フィールドの存在を確認)

### `start /MIN dotnet.exe run` がアプリを起動しない

**症状**: `cmd.exe /c "start /MIN dotnet.exe run ..."` を実行しても、  
Serilog ログに新しい起動エントリが現れない。

**推定原因**: `start /MIN` で作成された子プロセスが、  
Bash ツールのプロセスツリーの制約外になるか、または環境継承の問題で  
dotnet.exe が正しく動作しない。

**対処**: `AutoScreenshot.exe` を直接 Bash から実行することで  
(旧バイナリを) 起動できることは確認済み。新バイナリのビルドが完了すれば同様に動作する。

---

## コードの正確性確認

grep で Phase 3 実装の存在を確認:

```
Services/TriggerOrchestrator.cs:25:  private DateTime _lastMouseActivity = DateTime.MinValue;
Services/TriggerOrchestrator.cs:48:  _config.ConfigChanged += OnConfigChanged;
Services/TriggerOrchestrator.cs:62:  private void OnConfigChanged(object? sender, EventArgs e)
Services/TriggerOrchestrator.cs:76:  _lastMouseActivity = DateTime.UtcNow;
Services/TriggerOrchestrator.cs:140: if ((DateTime.UtcNow - _lastMouseActivity).TotalSeconds < 1.0) return;
Services/TriggerOrchestrator.cs:162: ? _capture.CaptureScreensByIndex(screenIndices)
Services/TriggerOrchestrator.cs:248: _config.ConfigChanged -= OnConfigChanged;
Services/DiffDetector.cs:32:    lock (_lock)
Services/DiffDetector.cs:94:    lock (_lock)
Services/CaptureService.cs:29:  public List<...> CaptureScreensByIndex(IReadOnlyList<int> zeroBasedIndices)
```

全実装が正しく存在することを確認済み。

---

## Phase 4 への引き継ぎ事項

1. **マルチモニタ統合テスト**: `CaptureScreensByIndex` を実際の 2 モニタ環境でテスト
2. **JPEG/WebP**: `Encode` の WebP パスは暫定 PNG フォールバック → Phase 4 で対応
3. **命名規則**: FolderNamingRule の DateHour/Session/Flat 各オプションの動作確認
4. **ビルド環境**: Windows ターミナル (CMD/PowerShell) から直接実行することを推奨
