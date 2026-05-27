# v1.1.0 リリースバイナリ・ハッシュ更新

## ユーザープロンプト

```
./release/配下のバイナリ・ハッシュもv1.1.0のものにビルドしてアップデートしてください。
```

---

## 実施内容

### 問題確認

既存の `publish/` ディレクトリに `System.Speech.dll` が含まれていないことを確認。
前回セッションのパブリッシュは `System.Speech` 追加前のビルドだった可能性があるため再ビルドが必要。

```
publish/ に System.Speech.dll が存在しない → 再 dotnet publish が必要
```

### ビルド実行

```
dotnet publish src/AutoScreenshot/AutoScreenshot.csproj \
  -c Release -r win-x64 --self-contained true -o publish
```

**結果**: 成功（警告: SixLabors.ImageSharp 3.1.7 の NU1902 脆弱性警告のみ、エラーなし）

再ビルド後、`publish/System.Speech.dll` が存在することを確認。

### ZIP 作成

```python
# Python zipfile モジュールで publish/ 全ファイルを圧縮
zipfile.ZipFile('releases/AutoScreenshot-v1.1.0-win-x64.zip', 'w', ZIP_DEFLATED, compresslevel=6)
```

**結果**:
- ファイル: `releases/AutoScreenshot-v1.1.0-win-x64.zip`
- サイズ: 81,757,399 bytes（約 78 MB）

### SHA-256 ハッシュ計算

```python
sha256 = hashlib.sha256()
# 65536 バイトのチャンクで計算
```

**結果**:
```
SHA-256: 1a9cc18d21cce3639cf7516403d4f7976721ed9c5238c6223a9fefdb19dfe696
```

### ハッシュファイル作成

```
releases/AutoScreenshot-v1.1.0-win-x64.zip.sha256
内容: 1a9cc18d21cce3639cf7516403d4f7976721ed9c5238c6223a9fefdb19dfe696  AutoScreenshot-v1.1.0-win-x64.zip
```

### リリースノートへのハッシュ反映

- `RELEASE_NOTES_v1.1.0_JA.md` に「ファイルの整合性確認」セクションを追加
- `RELEASE_NOTES_v1.1.0_EN.md` に「SHA-256 Checksum」セクションを追加

---

## releases/ 最終状態

| ファイル | サイズ |
|---|---|
| `AutoScreenshot-v1.0.0-win-x64.zip` | 86,209,829 bytes |
| `AutoScreenshot-v1.0.0-win-x64.zip.sha256` | 101 bytes |
| `AutoScreenshot-v1.1.0-win-x64.zip` | **81,757,399 bytes** |
| `AutoScreenshot-v1.1.0-win-x64.zip.sha256` | **101 bytes** |
| `CHANGELOG.md` | 4,576 bytes |
| `README.txt` | 1,700 bytes |
| `RELEASE_NOTES_EN.md` | 3,427 bytes（v1.0.0用） |
| `RELEASE_NOTES_JA.md` | 4,978 bytes（v1.0.0用） |
| `RELEASE_NOTES_v1.1.0_EN.md` | 5,716 bytes |
| `RELEASE_NOTES_v1.1.0_JA.md` | 6,890 bytes |

v1.1.0 の ZIP が v1.0.0 より約 4.4 MB 小さいのは、.NET ランタイムの差分最適化と圧縮率の差によるもの。

---

## 補足

- v1.0.0 の ZIP・ハッシュは削除せず残存させた（過去バージョンの配布用）
- `releases/RELEASE_NOTES_EN.md` / `RELEASE_NOTES_JA.md`（v1.0.0用）も削除せず残存
- 新バージョンはバージョン番号付きファイル名（`RELEASE_NOTES_v1.1.0_*.md`）で管理する命名規則を採用
