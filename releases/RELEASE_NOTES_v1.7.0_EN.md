# AutoScreenshot v1.7.0 Release Notes

**Release date**: 2026-05-30

---

## Overview

Major update featuring lossless image quality, flexible folder template configuration,
fallback folder for high availability, and an export progress dialog.

---

## Changes

### Added

#### Lossless Image Saving (FR-H1, FR-H2)

- **Before images** now match the configured image format (PNG/JPEG/WebP) with lossless quality:
  - JPEG: quality = 100 (no compression)
  - WebP: lossless mode
- **After images** are also saved losslessly

#### Folder Template Configuration (FR-H3)

Save destinations follow a 3-level hierarchy: `Base Folder / Template Folder / File`.

**Available placeholders:**

| Placeholder | Example (2026-05-30 14:30) |
|---|---|
| `{date}` | `20260530` |
| `{datetime}` | `20260530143022` |
| `{date_time}` | `20260530_143022` |
| `{date-time}` | `20260530-143022` |
| `{hour}` | `1400` |
| `{title}` | Session title |
| `{title_short}` | Title up to 40 chars |
| `{id}` | First 8 chars of session ID |
| `{username}` | Windows login name |
| `{computername}` | Machine name |

#### "Path Settings" Tab (FR-H4)

New dedicated tab in the Settings window (after "General") consolidates all folder/path settings for images, manuals, and videos in one place. Hover the [ℹ] icon to see placeholder reference.

#### Secondary Base Folder / Fallback (FR-H5)

Automatically switches to a secondary folder when the primary drive is inaccessible or full.

- Drive check at startup
- Immediate switch on write failure during capture
- Balloon notification on fallback activation

#### Export Progress Dialog (FR-H6)

Non-modal progress window shown during export operations.

- Determinate progress bar with step/frame counter
- Cancel button to abort
- Export progress also shown in tray icon tooltip

### Changed

- Version: 1.6.1 → 1.7.0
- Settings window: path-related settings consolidated in the new "Path Settings" tab

---

## Upgrade Notes

- Existing `config.json` `SaveFolder` is automatically migrated to `ImageBaseFolder`
- `FolderNaming` enum is replaced by `ImageFolderTemplate` (default: `yyyyMMdd`)
- Existing `.ascproj` folders work as-is

---

## Installation

1. Extract `AutoScreenshot-v1.7.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)

SHA-256: `9311fdc184da729b6022512cb5ce31c10700656cb4f7abfb9a0d6103263dc129`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
