# AutoScreenshot v1.4.0 Release Notes

**Release date**: 2026-05-28

---

## Overview

v1.4.0 focuses on UI/UX improvements and visual feedback enhancements.
The tray icon now changes to 5 colors based on processing state, and the Project View window
has been redesigned with a side-by-side split layout that keeps the step list and details
always visible simultaneously.

---

## New Features & Changes

### 5-State Tray Icon

Real-time visual feedback through icon color changes.

| State | Color | Trigger |
|---|---|---|
| Recording | Blue `#0078D7` | Normal recording |
| Paused | Gray `#808080` | User pause / low disk auto-stop |
| Captured | Green `#107C10` | 200ms flash after successful capture |
| Processing | Orange `#CA5010` | Export in progress (supports concurrent exports) |
| Error | Red `#C50F1F` | Shown for 5 seconds after export error |

### Restructured Tray Menu

Menu layout adapts based on whether the project feature is enabled.
- Project enabled: shows project name and export submenu
- Project disabled: shows session management, manual generation, and video generation directly

### Settings Window Tab Reorder

Frequently used tabs moved to the front.

```
[General] [Capture Triggers] [Storage] [Privacy]       ← Capture settings
[Manual Gen] [LLM] [Video Gen] [Project]               ← Output & generation
[Notifications] [Metadata]                             ← Auxiliary & details
```

### Project View Window UI Refactoring

- **Vertical step list**: Always shows step number, thumbnail, and description. Drag-to-reorder handle (☰) per item
- **Persistent split layout**: Step list and detail (image / annotations / description) always shown side by side — no more tab switching
- **Export dropdown button**: "▼ Export" button at the top consolidates all export actions
- **Collapsible annotation tools**: Expander auto-opens only when existing annotations are present, saving screen space
- **Progress bar**: ProgressBar shown in status bar during export operations
- **Show deleted toggle**: Checkbox to show/hide deleted steps

---

## Installation

1. Extract `AutoScreenshot-v1.4.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no admin rights or .NET runtime required)
3. Upgrading from v1.3.0: existing `config.json` and `.ascproj` folders work as-is

**SHA-256**: `0bf25b4b00f6df6246be7d6ea3d55d31c66e7f9088fc4b15f575bc271a3776f0`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
