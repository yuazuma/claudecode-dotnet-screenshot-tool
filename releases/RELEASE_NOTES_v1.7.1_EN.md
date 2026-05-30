# AutoScreenshot v1.7.1 Release Notes

**Release date**: 2026-05-30

---

## Overview

v1.7.1 is a bug-fix and minor feature release that adds official MP4 export support on Azure Windows Server 2025.
For environments without an H.264 MFT (Azure Server without Desktop Experience, etc.), a FFmpeg-based fallback is now implemented.
**AVI output has been removed entirely; MP4 (H.264) is the sole video format.**

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
  - **AVI fallback removed.** If no MP4 path succeeds, an explicit error is logged with
    instructions to install FFmpeg or the Media Feature Pack.

### Fixed

- **JSON property name collision**: `ManualGenConfig.outputFolder` was serialized twice, causing a deserialization exception on startup. Fixed by removing the duplicate field.
- **Folder template `.ascproj` bug**: The `s` character in `.ascproj` was misinterpreted as a `DateTime.ToString()` seconds specifier during template evaluation. Fixed with placeholder escaping.
- **JPEG save `ArgumentException`**: Saving an ARGB Bitmap to JPEG failed with `ArgumentException`. Fixed by converting to `Format24bppRgb` before saving.

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

SHA-256: `84a043e2f598c3a2bef7ea55f9f1cd09a974b6d2ab12ad8aae8c645dec8a657d`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
- MP4 export: H.264 MFT (built into Windows client) or FFmpeg 8.x+
