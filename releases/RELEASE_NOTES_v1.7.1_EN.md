# AutoScreenshot v1.7.1 Release Notes

**Release date**: 2026-05-31

---

## Overview

v1.7.1 is a bug-fix and feature-addition release that adds official support for MP4 export, RDP screen capture, and Markdown image display on Azure Windows Server 2025.
For environments without an H.264 MFT, a FFmpeg-based fallback is implemented, and **AVI output has been removed entirely; MP4 (H.264) is the sole video format.**
RDP screenshot capture now uses `Windows.Graphics.Capture` API, fixing the solid-background issue.

---

## Changes

### Added

- **`--export` CLI flag for headless export**
  - `AutoScreenshot.exe --export <project_path> [--type md,html,video,images,zip]`
  - Runs exports without a GUI (for RDP sessions and automation scripts)
  - Exit codes: 0=success, 1=partial failure, 2=argument error

- **MP4 export on Azure Windows Server 2025**
  - Encoders are tried in priority order:
    1. `MfVideoWriter` — IMFSinkWriter H.264 (standard)
    2. `H264Mp4Writer` — direct H.264 MFT (bypasses MPEG-4 muxer)
    3. `FfmpegMp4Writer` — FFmpeg subprocess (searches PATH and known install paths)
  - **AVI fallback removed.** If no MP4 path succeeds, an explicit error is logged with instructions to install FFmpeg or the Media Feature Pack.

- **RDP screen capture improvement**
  - `Windows.Graphics.Capture (WGC)` API reads directly from the DWM compositor output
  - Automatically switches to WGC when an RDP session is detected (GDI remains for non-RDP)
  - Falls back to GDI `CopyFromScreen` if WGC fails
  - Yellow capture notification border suppressed (Windows 11 Build 22000+)

- **Markdown image `_images/` subfolder management**
  - Creates `{MDFileName}_images/` alongside the MD file and copies all images there
  - Eliminates `../images/...` paths — images now display correctly in VS Code, GitHub, etc.
  - Images wider than 1200px are resized to exactly 1200px (aspect ratio preserved)

### Fixed

- **JSON property name collision**: `ManualGenConfig.outputFolder` was serialized twice, causing a deserialization exception on startup. Fixed by removing the duplicate field.
- **Folder template `.ascproj` bug**: The `s` character in `.ascproj` was misinterpreted as a `DateTime.ToString()` seconds specifier during template evaluation. Fixed with placeholder escaping.
- **JPEG save `ArgumentException`**: Saving an ARGB Bitmap to JPEG failed with `ArgumentException`. Fixed by converting to `Format24bppRgb` before saving.
- **Markdown image duplicate copy**: When before/after images reference the same file, a spurious copy (`step_001_1.png`) was generated. Fixed by source-path caching.
- **VideoGenerator `dur` division bug**: Frame duration (already in seconds) was incorrectly divided by 10,000,000. Fixed by removing the erroneous division.

### Changed

- Version: 1.7.0 → 1.7.1

---

## Installation

1. Extract `AutoScreenshot-v1.7.1-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.7.0:
   - Your existing `config.json` works as-is
   - Your existing `.ascproj` folders work as-is

> **MP4 export note**: On Azure Windows Server or other environments without an H.264 encoder,
> FFmpeg is required. Install it via `winget install Gyan.FFmpeg` or
> `DISM /Online /Add-Capability /CapabilityName:Media.MediaFeaturePack~~~~0.0.1.0`.

SHA-256: `68b96b11ef6ae2c161d36ff2fe7274e65a26e854d6ff94ca975b70adc4abece2`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
- MP4 export: H.264 MFT (built into Windows client) or FFmpeg 8.x+
- RDP capture: Windows 10 1803+ / Windows Server 2019+ (WGC API support)
