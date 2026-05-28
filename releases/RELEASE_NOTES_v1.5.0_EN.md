# AutoScreenshot v1.5.0 Release Notes

**Release date**: 2026-05-28

---

## Overview

Removed the project feature enable/disable toggle that existed solely for v1.1.0
backward compatibility. The project feature is now always active.

The `ProjectConfig.Enabled` flag and all related `if/else` branches have been fully
removed, improving code readability and maintainability.

---

## Changes

### Removed

- **Project feature enable/disable toggle**
  - Removed the "Enable project file feature (off = v1.1.0 behavior)" checkbox
    from the Project tab in Settings
  - Removed `ProjectConfig.Enabled` property
  - Removed `if (projectEnabled)` branch from tray menu (project-enabled layout
    is now always shown)
  - Removed all `Enabled` conditions from session processing logic

### Changed

- Project feature is now always active; fallback to pre-v1.1.0 behavior is removed
- Auto-export flags always reference `ProjectConfig` values
- `ManualGenConfig.OutputMarkdown` / `OutputDocx` and
  `VideoGen.AutoGenerateWithManual` remain in the Settings UI but are no longer
  referenced during automatic session export

---

## Installation

1. Extract `AutoScreenshot-v1.5.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.4.0:
   - Your existing `config.json` works as-is
     (if `"Enabled": false` is present it will be silently ignored)
   - Your existing `.ascproj` folders work as-is

SHA-256: `a2a2ffc9fb9629edb53589bb2241ef9b647877665e5936bc485736ddf563806c`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
