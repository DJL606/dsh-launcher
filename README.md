<!-- ANCHOR:zh -->
<h1 align="center">dsh-launcher</h1>

<div align="center">

**Windows 上启动 [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh)（`dsh`）的一键启动器**<br>
**A one-click Windows launcher for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (`dsh`)**

单文件 C# · 无第三方依赖 · 托盘常驻<br>
Single-file C# · no third-party dependencies · lives in the system tray

[![License: CC BY-NC 4.0](https://img.shields.io/badge/License-CC%20BY--NC%204.0-lightgrey.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6.svg)](#-环境要求--requirements)
[![Language: C%23](https://img.shields.io/badge/Language-C%23-239120.svg)](#-编译--build)
[![Release: v0.6.7](https://img.shields.io/github/v/release/DJL606/dsh-launcher?label=Release)](https://github.com/DJL606/dsh-launcher/releases)

**语言 / Language：** **中文** ｜ [English](#anchor-en)

</div>

---

## 目录 / Table of contents

| 中文 | English |
|---|---|
| [这是什么](#这是什么) | [What it is](#en-what-it-is) |
| [它解决什么问题](#它解决什么问题) | [Problems it solves](#en-problems-it-solves) |
| [功能](#功能) | [Features](#en-features) |
| [环境要求](#环境要求) | [Requirements](#en-requirements) |
| [安装](#安装) | [Install](#en-install) |
| [使用](#使用) | [Usage](#en-usage) |
| [编译](#编译) | [Build](#en-build) |
| [配置与自检钩子](#配置与自检钩子) | [Configuration & self-test hooks](#en-configuration--self-test-hooks) |
| [排障速查](#排障速查) | [Troubleshooting](#en-troubleshooting) |
| [仓库结构](#仓库结构) | [Repository layout](#en-repository-layout) |
| [许可](#许可) | [License](#en-license) |
| [免责声明](#免责声明) | [Disclaimer](#en-disclaimer) |

---

## 这是什么

一个给 **DeepSeek Harness**（`dsh`）用的 Windows 启动器：隐藏启动本机的 dsh web 服务、用**带 token 的地址**打开页面、常驻系统托盘，并在启动前后替你做插件兼容预检、更新检查与故障自愈。

它把"服务起不来 / 页面打不开 / 插件版本错配"这些坑都堵在启动阶段，而不是让你在浏览器里对着 `Failed to fetch` 猜。

> **非官方项目**，与 DeepSeek 官方无关。仓库名是随手起的玩笑名，功能上就是 dsh 启动器。

## 它解决什么问题

dsh 本身是个强大的 Agent 运行时，但日常用起来有几类"看起来像坏了"的故障，本启动器把它们逐个处理：

| 现象 | 真实原因 | 启动器的处理 |
|---|---|---|
| 服务完全起不来，报 `plugin tree failed to load` | 插件按**具体 dsh 版本**声明兼容范围，错配时在 profile 装载阶段抛异常 | 扫描服务器输出识别该错误，解析出插件名与 loader 条目，立即弹窗修复；提供**文件级禁用**（改 3 处配置 + 自动备份），不依赖 pnpm |
| 页面能打开，但按钮点了没反应 / 图片发不出去（`Failed to fetch`） | ①服务根本没在跑（浏览器里是缓存页）；②浏览器标签页跑的是**旧版前端**，调用的接口在新版已改名 | 自动拉起并自检；用带 token 的地址开页面；接口自检失败时给气泡提示 |
| 重启多少次都还在跑旧版本 | 3080 上是被强杀的旧启动器留下的孤儿服务，谁都停不掉 | 用 netstat 找监听进程 + **服务记录**（PID/启动时间/版本）精确判定，问一次是否换版本重启 |
| 升级后 dsh 起不来 | 装了新版本但插件还没适配 | 启动前插件兼容预检（复刻插件的 `assertHarnessCompatibility` 语义 + 扫描插件代码判断是否真有加载期断言），区分 `fatal` / `risky`，只有 fatal 才拦 |
| 从某些宿主环境里启动 dsh 直接崩 | 宿主注入的 `NODE_OPTIONS` 语言 shim 带 safe-delete 守卫，拦下 dsh 删自己的 `.lock` 文件 | 启动前在命令行里清掉 `NODE_OPTIONS` 与 `CODEBUDDY_SAFE_DELETE_BULK_GUARD` |
| 更新提示把环境搞崩 | 插件新版本只支持更高版本的 dsh（例如要求 `>=0.1.7-rc.1 <0.1.8-0`，而本机是 `0.1.5-rc.3`） | **兼容闸门**：只提示与当前 dsh 相容的插件版本，其余拦下并说明原因 |

## 功能

- **一键启动**：隐藏启动 npx 缓存里的 dsh（不经 npx 解析，秒起、离线可用），服务就绪后才打开浏览器，只开一个标签页
- **带 token 的地址**：抓服务器 stdout 里 `dsh web: http://127.0.0.1:3080/?token=...` 的地址用于打开页面（裸地址会被 token 校验拒绝）
- **托盘常驻**：双击托盘图标重开页面；右键菜单包含
  - 打开 DeepSeek 网页
  - **检查 dsh / 插件更新…** —— 一张勾选菜单，dsh 本体与各插件逐项选择是否更新
  - 插件兼容检查…
  - 切换版本（重启生效）
  - **停止服务并退出**（合并了"停自有服务"与"处理别人的服务"两种情况）
- **更新检查**：启动后自动检查一次（仅在有新版本时弹窗）+ 托盘手动检查
  - dsh 本体 → 后台下载新版本，安装/启动失败**自动回退**，绝不会"升级失败又打不开"
  - 插件 → **文件级安装**：`npm pack` 下载 → 解包 → 覆盖到 profile 的 `node_modules`（先整目录备份，可回滚），无需 pnpm
  - 自动跳过 `link:`/`file:` 规格与符号链接型插件（本地开发包不动）
- **健康自检**：启动后 6 秒用 token→cookie→POST 只读接口验证 client-api 通路，失败留日志 + 气泡
- **停机即退**：关闭整个浏览器（或托盘停止）时自动收掉服务及其进程树，不留孤儿
- **只读自检钩子**：`DSH_LAUNCHER_SELFTEST=1` 输出完整环境体检报告，不动你的真实状态

### 防卡死设计

任何"会一直等下去"的路径都被显式设了上限：

- 所有外部调用**都有超时**；npm 调用先起读取线程再 `WaitForExit`（早期版本因先 `ReadToEnd` 导致超时形同虚设、能卡 16 分钟，已修）
- 升级有 **30 分钟硬上限**；杀进程用 `taskkill /F /T` 整树，不留 npm/node 孤儿
- 升级**单飞锁**，重复点击不会并发；托盘进度每 15 秒刷新
- 退出/停止时清理仍在跑的 npm
- 托盘随时可中断，走到任何一步都能干净收尾

## 环境要求

- Windows（64 位；托盘与 DPI 感知依赖 WinForms）
- .NET Framework 4.x 自带的 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`）
- Node.js（含 `npx`），且本机已装过 dsh（缓存目录里要有 `@deepseek-ai/dsh`）
- 可选：`pnpm`（dsh 的 `plugin` 子命令依赖它；**没有也能用本启动器**，插件增删走文件级实现）

## 安装

没有安装程序，下载即用。两种方式：

**A. 直接下二进制（推荐给使用者）**

到 [Releases](https://github.com/DJL606/dsh-launcher/releases) 下载 `launcher-v0.6.7.exe`，放到任意目录双击即可。**不需要**放到项目目录里，也不会在 exe 所在目录生成任何文件。

> 该 exe 未做代码签名，Windows SmartScreen 可能提示"未知发布者"；源码与构建脚本都在本仓库，可自行编译核对。

### 安全须知（必读）

- 启动器会把**带 token 的本地地址**写进 `%LOCALAPPDATA%\DeepSeekLauncher\launcher.log`（排障需要它）。该 token 等同于对你本机 dsh 服务的访问权：**不要把这份日志公开上传**（帖子/问题/仓库），要发给别人请先把 `?token=...` 部分打码。
- 服务仅监听 `127.0.0.1`，不对你的网络开放；若你自己改成 `0.0.0.0` 或做端口转发，请自行加访问控制。
- 日志里还会出现你的路径与插件清单；本仓库的文档已脱敏，你发自己的日志时请同样处理。

**B. 自行编译（推荐给想改代码的人）**

见下方[编译](#编译)一节。

## 使用

1. 双击 `launcher-v0.6.7.exe`
2. 服务就绪后会自动用默认浏览器打开 dsh 页面；托盘出现图标
3. 结束时：右键托盘 → **停止服务并退出**（关掉整个浏览器也会自动停止）

状态与日志都在 `%LOCALAPPDATA%\DeepSeekLauncher\`，**不在 exe 所在目录生成任何文件**：

| 文件 | 用途 |
|---|---|
| `launcher.log` | 启动器与服务器的日志（`SVR>` 服务器 stdout，`SVR!` 服务器 stderr） |
| `version.txt` | 版本决策状态（当前版本 / 已拒绝的版本 / 服务记录） |
| `detached-server.log` | 脱离启动路径下服务器自身的输出 |

首次在已有 dsh 服务的机器上运行时，会**问一次**是否接管 3080 上那个服务（因为旧版本没写过服务记录）；点"是"之后就不再打扰。

## 编译

双击对应版本的构建脚本，例如：

```bat
build-v067.cmd
```

窗口里出现 `csc_exit=0` 即成功，产出 `launcher-v067.exe`；若目录里已存在 `DeepSeek一键启动-v0.6.7.exe` 会一并刷新。

等价的手工命令：

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ ^
  /codepage:65001 /utf8output /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  /out:launcher-v067.exe launcher.cs
```

> 注意：本目录下所有 `.cmd` **必须保持纯 ASCII**。`cmd.exe` 按系统 OEM 代码页（中文系统 GBK/936）读取脚本，UTF-8 的中文注释会被解成乱码并把后续命令拆坏。
>
> `app.ico` 是二进制图标，仓库可能不含它；构建脚本已做成"图标/清单存在才加参数"，缺失时只是没有自定义图标（把任意 `app.ico` 放到项目根目录即可用它）。

## 配置与自检钩子

自检（只读，不启动服务）：

```bat
set DSH_LAUNCHER_SELFTEST=1
launcher-v067.exe
```

报告写在 `%TEMP%\dsh-launcher-selftest.log`，包含：版本、pnpm 检测、profile 与插件兼容清单、3080 端口占用者分类、更新候选、退出分支判定等。

其他隐藏钩子：

| 变量 | 作用 |
|---|---|
| `DSH_LAUNCHER_PROFILES_ROOT` | 把 profile 根目录指到别处（隔离测试用） |
| `DSH_LAUNCHER_REPAIR_TEST` (+`_PLUGIN`/`_ID`) | 在**临时副本**上验证"文件级禁用插件"整条路径 |
| `DSH_LAUNCHER_AUTOSTOP_MS` | 到点自动走"停止 → 退出"，用于无人值守验证（⚠️ 会停掉正在跑的服务） |
| `DSH_LAUNCHER_STATE_FILE` | 把状态文件重定向（配合自检，不碰真实状态） |

## 排障速查

- **服务起不来，日志里有 `plugin tree failed to load`**：启动前的插件预检会直接指出是哪个插件；托盘"插件兼容检查…"可随时复查。
- **页面按钮全废 / 图片发不出去（`Failed to fetch`）**：先关掉**所有**旧标签页再重开（浏览器会话恢复出的旧标签页跑的是旧版前端；旧接口名在新版已改名，例如 `attachments/upload` → `fileUploads/upload`）。也可先看启动器日志里的"接口自检"结论。
- **发图片提示"当前模型不支持图片"**：模型能力问题，不是故障 —— 切到支持视觉的模型（如 `deepseek-v4-flash-vision-exp`）。
- **启动器日志里 `pnpm=<none>`**：本机没装 pnpm，所以 dsh 的 `dsh plugin ...` 命令不可用 —— 本启动器改用文件级插件管理绕开它。
- **dsh 报 `timed out waiting for the writer lock`**：上次异常退出残留的锁文件。删掉 `%USERPROFILE%\.dsh\.credentials.yaml.lock` 与 `settings.yaml.lock` 即可。

## 仓库结构

```
launcher.cs                     启动器源码（单文件，无第三方依赖）
build-v067.cmd                  构建脚本（纯 ASCII）
app.manifest                    DPI 感知清单
使用说明.txt                     用户手册（按版本记录每一项改动与实测结果）
v0.6.7-改动与验证记录.txt        本版改动 + 验证证据 + 备份/回滚位置
v0.6.5-修复与验证记录.txt        上一版记录（含 8 个实测 bug 的定位与修法）
LICENSE                         CC BY-NC 4.0 官方全文
README-en.md                    英文版说明（English-only copy）
```

> Release 里的资产名统一用 ASCII（如 `launcher-v0.6.7.exe`、`USAGE-zh-CN.txt`）：GitHub 会把 Release 资产名里的中文清洗掉，所以附件的命名与仓库内文件名不完全对应，对照表见 [Release 页面](https://github.com/DJL606/dsh-launcher/releases)。

## 许可

本项目采用 **Creative Commons Attribution-NonCommercial 4.0 International**（CC BY-NC 4.0，署名—非商业性使用 4.0 国际）：

- ✅ 可自由使用、修改、分享（**需署名**并保留许可）
- ❌ **不得用于商业用途**
- 协议全文见 [`LICENSE`](./LICENSE)（官方文本）

> GitHub 页面上的许可标签显示为 `Other`：GitHub 官方许可库已移除 CC 的 NC 系模板（`GET /licenses/cc-by-nc-4.0` 返回 404），这不影响 `LICENSE` 文件本身的效力。

## 免责声明

- 本项目为**非官方**工具，与 DeepSeek 官方无任何关联。
- 软件按"现状"提供，不附带任何明示或暗示的担保。
- 使用前请知悉：启动器会**读改** dsh 的 profile 配置与插件目录，改动前一律整份备份到 `.backup-<时间戳>\`，可整目录回滚。

---

<!-- ANCHOR:en -->
<h2 align="center" id="english">English</h2>

<div align="center">

A one-click Windows launcher for **DeepSeek Harness** (`dsh`). Single-file C#, no third-party dependencies, lives in the system tray.

**Language / 语言：** [中文](#anchor-zh) ｜ **English**

</div>

---

## <a id="en-what-it-is"></a>What it is

A Windows launcher for [DeepSeek Harness](https://www.npmjs.com/package/@deepseek-ai/dsh) (`dsh`). It starts the local `dsh web` service hidden, opens the page using the **tokenized URL**, stays resident in the system tray, and handles plugin-compatibility pre-checks, update checks and self-healing around the launch.

Its point is to catch the "service won't start / page won't open / plugin version mismatch" failures **at launch time**, instead of leaving you staring at `Failed to fetch` in the browser.

> **Unofficial project.** Not affiliated with DeepSeek. The repo name is a joke name — functionally this is just a dsh launcher.

## <a id="en-problems-it-solves"></a>Problems it solves

| Symptom | Real cause | What the launcher does |
|---|---|---|
| Service won't start at all; logs say `plugin tree failed to load` | Plugins declare compatibility against a **specific dsh version**; a mismatch throws while the profile is being loaded | Scans the server output for that error, extracts the plugin name and loader entry, and pops a repair dialog; offers **file-level disable** (3 config edits + automatic backup) with no pnpm needed |
| Page opens but buttons do nothing / images won't send (`Failed to fetch`) | ① The service isn't running at all (the browser is showing a cached page); ② the tab is running an **old front-end bundle** calling endpoints that were renamed | Starts the service and health-checks it; opens the page with the tokenized URL; shows a tray balloon when the API self-check fails |
| Old version keeps running no matter how often you restart | An orphaned service on port 3080 left behind by a killed launcher — nothing owns or stops it | Uses `netstat` to find the listener plus a **service record** (PID / start time / version) to decide precisely, then asks once whether to restart on the new version |
| dsh won't start after an upgrade | New version installed but plugins haven't caught up | Pre-launch plugin compatibility check (re-implements the plugins' `assertHarnessCompatibility` semantics and scans plugin code for real load-time assertions), classifies `fatal` vs `risky`, and only blocks on `fatal` |
| dsh crashes immediately when started from certain host environments | The host injects a `NODE_OPTIONS` language shim carrying a safe-delete guard that blocks dsh from deleting its own `.lock` file | Clears `NODE_OPTIONS` and `CODEBUDDY_SAFE_DELETE_BULK_GUARD` on the command line before launching |
| An update prompt wrecks the environment | The new plugin version only supports a newer dsh (e.g. requires `>=0.1.7-rc.1 <0.1.8-0` while the machine runs `0.1.5-rc.3`) | **Compatibility gate**: only offers plugin versions compatible with the installed dsh, and explains why the others are withheld |

## <a id="en-features"></a>Features

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

## <a id="en-requirements"></a>Requirements

- Windows (64-bit; the tray and DPI awareness rely on WinForms)
- The `csc.exe` bundled with .NET Framework 4.x (`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`)
- Node.js (with `npx`), and dsh must have been installed at least once (the cache directory must contain `@deepseek-ai/dsh`)
- Optional: `pnpm` (dsh's `plugin` subcommand needs it; **the launcher works without it**, since plugin management is implemented at file level)

## <a id="en-install"></a>Install

No installer — download and run. Two options:

**A. Grab the binary (recommended for users)**

Download `launcher-v0.6.7.exe` from [Releases](https://github.com/DJL606/dsh-launcher/releases) and double-click it from any directory. It does **not** need to live in the project folder, and it writes **no files** next to the exe.

> The exe is not code-signed, so Windows SmartScreen may warn about an unknown publisher. The source and build script are both in this repo, so you can build and verify it yourself.

**B. Build it yourself (recommended if you want to modify the code)**

See [Build](#en-build) below.

## <a id="en-usage"></a>Usage

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

## <a id="en-build"></a>Build

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

## <a id="en-configuration--self-test-hooks"></a>Configuration & self-test hooks

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

## <a id="en-troubleshooting"></a>Troubleshooting

- **Service won't start; log contains `plugin tree failed to load`** — the pre-launch plugin check names the offending plugin; use tray → "Plugin compatibility check…" to re-check at any time.
- **All page buttons dead / images won't send (`Failed to fetch`)** — close **all** old tabs first, then reopen. Tabs restored from a browser session run the old front-end bundle, and old endpoint names were renamed in the new version (e.g. `attachments/upload` → `fileUploads/upload`). Also check the launcher log's "API self-check" verdict.
- **Sending an image says the current model doesn't support images** — a model capability issue, not a bug. Switch to a vision-capable model (e.g. `deepseek-v4-flash-vision-exp`).
- **Launcher log says `pnpm=<none>`** — pnpm isn't installed, so dsh's `dsh plugin ...` commands are unavailable; the launcher sidesteps this with file-level plugin management.
- **dsh reports `timed out waiting for the writer lock`** — lock files left over from an unclean exit. Delete `%USERPROFILE%\.dsh\.credentials.yaml.lock` and `settings.yaml.lock`.

## <a id="en-repository-layout"></a>Repository layout

```
launcher.cs                     Launcher source (single file, no third-party deps)
build-v067.cmd                  Build script (pure ASCII)
app.manifest                    DPI-awareness manifest
使用说明.txt                     User manual (per-version changes and measured results)
v0.6.7-改动与验证记录.txt        This version's changes + verification evidence + rollback locations
v0.6.5-修复与验证记录.txt        Previous version's record (8 measured bugs, root causes and fixes)
LICENSE                         Full official CC BY-NC 4.0 text
README-en.md                    English-only copy of this README
```

## <a id="en-license"></a>License

**Creative Commons Attribution-NonCommercial 4.0 International** (CC BY-NC 4.0):

- ✅ Free to use, modify and share — **with attribution** and a copy of the license
- ❌ **Commercial use is not permitted**
- Full text: [`LICENSE`](./LICENSE) (official text)

> GitHub shows the license label as `Other` because GitHub's official license library no longer carries the CC non-commercial templates (`GET /licenses/cc-by-nc-4.0` returns 404). This does not affect the `LICENSE` file itself.

## <a id="en-disclaimer"></a>Disclaimer

- This is an **unofficial** tool, not affiliated with DeepSeek in any way.
- Provided "as is", without warranty of any kind, express or implied.
- Be aware that the launcher **reads and modifies** dsh profile configuration and plugin directories. Before every change it makes a full backup under `.backup-<timestamp>\`, so the change can be rolled back directory by directory.
