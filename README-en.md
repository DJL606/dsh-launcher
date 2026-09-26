<!-- ANCHOR:en -->
<h1 align="center">dsh-launcher</h1>

<div align="center">

**A one-click Windows launcher for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (`dsh`)**

Single-file C# · no third-party dependencies · lives in the system tray

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#requirements)
[![Language: C%23](https://img.shields.io/badge/Language-C%23-239120.svg)](#build)
[![Release: v0.6.7](https://img.shields.io/github/v/release/DJL606/dsh-launcher?label=Release)](https://github.com/DJL606/dsh-launcher/releases)

**Language:** [中文](README.md) ｜ **English**

</div>

---

## Table of contents

- [What it is](#what-it-is)
- [Problems it solves](#problems-it-solves)
- [Features](#features)
  - [Anti-hang design](#anti-hang-design)
- [Requirements](#requirements)
- [Install](#install)
- [Usage](#usage)
- [Build](#build)
- [Configuration & self-test hooks](#configuration--self-test-hooks)
- [Troubleshooting](#troubleshooting)
- [Repository layout](#repository-layout)
- [License](#license)
- [Disclaimer](#disclaimer)

---

## What it is

A Windows launcher for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (`dsh`). It starts the local `dsh web` service hidden, opens the page using the **tokenized URL**, stays resident in the system tray, and handles plugin-compatibility pre-checks, update checks and self-healing around the launch.

Its point is to catch the "service won't start / page won't open / plugin version mismatch" failures **at launch time**, instead of leaving you staring at `Failed to fetch` in the browser.

> **Unofficial project.** Not affiliated with DeepSeek. The repo name is a joke name — functionally this is just a dsh launcher.

## Problems it solves

| Symptom | Real cause | What the launcher does |
|---|---|---|
| Service won't start at all; logs say `plugin tree failed to load` | Plugins declare compatibility against a **specific dsh version**; a mismatch throws while the profile is being loaded | Scans the server output for that error, extracts the plugin name and loader entry, and pops a repair dialog; offers **file-level disable** (3 config edits + automatic backup) with no pnpm needed |
| Page opens but buttons do nothing / images won't send (`Failed to fetch`) | ① The service isn't running at all (the browser is showing a cached page); ② the tab is running an **old front-end bundle** calling endpoints that were renamed | Starts the service and health-checks it; opens the page with the tokenized URL; shows a tray balloon when the API self-check fails |
| Old version keeps running no matter how often you restart | An orphaned service on port 3080 left behind by a killed launcher — nothing owns or stops it | Uses `netstat` to find the listener plus a **service record** (PID / start time / version) to decide precisely, then asks once whether to restart on the new version |
| dsh won't start after an upgrade | New version installed but plugins haven't caught up | Pre-launch plugin compatibility check (re-implements the plugins' `assertHarnessCompatibility` semantics and scans plugin code for real load-time assertions), classifies `fatal` vs `risky`, and only blocks on `fatal` |
| dsh crashes immediately when started from certain host environments | The host injects a `NODE_OPTIONS` language shim carrying a safe-delete guard that blocks dsh from deleting its own `.lock` file | Clears `NODE_OPTIONS` and `CODEBUDDY_SAFE_DELETE_BULK_GUARD` on the command line before launching |
| An update prompt wrecks the environment | The new plugin version only supports a newer dsh (e.g. requires `>=0.1.7-rc.1 <0.1.8-0` while the machine runs `0.1.5-rc.3`) | **Compatibility gate**: only offers plugin versions compatible with the installed dsh, and explains why the others are withheld |

## Features

- **One-click launch** — starts dsh from the npx cache hidden (no `npx` resolution, starts instantly, works offline); opens the browser only once the service is ready, and only one tab.
- **Tokenized URL** — captures `dsh web: http://127.0.0.1:3080/?token=...` from the server's stdout and uses it to open the page (a bare URL is rejected by token validation).
- **Tray resident** — double-click the tray icon to reopen the page. The context menu offers:
  - Open DeepSeek page
  - **Check dsh / plugin updates…** — a single check-list dialog letting you pick updates per component
  - Plugin compatibility check…
  - Switch version (takes effect on restart)
  - **Stop service and exit** (merges "stop my own service" and "deal with someone else's service")
- **Update checks** — one automatic check after startup (dialog only when something new exists) plus manual checks from the tray:
  - dsh itself → downloads the new version in the background; **automatically rolls back** if install/launch fails, so you can never end up "upgraded but unable to open"
  - Plugins → **file-level install**: `npm pack` → unpack → overwrite into the profile's `node_modules` (whole-directory backup first, rollback-able), no pnpm needed
  - `link:`/`file:` specs and symlinked plugins are skipped automatically (local dev packages are left alone)
- **Health self-check** — 6 seconds after launch, validates the client-api path via token → cookie → read-only POST; logs failures and shows a tray balloon.
- **Exit on shutdown** — closing the whole browser (or stopping from the tray) tears down the service and its process tree, leaving no orphans.
- **Read-only self-test hook** — `DSH_LAUNCHER_SELFTEST=1` prints a full environment report without touching your real state.

### Anti-hang design

Every "could wait forever" path has an explicit bound:

- All external calls have **timeouts**; npm calls start a reader thread before `WaitForExit` (older builds called `ReadToEnd` first, which made the timeout useless and could hang for 16 minutes — fixed).
- Upgrades have a **30-minute hard cap**; process kills use `taskkill /F /T` on the whole tree, so no orphaned npm/node processes.
- Upgrade is guarded by a **single-flight lock** so double-clicks can't run concurrently; the tray progress refreshes every 15 seconds.
- Still-running npm processes are cleaned up on exit/stop.
- Interruptible from the tray at any point, and every step finishes cleanly.

## Requirements

- Windows (64-bit; the tray and DPI awareness rely on WinForms)
- The `csc.exe` bundled with .NET Framework 4.x (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`)
- Node.js (with `npx`), and dsh must have been installed at least once (the cache directory must contain `@deepseek-ai/dsh`)
- Optional: `pnpm` (dsh's `plugin` subcommand needs it; **the launcher works without it**, since plugin management is implemented at file level)

## Install

No installer — download and run. Two options:

**A. Grab the binary (recommended for users)**

Download `launcher-v0.6.7.exe` from [Releases](https://github.com/DJL606/dsh-launcher/releases) and double-click it from any directory. It does **not** need to live in the project folder, and it writes **no files** next to the exe.

> The exe is not code-signed, so Windows SmartScreen may warn about an unknown publisher. The source and build script are both in this repo, so you can build and verify it yourself.

### Security note (please read)

- The launcher writes the **tokenized local URL** into `%LOCALAPPDATA%\DeepSeekLauncher\launcher.log` (it is needed for troubleshooting). That token grants access to your local dsh service: **do not publish that log** (in issues, posts or repos). Redact the `?token=...` part before sharing.
- The service only listens on `127.0.0.1`; it is not exposed to your network. If you change the bind address or add port forwarding, add access control yourself.
- Logs also contain your file paths and plugin list; this repo's docs are redacted, so do the same when you post your own logs.

**B. Build it yourself (recommended if you want to modify the code)**

See [Build](#build) below.

## Usage

1. Double-click `launcher-v0.6.7.exe`.
2. Once the service is ready, the dsh page opens in your default browser and a tray icon appears.
3. To stop: right-click the tray icon → **Stop service and exit** (closing the entire browser also stops it).

State and logs live in `%LOCALAPPDATA%\DeepSeekLauncher\` — **nothing is written next to the exe**:

| File | Purpose |
|---|---|
| `launcher.log` | Launcher and server log (`SVR>` = server stdout, `SVR!` = server stderr) |
| `version.txt` | Version-decision state (current version / declined versions / service record) |
| `detached-server.log` | The server's own output when started through the detached path |

The first time you run it on a machine that already has a dsh service, it asks **once** whether to take over the service on port 3080 (older builds never wrote a service record). After you answer "yes", it won't ask again.

## Build

Double-click the build script for the version you want, e.g.:

```bat
build-v067.cmd
```

Success shows `csc_exit=0` and produces `launcher-v067.exe`; if `DeepSeek一键启动-v0.6.7.exe` already exists in the folder it is refreshed too.

Equivalent manual command:

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ ^
  /codepage:65001 /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:launcher-v067.exe launcher.cs
```

> Note: every `.cmd` in this folder **must stay pure ASCII**. `cmd.exe` reads scripts using the system OEM code page (GBK/936 on Chinese systems), so UTF-8 Chinese comments turn into garbage and break the following commands.
>
> `app.ico` is a binary icon and may not be present in the repo. The build script only adds the icon/manifest arguments when those files exist — without them you simply get no custom icon (drop any `app.ico` into the project root to use one).

## Configuration & self-test hooks

Self-test (read-only, does not start the service):

```bat
set DSH_LAUNCHER_SELFTEST=1
launcher-v067.exe
```

The report is written to `%TEMP%\dsh-launcher-selftest.log` and covers: version, pnpm detection, profile and plugin-compatibility inventory, classification of the process listening on port 3080, update candidates, and the exit-branch decision.

Other hidden hooks:

| Variable | Effect |
|---|---|
| `DSH_LAUNCHER_PROFILES_ROOT` | Point the profile root elsewhere (for isolated tests) |
| `DSH_LAUNCHER_REPAIR_TEST` (+`_PLUGIN`/`_ID`) | Exercise the whole "file-level plugin disable" path on a **temporary copy** |
| `DSH_LAUNCHER_AUTOSTOP_MS` | Automatically run "stop → exit" after N ms, for unattended verification (⚠️ this will stop a running service) |
| `DSH_LAUNCHER_STATE_FILE` | Redirect the state file (pairs with the self-test so your real state is untouched) |

## Troubleshooting

- **Service won't start; log contains `plugin tree failed to load`** — the pre-launch plugin check names the offending plugin; use tray → "Plugin compatibility check…" to re-check at any time.
- **All page buttons dead / images won't send (`Failed to fetch`)** — close **all** old tabs first, then reopen. Tabs restored from a browser session run the old front-end bundle, and old endpoint names were renamed in the new version (e.g. `attachments/upload` → `fileUploads/upload`). Also check the launcher log's "API self-check" verdict.
- **Sending an image says the current model doesn't support images** — a model capability issue, not a bug. Switch to a vision-capable model (e.g. `deepseek-v4-flash-vision-exp`).
- **Launcher log says `pnpm=<none>`** — pnpm isn't installed, so dsh's `dsh plugin ...` commands are unavailable; the launcher sidesteps this with file-level plugin management.
- **dsh reports `timed out waiting for the writer lock`** — lock files left over from an unclean exit. Delete `%USERPROFILE%\.dsh\.credentials.yaml.lock` and `settings.yaml.lock`.

## Repository layout

```
launcher.cs                     Launcher source (single file, no third-party deps)
build-v067.cmd                  Build script (pure ASCII)
app.manifest                    DPI-awareness manifest
使用说明.txt                     User manual (per-version changes and measured results)
v0.6.7-改动与验证记录.txt        This version's changes + verification evidence + rollback locations
v0.6.5-修复与验证记录.txt        Previous version's record (8 measured bugs, root causes and fixes)
LICENSE                         Full official CC BY-NC 4.0 text
README-en.md                    This file (English)
```

> Release assets use ASCII names (e.g. `launcher-v0.6.7.exe`, `USAGE-zh-CN.txt`) because GitHub strips non-ASCII characters from asset names — so asset names don't map one-to-one onto the file names above. See the [Releases page](https://github.com/DJL606/dsh-launcher/releases) for the mapping.

## License

**Creative Commons Attribution-NonCommercial 4.0 International** (CC BY-NC 4.0):

- ✅ Free to use, modify and share — **with attribution** and a copy of the license
- ❌ **Commercial use is not permitted**
- Full text: [`LICENSE`](LICENSE) (official text)

> GitHub shows the license label as `Other` because GitHub's official license library no longer carries the CC non-commercial templates (`GET /licenses/cc-by-nc-4.0` returns 404). This does not affect the `LICENSE` file itself.

## Disclaimer

- This is an **unofficial** tool, not affiliated with DeepSeek in any way.
- Provided "as is", without warranty of any kind, express or implied.
- Be aware that the launcher **reads and modifies** dsh profile configuration and plugin directories. Before every change it makes a full backup under `.backup-<timestamp>\`, so the change can be rolled back directory by directory.
