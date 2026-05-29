# v1.6.1 before 画像フォールバック実装・要件定義書作成

## ユーザープロンプト

```
操作前の画像が取得できなかった場合は、アノテーションを付与する前の、「素の操作後画像」を
操作前として扱うように、ソースコードとドキュメント類を修正してください。また、これを含めた
今回（v1.6.1）の修正内容を、要件定義書に書き起こして、
./requirements/追加要件_LLM呼出修正.md として保存してください。
```

---

## 実装内容

`BeforeImagePath == null` のステップに対して、
素の after 画像（アノテーション適用前の `AfterImagePath`）をフォールバックとして使用する。

### 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `Services/ExportService.cs` | `BuildAnnotatedSession()`: `rawAfterPath` を before のフォールバックとして設定 |
| `Services/ExportService.cs` | `ExportImagesAsync()`: `step.BeforeImagePath ?? step.AfterImagePath` でフォールバック参照 |
| `Services/HtmlManualWriter.cs` | `BuildBeforeImageTag()`: `step.BeforeImagePath ?? step.AfterImagePath` をフォールバック参照 |
| `Views/ProjectViewWindow.xaml.cs` | `LoadAnnotationImage()`: `step.BeforeImagePath ?? step.AfterImagePath` でフォールバック表示 |

### フォールバックの効果

| 状況 | 変更前 | 変更後 |
|---|---|---|
| before なし・annotation なし | before 欄が空 | 素の after 画像を before に表示（before/after 同一） |
| before なし・annotation あり | before 欄が空、after はアノテーション付き | 素の after → アノテーション付き after の比較が可能 |
| before あり | 変更なし | 変更なし |

---

## 要件定義書

`requirements/追加要件_LLM呼出修正.md` を新規作成。

v1.6.1 の修正内容 3 件を FR-G1〜G3 として文書化:
- FR-G1: LLM 呼び出し形式変更（Azure AI Inference SDK → HttpClient + Anthropic Messages API）
- FR-G2: エンドポイント URL 入力欄マスク解除（PasswordBox → TextBox）
- FR-G3: before 画像フォールバック

---

## ビルド確認

```
0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```
