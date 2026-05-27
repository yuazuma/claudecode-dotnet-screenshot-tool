## AutoScreenshot v1.1.0

**Released**: 2026-05-27

A Windows system tray application that automatically captures screenshots
triggered by user interactions and generates operation procedure manuals.

---

### What's New in v1.1.0

#### Video Generation

AutoScreenshot can now generate **APNG (animated PNG)** and **MP4 (H.264)** video files directly from the same operation steps used for procedure manuals.

**Highlights:**

- **APNG output**: Pure managed PNG chunk writer (no third-party library). Plays in browsers and virtually all PNG viewers
- **MP4 output**: H.264 + AAC encoding via Windows MediaFoundation (IMFSinkWriter COM interop)
- **TTS narration**: Reads step descriptions aloud using Windows SAPI and mixes the audio into the MP4 track
- **Frame decorations**:
  - Ripple effect (concentric circles at the cursor position for mouse events)
  - Dashed border around the cursor area
  - Telop band at the bottom (event label / description / timestamp)
- **Output resolution**: HD (1280×720), Full HD (1920×1080), QHD (2560×1440)
- **Frame timing**: Fixed seconds per frame, or variable (matched to TTS duration)
- **Background generation**: Recording and manual export continue uninterrupted during video generation
- **Tray menu "Generate Video"**: Instant generation from the current session
- **Auto-generate with manual**: Optionally generate video whenever a manual is exported

**Settings (Settings window → "Video Generation" tab):**

| Setting | Default |
|---|---|
| Output APNG | On |
| Output MP4 | On |
| Video output folder | Auto (`videos\` inside save folder) |
| Frame timing | Fixed 3.0 s |
| Output resolution | HD (1280×720) |
| MP4 bitrate | 4 Mbps |
| Ripple effect | On |
| Cursor border | On |
| Telop | On |
| TTS narration | On |
| Open folder when done | On |
| Auto-generate with manual | Off |

---

### Bug Fixes

None (v1.0.0 was the initial release).

---

### Changes (v1.0.0 → v1.1.0)

| Category | Change |
|---|---|
| New file | `Models/VideoGenConfig.cs` — video generation settings model |
| New file | `Services/TtsService.cs` — Windows SAPI TTS |
| New file | `Services/FrameRenderer.cs` — frame compositing |
| New file | `Services/ApngWriter.cs` — APNG chunk writer |
| New file | `Services/MfVideoWriter.cs` — MediaFoundation MP4 output |
| New file | `Services/VideoGenerator.cs` — video generation orchestrator |
| Modified | `Models/AppConfig.cs` — added `VideoGenConfig VideoGen` property |
| Modified | `Services/ManualSessionRecorder.cs` — added `SetVideoGenerator` / `GenerateVideoNow` |
| Modified | `Services/NotifyIconWrapper.cs` — added "Generate Video" tray menu item |
| Modified | `Services/Notifier.cs` — added `ShowBalloon(title, message)` |
| Modified | `Views/SettingsWindow.xaml` — added "Video Generation" tab (9th tab) |
| Modified | `Views/SettingsWindow.xaml.cs` — LoadSettings / ApplySettings for video tab |
| Modified | `AutoScreenshot.csproj` — added `System.Speech 9.0.0`, bumped version to 1.1.0 |

---

### Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 version 1809 or later, Windows 11 |
| Architecture | x64 |
| .NET runtime | Not required (self-contained) |
| Administrator | Not required |
| TTS voices | Windows built-in SAPI voices (no extra install needed; high-quality voices installable via System Settings) |
| MP4 generation | Windows MediaFoundation (built-in on Windows 10+) |

---

### Installation

1. Extract `AutoScreenshot-v1.1.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe`
3. A tray icon appears when ready

**Upgrading from v1.0.0:**
1. Exit AutoScreenshot (tray menu → "Exit")
2. Overwrite all files in the installation folder with the new ZIP contents
3. Run `AutoScreenshot.exe`
4. Settings are carried over automatically (`config.json` is extended with the `videoGen` section on first run)

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
| `System.Speech.dll` | Required for TTS narration (added in v1.1.0) |
| `README.txt` | Install / uninstall instructions |

---

### Known Limitations

- **WebP images are not embedded in Word (.docx)** — Open XML does not support WebP. PNG and JPEG embed correctly
- **SixLabors.ImageSharp 3.1.7** has a reported moderate-severity CVE. This library is used only for internal encoding; no external WebP files are read. Risk is limited
- **LLM integration requires Azure AI Foundry** — direct calls to the Anthropic API are not supported
- **OCR requires a Windows language pack** (e.g., Japanese) to be installed on the OS
- **TTS voice quality depends on system configuration** — built-in voices are of basic quality; high-quality voices (e.g., Microsoft Haruka) can be added via Windows Settings

---

### Licenses

- Application: Private
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
- System.Speech 9.0.0: MIT License (.NET Foundation)
