# AutoScreenshot v1.5.1 Release Notes

**Release date**: 2026-05-28

---

## Overview

Patch release with bug fixes and dead code removal found during code review.
No new features.

---

## Changes

### Fixed

- **`ProjectViewWindow`: Fixed step insertion position bug in "Add Step"**
  - `BtnAddStep_Click` was using the view index (index in the filtered `_stepVms` list,
    which excludes deleted steps) as an index into the model list (`_selectedProject.Steps`,
    which includes deleted steps)
  - When deleted steps were present, the new step was inserted at the wrong position
    and assigned the wrong step number
  - Fixed by computing insertion position from `StepNumber` using `FindLastIndex`

- **`ProjectViewWindow`: Fixed crash on load failure**
  - The `Loaded` event's `async void` lambda had no exception handler
  - An error from `ListProjectsAsync` would crash the entire application
  - Wrapped in try-catch; on failure, shows an error message in the status bar

- **`ExportService`: Removed unused `BuildSession()` method** (dead code removal)

- **`VideoGenerator`: Removed `Where(s => ... || true)` dead code**

### Changed

- Version: 1.5.0 → 1.5.1

---

## Installation

1. Extract `AutoScreenshot-v1.5.1-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.5.0:
   - Your existing `config.json` works as-is
   - Your existing `.ascproj` folders work as-is

SHA-256: `49f8e5000d1049f28d2af9fa42245bb74f9d1a8d299c2baecbf5da8c7a913ed8`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
