## AutoScreenshot v1.0.0

A Windows system tray application that automatically captures screenshots
triggered by user interactions and generates operation procedure manuals.

---

### Features

**Automatic Screenshot Capture**
- Captures on mouse clicks (left/right/middle), drag-and-drop, and scroll wheel
- Captures after keyboard idle (default: 2s) to record text input sessions
- Captures on active window changes
- Screen diff detection (30% pixel change threshold, 3s interval)
- Cooldown, app exclusion, and pause/resume controls

**Auto-Generated Operation Manuals**
- Generates Markdown (.md) and Word (.docx) manuals from captured events
- Extracts UI element names (button labels, field names) via Windows UI Automation
- Falls back to Windows OCR (Windows.Media.Ocr) when UIA is unavailable
- Records actual typed text with Shift-state awareness and Backspace correction
- Auto-chapters by active window, with time-gap subheadings
- Custom template support (.md template, .dotx template)

**Azure AI Foundry Integration**
- Improves step descriptions using Claude deployed on Azure AI Foundry
- Generates a 3–5 line session summary for the document cover page
- API credentials stored encrypted via Windows DPAPI (no plaintext storage)
- Graceful fallback to rule-based descriptions on LLM failure

**Privacy & Security**
- Auto-detects and blacks out password fields (UIAutomation IsPassword=true)
- App exclusion list with wildcard pattern matching
- Screen diff suppressed within 1s of keyboard/mouse activity

---

### Requirements

- Windows 10 (1809+) or Windows 11, x64
- No .NET runtime installation required (self-contained)
- No administrator privileges required

---

### Installation

1. Extract `AutoScreenshot-v1.0.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe`
3. A tray icon appears when ready

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
| `README.txt` | Install / uninstall instructions |

---

### Known Limitations

- **WebP images are not embedded in Word (.docx)** — Open XML does not support WebP. PNG and JPEG embed correctly
- **SixLabors.ImageSharp 3.1.7** has a reported moderate-severity CVE. This library is used only for internal encoding; no external WebP files are read. Risk is limited, but an update is planned for the next release
- **LLM integration requires Azure AI Foundry** — direct calls to the Anthropic API are not supported
- **OCR requires a Windows language pack** (e.g., Japanese) to be installed on the OS

---

### SHA-256 Checksum

```
e008d0a9c84ff0b74cdc08b01a5a4b5ad757034ee06ce3499610b391dcd7e1ba  AutoScreenshot-v1.0.0-win-x64.zip
```

---

### Licenses

- Application: Private
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
