# AutoScreenshot v1.6.1 Release Notes

**Release date**: 2026-05-29

---

## Overview

Patch release with a single UI fix in the Settings window. No new features.

---

## Changes

### Fixed

- **Endpoint URL field: removed password masking**
  - Changed the "Microsoft Azure AI Foundry Endpoint URL" input in the LLM Integration tab
    from a `PasswordBox` (characters hidden as `●`) to a plain `TextBox`
  - The URL is now visible, making it easier to verify and copy
  - Storage in config.json remains DPAPI-encrypted (NF-04 unchanged)

### Changed

- Version: 1.6.0 → 1.6.1

---

## Installation

1. Extract `AutoScreenshot-v1.6.1-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.6.0:
   - Your existing `config.json` works as-is
   - Your existing `.ascproj` folders work as-is

SHA-256: `4edf6dea1cb2450cbe61d42966964f92e0e04d08935dc4d742438754a4bf71f3`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
