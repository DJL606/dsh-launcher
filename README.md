# 如同看见原子弹爆炸 · dsh-launcher

> DeepSeek Harness（dsh）一键启动器 —— 单文件 C#，双击即用；把"服务起不来 / 页面打不开 / 插件版本错配"这些坑都堵在启动阶段。

一个给 **DeepSeek Harness**（`dsh`）用的 Windows 启动器：隐藏启动本机的 dsh web 服务、用带 token 的地址打开页面、常驻系统托盘，并在启动前后替你做插件兼容预检、更新检查与故障自愈。

> 非官方项目，与 DeepSeek 官方无关。仓库名是随手起的玩笑名，功能上就是 dsh 启动器。

---

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

## 环境要求

- Windows（64 位；托盘与 DPI 感知依赖 WinForms）
- .NET Framework 4.x 自带的 `csc.exe`（`C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`）
- Node.js（含 `npx`），且本机已装过 dsh（缓存目录里要有 `@deepseek-ai/dsh`）
- 可选：`pnpm`（dsh 的 `plugin` 子命令依赖它；**没有也能用本启动器**，插件增删走文件级实现）

## 编译

双击对应版本的构建脚本，例如：

```bat
build-v067.cmd
```

窗口里出现 `csc_exit=0` 即成功，产出 `launcher-v067.exe`；若目录里已存在 `DeepSeek一键启动-v0.6.7.exe` 会一并刷新。

> 注意：本目录下所有 `.cmd` **必须保持纯 ASCII**。`cmd.exe` 按系统 OEM 代码页（中文系统 GBK/936）读取脚本，UTF-8 的中文注释会被解成乱码并把后续命令拆坏。
>
> `app.ico` 是二进制图标，仓库可能不含它；构建脚本已做成"图标/清单存在才加参数"，缺失时只是没有自定义图标（把任意 `app.ico` 放到项目根目录即可用它）。

## 使用

1. 双击 `DeepSeek一键启动-v0.6.7.exe`
2. 服务就绪后会自动用默认浏览器打开 dsh 页面；托盘出现图标
3. 结束时：右键托盘 → **停止服务并退出**（关掉整个浏览器也会自动停止）

状态与日志都在 `%LOCALAPPDATA%\DeepSeekLauncher\`，**不在 exe 所在目录生成任何文件**：

| 文件 | 用途 |
|---|---|
| `launcher.log` | 启动器与服务器的日志（`SVR>` 服务器 stdout，`SVR!` 服务器 stderr） |
| `version.txt` | 版本决策状态（当前版本 / 已拒绝的版本 / 服务记录） |

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
| `DSH_LAUNCHER_AUTOSTOP_MS` | 到点自动走"停止 → 退出"，用于无人值守验证 |
| `DSH_LAUNCHER_STATE_FILE` | 把状态文件重定向（配合自检，不碰真实状态） |

## 排障速查

- **服务起不来，日志里有 `plugin tree failed to load`**：启动前的插件预检会直接指出是哪个插件；托盘"插件兼容检查…"可随时复查。
- **页面按钮全废 / 图片发不出去（`Failed to fetch`）**：先关掉**所有**旧标签页再重开（浏览器会话恢复出的旧标签页跑的是旧版前端；旧接口名在新版已改名，例如 `attachments/upload` → `fileUploads/upload`）。也可先看启动器日志里的"接口自检"结论。
- **发图片提示"当前模型不支持图片"**：模型能力问题，不是故障 —— 切到支持视觉的模型（如 `deepseek-v4-flash-vision-exp`）。
- **启动器日志里 `pnpm=<none>`**：本机没装 pnpm，所以 dsh 的 `dsh plugin ...` 命令不可用 —— 本启动器改用文件级插件管理绕开它。
- **dsh 报 `timed out waiting for the writer lock`**：上次异常退出残留的锁文件。删掉 `%USERPROFILE%\.dsh\.credentials.yaml.lock` 与 `settings.yaml.lock` 即可。

## 仓库内容

```
launcher.cs                     启动器源码（单文件，无第三方依赖）
build-v067.cmd                  构建脚本（纯 ASCII）
app.manifest                    DPI 感知清单
使用说明.txt                     用户手册（按版本记录每一项改动与实测结果）
v0.6.7-改动与验证记录.txt        本版改动 + 验证证据 + 备份/回滚位置
v0.6.5-修复与验证记录.txt        上一版记录（含 8 个实测 bug 的定位与修法）
LICENSE                         CC BY-NC 4.0
```

## 许可

本项目采用 **Creative Commons Attribution-NonCommercial 4.0 International（CC BY-NC 4.0，署名—非商业性使用 4.0 国际）**：

- ✅ 可自由使用、修改、分享（需署名）
- ❌ **不得用于商业用途**
- 协议全文见 [`LICENSE`](./LICENSE)（官方文本）

## 免责声明

- 本项目为非官方工具，与 DeepSeek 官方无任何关联。
- 软件按"现状"提供，不附带任何明示或暗示的担保。使用前请知悉：启动器会读改 dsh 的 profile 配置（改动前一律整份备份到 `.backup-<时间戳>\`）。
