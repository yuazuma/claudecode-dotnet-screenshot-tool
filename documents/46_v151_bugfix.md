# v1.5.1 バグ修正

## ユーザープロンプト

```
ソースコードの修正すべき箇所がないか確認してください。
```

## 調査手順

全ソースファイルを静的レビュー。着目した問題の種類:
- デッドコード（未使用メソッド・到達不能条件）
- async void の例外処理漏れ
- インデックスとモデルのミスマッチ

## 修正内容

### 1. `VideoGenerator.cs:35-37` — `|| true` デッドコード除去

```csharp
// Before
var steps = session.Steps
    .Where(s => s.ImagePath != null || true) // 画像なしステップも含む
    .ToList();

// After
var steps = session.Steps.ToList();
```

`|| true` により `Where` の条件が常に `true` → `.Where()` 全体が無意味。コメントと共に除去。

### 2. `ExportService.cs:308-344` — 未使用 `BuildSession()` メソッド削除

`BuildAnnotatedSession()` に完全に置き換え済みの旧メソッドが残存。呼び出し元なし。削除。

### 3. `ProjectViewWindow.xaml.cs:759-776` — ステップ追加挿入位置バグ修正

**バグの内容:**
- `insertAfter = _selectedStepIndex` は `_stepVms` のビューインデックス（削除済みステップを除外したフィルタ後リスト上の番号）
- `_selectedProject.Steps.Insert(insertAfter + 1, newStep)` は `_selectedProject.Steps`（削除済みを含む全ステップ）のインデックスとして使用
- `ChkShowDeleted` が OFF の状態で削除済みステップが存在すると両者のインデックスが一致しない

**例（削除済みステップ混在時）:**
```
_selectedProject.Steps: [1, 2(deleted), 3, 4]
_stepVms: [step1, step3, step4]  (index 0, 1, 2)

ユーザーが step3 を選択: _selectedStepIndex = 1
insertAfter = 1, newStepNumber = 3

修正前: Steps.Insert(2, newStep) → [1, 2(deleted), newStep(3), 3→4, 4→5]
  ✗ step3 の前に挿入（step3=index2 に割り込んでしまう）

修正後: FindLastIndex(s => s.StepNumber < 3) = index 1 (=step2_deleted)
         insertIdx = 2
         Steps.Insert(2, newStep) → [1, 2(deleted), newStep(3), 3→4, 4→5]
         ...に見えるが、ShiftはStepNumber>=3のみなので:
         newStepNumber = 3+1 = 4? ← いや afterStepNumber = step3.StepNumber = 3,
         newStepNumber = 4, shift steps >= 4: [1, 2(del), 3, 4→5]
         FindLastIndex(s => StepNumber < 4) → index 2 (=step3)
         Steps.Insert(3, newStep(4)) → [1, 2(del), 3, newStep(4), 4→5]  ✓
```

**修正後コード:**
```csharp
int afterStepNumber = _selectedStepIndex >= 0
    ? _stepVms[_selectedStepIndex].Step.StepNumber
    : (_selectedProject.Steps.Count > 0 ? _selectedProject.Steps.Max(s => s.StepNumber) : 0);
int newStepNumber = afterStepNumber + 1;

foreach (var s in _selectedProject.Steps.Where(s => s.StepNumber >= newStepNumber))
    s.StepNumber++;

var newStep = new ProjectStep { StepNumber = newStepNumber, ... };
int insertIdx = _selectedProject.Steps.FindLastIndex(s => s.StepNumber < newStepNumber) + 1;
_selectedProject.Steps.Insert(insertIdx, newStep);
```

### 4. `ProjectViewWindow.xaml.cs:62` — Loaded ハンドラー例外処理追加

```csharp
// Before
Loaded += async (_, _) => await RefreshProjectListAsync();

// After
Loaded += async (_, _) =>
{
    try { await RefreshProjectListAsync(); }
    catch (Exception ex)
    {
        Log.Error(ex, "プロジェクト一覧の初期読み込み失敗");
        SetStatus("プロジェクトの読み込みに失敗しました。");
    }
};
```

`async void` ラムダの未補足例外はアプリクラッシュを引き起こす。

## 非該当として除外した指摘

| 指摘 | 判断 | 理由 |
|---|---|---|
| `Dispose` での `GetAwaiter().GetResult()` ブロッキング | 問題なし | async チェーンが全て `ConfigureAwait(false)` を使用、UI スレッド不要 |
| fire-and-forget エクスポート（`_ = ...`） | 意図的 | ExportService 内部でエラー処理済み、UI 応答性維持のため |
| `async void` イベントハンドラー | 問題なし | WPF/WinForms の標準パターン |
| `Application.Current?.Dispatcher.BeginInvoke()` | 問題なし | `?.` 演算子で null 条件チェック済み |
