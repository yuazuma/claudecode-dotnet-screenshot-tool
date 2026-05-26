# Phase 4 LLM連携 実装

## 概要

Azure AI Foundry 経由で Claude モデルを呼び出す LLM 連携機能を実装した。

## 実装内容

### 1. NuGet パッケージ追加 (AutoScreenshot.csproj)

```xml
<PackageReference Include="Azure.AI.Inference" Version="1.0.0-beta.5" />
<PackageReference Include="System.Security.Cryptography.ProtectedData" Version="9.0.0" />
```

### 2. DpapiHelper.cs (新規)

Windows DPAPI でテキストを暗号化/復号するヘルパー (NF-04)。
- `Protect(plainText)` → Base64暗号文
- `Unprotect(cipherText)` → 平文（失敗時は空文字）

### 3. LlmService.cs (新規)

Azure AI Inference SDK を使った LLM サービス (L-03, L-04, L-08)。
- `ImproveDescriptionsAsync(session)` — 全ステップのルールベーステキストを一括送信し、LLMで改善された説明文を `DescriptionLlm` に設定
- `GenerateDigestAsync(session)` — セッション全体を要約した3〜5行のダイジェストを返す
- 失敗時は null 返却 (L-06 フォールバック)

**重要**: `Azure.AI.Inference` 1.0.0-beta.2 で `ChatCompletions.Choices` が削除され、プロパティが `ChatCompletions` に直接フラット化された。正しいアクセスは `response.Value.Content`。

### 4. ManualSession.cs (修正)

```csharp
public string? Digest { get; set; }
```

### 5. ManualSessionRecorder.WriteSessionAsync() (修正)

LLM有効かつエンドポイント/APIキーが両方設定されている場合のみ (L-02):
1. `DpapiHelper.Unprotect()` で復号
2. `LlmService.ImproveDescriptionsAsync()` 実行
3. `LlmService.GenerateDigestAsync()` 実行して `session.Digest` に設定
4. 失敗時はログ記録してルールベースで続行 (L-06)

### 6. MarkdownManualWriter.cs (修正)

Digest が存在する場合、表紙に「操作内容サマリー」セクションを追加 (D-01)。

### 7. DocxManualWriter.cs (修正)

Digest が存在する場合、表紙に「操作内容サマリー」Heading2 + 段落を追加 (D-01)。

### 8. SettingsWindow.xaml (修正)

「LLM連携」TabItem を追加:
- `ChkLlmEnabled` — LLM連携の有効/無効
- `PwdLlmEndpoint` — Azure AI Foundry エンドポイント URL (PasswordBox)
- `PwdLlmApiKey` — API キー (PasswordBox)
- `TxtLlmDeploymentName` — デプロイメント名

### 9. SettingsWindow.xaml.cs (修正)

- `LoadSettings()`: DPAPI復号してPasswordBoxに設定
- `ApplySettings()`: DPAPI暗号化してcfgに保存 (NF-04)

## ビルド結果

```
ビルドに成功しました。
    3 個の警告（SixLabors.ImageSharp 脆弱性のみ）
    0 エラー
```

パブリッシュ完了: `publish/AutoScreenshot.exe`
