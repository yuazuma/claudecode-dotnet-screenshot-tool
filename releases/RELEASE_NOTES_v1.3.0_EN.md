# AutoScreenshot v1.3.0 Release Notes

**Release date**: 2026-05-27

---

## Overview

v1.3.0 adds five major features: visual step annotations, enhanced project management, merge/split operations, HTML export, and incremental LLM processing.

---

## New Features

### FR-C: Step Annotations

Added an annotation panel to the right pane of the Project View window.

- **Tools**: Number badge, Arrow, Rectangle, Callout
- **Colors**: Red, Blue, Yellow, Green
- Annotations are persisted to `project.json` with pixel coordinates
- Exports (Markdown, Word, HTML, Video) automatically use annotation-burned images

### FR-D: Project Management Enhancements

- **Full-text search**: Filter by title, description, or tags
- **Tag filter**: Toggle-button strip for multi-tag filtering
- **Step reordering**: Drag and drop thumbnails to reorder (StepNumber updated automatically)
- **Manual step insertion**: "＋Add Step" button to insert a custom image + description

### FR-E: Project Merge & Split

- **Merge**: Select multiple projects → "Merge..." button → enter title → creates a new project combining steps in chronological order (originals unchanged)
- **Split**: Select a step → "Split Here" button → enter two titles → creates two new projects split at that step (original unchanged)

### FR-A: HTML Export

- "Manual (HTML)" button generates a single self-contained HTML file
- Images are Base64-embedded — share with one file, no separate assets needed
- Auto HTML export on recording stop can be enabled in Settings

### FR-B: Incremental LLM Processing

- After each step is appended, LLM description improvement is queued in the background
- Enable via "Auto LLM improvement on step append" checkbox in Settings
- Falls back to rule-based description on LLM failure (existing behavior preserved)

---

## Installation

1. Extract `AutoScreenshot-v1.3.0-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no admin rights or .NET runtime required)
3. Upgrading from v1.2.0: existing `config.json` and `.ascproj` folders work as-is

SHA-256: `36096a3427c04f14db3941ea9e258fbd714e59064ae0e40c16a3d28d47db8901`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
