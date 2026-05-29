# AutoScreenshot v1.6.0 Release Notes

**Release date**: 2026-05-29

---

## Overview

Added before/after screenshot separation. For every click or key input, AutoScreenshot
automatically captures two screenshots — one before the action and one after — to improve
both procedure manual quality and audit trail completeness.

---

## Changes

### Added

- **Before/after screenshot separation**
  - Captures a "before" screenshot (PNG) on mouse button DOWN
  - Captures an "after" screenshot after `PostClickDelayMs` (default 250ms) delay following button UP
  - For keyboard input: captures "before" at the start of a new key sequence, "after" after the idle timeout
  - Before images are stored as PNG (lossless) in `images/before/` — no annotations, overlays, or timestamps applied
  - Markdown / Word manuals output before → after in sequence
  - HTML manuals display before / after side by side with captions
  - Image export includes before images in an `exports/images/before/` subfolder
  - ProjectViewWindow step detail panel shows the before image (read-only / audit trail)

- **New settings** (Capture Trigger tab)
  - Capture before screenshot (default: On)
  - After-click capture delay: ms (default: 250ms)

### Changed

- Data model unified: `ImagePath` → `AfterImagePath`, `ThumbPath` → `AfterThumbPath`
  - Existing `project.json` files are automatically migrated on load (no data loss)
- Right-click and middle-click "after" capture timing changed from DOWN to UP + delay

### Fixed

- Keyboard before image expiry bug: fixed an issue where the before image was discarded
  during long typing sessions (>5 seconds of continuous input)

---

## Installation

1. Extract `AutoScreenshot-v1.6.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.5.x:
   - Your existing `config.json` works as-is
   - Your existing `.ascproj` folders work as-is
     (legacy `imagePath` fields are automatically migrated to `afterImagePath` on load)

SHA-256: `d831f7b67375e55dc665643a365caad82ca827d67401be3f4b238df449031cbd`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
