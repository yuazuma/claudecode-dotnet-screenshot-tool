## AutoScreenshot v1.2.0

**Released**: 2026-05-27

A Windows system tray application that automatically captures screenshots
triggered by user interactions and generates operation procedure manuals.

---

### What's New in v1.2.0

#### Project File Feature

All artifacts from a recording session are now organized in a single **`.ascproj/` folder**.
Screenshots, thumbnails, procedure manuals, videos, and export history are kept together.

**Project folder structure:**

```
20260527_153524_操作手順書.ascproj/
├── project.json          ← metadata & step list
├── images/               ← screenshots
├── thumbs/               ← thumbnails (JPEG, max 320 px)
├── exports/              ← exported manuals & videos
├── events_YYYY-MM-DD.log ← event log
└── events_YYYY-MM-DD.jsonl
```

**Project View ("Manage Projects…"):**

- Lists all projects, sorted by creation time (newest first)
- Thumbnail grid of steps — delete steps, edit descriptions
- Open project folder in Explorer

**Export options (ProjectViewWindow or tray menu):**

| Export type | Destination |
|---|---|
| Images | `exports/{ts}_images/` |
| Manual (Markdown) | `exports/{ts}_slug.md` |
| Manual (Word) | `exports/{ts}_slug.docx` |
| Video (APNG/MP4) | `exports/` |
| ZIP archive | Any location (Save File dialog) |

**In-project step editing:**

- **Delete step**: Files are moved to `images/_deleted/` (not permanently deleted). A deletion flag is recorded in `project.json`
- **Edit description**: `descriptionOverride` field overwrites LLM / rule-based descriptions

**Tray menu (v1.2.0 layout):**

- "Project break (start new project)" — saves the current session and starts a new project
- "Export >" — submenu for immediate export of the current project
- "Manage Projects…" — opens ProjectViewWindow

**Settings (Settings window → "Project" tab):**

| Setting | Default |
|---|---|
| Enable project feature | On |
| Thumbnail max width (px) | 320 |
| Auto-export Markdown manual | On |
| Auto-export Word manual | Off |
| Auto-export video | Off |
| Open folder on export complete | On |

> **Backward compatibility**: Disabling the project feature reverts to v1.1.0 behavior (date-based folder saves).

---

### Bug Fixes

None (no known bugs in v1.1.0).

---

### Changes (v1.1.0 → v1.2.0)

| Category | Change |
|---|---|
| New file | `Models/ProjectConfig.cs` — project feature settings model |
| New file | `Models/ProjectInfo.cs` — project.json target classes (ProjectInfo / ProjectStep / ExportRecord) |
| New file | `Services/ProjectStore.cs` — project folder creation, project.json read/write, listing |
| New file | `Services/ThumbnailService.cs` — thumbnail generation (JPEG, max 320 px, async) |
| New file | `Services/ExportService.cs` — export orchestrator (images / manual / video / ZIP) |
| New file | `Views/ProjectViewWindow.xaml` — project view window (900×600 px, resizable) |
| Modified | `Models/AppConfig.cs` — added `ProjectConfig Project` property |
| Modified | `Services/FileStorage.cs` — added `SetProjectFolder` / `ClearProjectFolder` |
| Modified | `Services/ManualSessionRecorder.cs` — integrated with ProjectStore for automatic project creation and update |
| Modified | `Services/MetadataLogger.cs` — log output directory corrected to project root in project mode |
| Modified | `Services/NotifyIconWrapper.cs` — tray menu restructured for v1.2.0; added ProjectStore / ExportService |
| Modified | `Views/SettingsWindow.xaml` — added "Project" tab (10th tab) |
| Modified | `AutoScreenshot.csproj` — version 1.2.0 |

---

### Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 version 1809 or later, Windows 11 |
| Architecture | x64 |
| .NET runtime | Not required (self-contained) |
| Administrator | Not required |
| TTS voices | Windows built-in SAPI voices (no extra install needed) |
| MP4 generation | Windows MediaFoundation (built-in on Windows 10+) |

---

### Installation

1. Extract `AutoScreenshot-v1.2.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe`
3. A tray icon appears when ready

**Upgrading from v1.1.0:**
1. Exit AutoScreenshot (tray menu → "Exit")
2. Overwrite all files in the installation folder with the new ZIP contents
3. Run `AutoScreenshot.exe`
4. Settings are carried over automatically (`config.json` is extended with the `project` section on first run)

> **Project feature note**: Starting from v1.2.0, new sessions are saved to `.ascproj/` folders instead of date-based folders. Existing screenshots are unaffected. Disable the project feature in Settings to revert to the v1.1.0 layout.

> **First launch**: If Windows SmartScreen shows a warning, click "More info" → "Run anyway".

---

### What's in the ZIP

| File | Purpose |
|---|---|
| `AutoScreenshot.exe` | Main executable (.NET 8 + WPF native DLLs bundled) |
| `Microsoft.Windows.SDK.NET.dll` | Required for OCR (WinRT projection) |
| `DocumentFormat.OpenXml.dll` et al. | Required for Word (.docx) output |
| `Serilog.dll` et al. | Required for logging |
| `SixLabors.ImageSharp.dll` | Required for WebP encoding |
| `System.Speech.dll` | Required for TTS narration |
| `README.txt` | Install / uninstall instructions |

---

### Known Limitations

- **WebP images are not embedded in Word (.docx)** — Open XML does not support WebP. PNG and JPEG embed correctly
- **SixLabors.ImageSharp 3.1.7** has a reported moderate-severity CVE. This library is used only for internal encoding; no external WebP files are read. Risk is limited
- **LLM integration requires Azure AI Foundry** — direct calls to the Anthropic API are not supported
- **OCR requires a Windows language pack** (e.g., Japanese) to be installed on the OS
- **TTS voice quality depends on system configuration** — built-in voices are of basic quality; high-quality voices (e.g., Microsoft Haruka) can be added via Windows Settings

---

### SHA-256 Checksum

```
29340a1263497800af36923dc5cfbae88804b702573763c9dade3148e1a3067a  AutoScreenshot-v1.2.0-win-x64.zip
```

---

### Licenses

- Application: Private
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
- System.Speech 9.0.0: MIT License (.NET Foundation)
