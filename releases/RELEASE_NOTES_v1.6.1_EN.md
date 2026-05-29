# AutoScreenshot v1.6.1 Release Notes

**Release date**: 2026-05-29

---

## Overview

Patch release with LLM integration fix, settings UI improvement, and before-image fallback.

---

## Changes

### Fixed

#### LLM calls switched to Anthropic Messages API

The Azure AI Foundry endpoint uses the Anthropic Messages API format (`/anthropic/v1/messages`),
but the previous implementation called the OpenAI-compatible Chat Completions format, causing all
LLM operations (incremental LLM and batch LLM) to fail with HTTP 400.

**Changes:**
- URL: `{base}/anthropic/v1/messages` (uses host from the configured endpoint)
- Auth: `Authorization: Bearer {key}` header
- Version: `anthropic-version: 2023-06-01` header
- Body format: Anthropic Messages API (`system` is a top-level field)
- DPAPI encryption for storage is unchanged (NF-04 maintained)

#### Endpoint URL field: removed password masking

Changed the "Microsoft Azure AI Foundry Endpoint URL" input in the LLM Integration tab
from a `PasswordBox` (characters masked as `●`) to a plain `TextBox`, making the URL
easy to verify and copy.

#### Before image fallback

Fixed an issue where steps without a `BeforeImagePath` exported with an empty before slot.
When before is unavailable, the raw after image (before annotation rendering) is now used as
the before image.

For annotated steps this enables "before annotations → after annotations" comparison.

**Applied in:** Markdown / Word / HTML exports, image export, ProjectViewWindow display.

### Changed

- Version: 1.6.0 → 1.6.1

---

## Installation

1. Extract `AutoScreenshot-v1.6.1-win-x64.zip` to any folder
2. Run `AutoScreenshot.exe` (no administrator rights or .NET runtime required)
3. Upgrading from v1.6.0:
   - Your existing `config.json` works as-is
   - Your existing `.ascproj` folders work as-is

SHA-256: `7cb2208b23a546cf8bf48761565808b8cb18c1ab9d650553ae43055c55bd8c69`

---

## Requirements

- Windows 10 (1809) or later / Windows 11
- x64 architecture
