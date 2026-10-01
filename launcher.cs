// ============================================================
//  DeepSeek Harness 一键启动器 v0.6.7
//  功能：隐藏启动本机已安装的 dsh（直接调用 npx 缓存里的 dsh.cmd，
//  不经 npx：秒起、离线可用）并加 --no-open；服务就绪后由本程序用
//  默认浏览器打开网页（只开一个 DeepSeek 标签页；若浏览器恢复出的
//  历史会话中已有该页则不重复打开）；关闭整个浏览器自动停止，或托盘
//  右键"停止服务并退出"。
//  版本与升级：启动时查 npm latest，有新版本且**确认可安装**才弹窗
//  询问；选"是"则联网安装新版本，安装/启动失败**自动回退**到原版本
//  （绝不会出现"升级失败又打不开"）；拒绝后 12 小时内不再询问；
//  托盘可随时"检查更新…"。
//  文件位置：状态与日志写在 %LOCALAPPDATA%\DeepSeekLauncher\，
//  不在 exe 所在目录（尤其桌面）生成任何文件。
//  响应性：UI 线程跑真正的消息循环，监视逻辑放后台线程 → 托盘不卡。
//  防卡死（v0.6.3）：
//   1) npm 命令的超时**真的生效**（先起读线程再 WaitForExit；旧版先
//      ReadToEnd 会把超时变成空等，升级下载 16 分钟时表现为"永远没结果"）；
//   2) 超时/退出时杀**整棵进程树**（cmd → npm.cmd → node），只杀 cmd 会
//      留下 npm/node 孤儿继续占网占盘；
//   3) 升级安装**单飞**：重复点"检查更新"不会起第二个 npm 抢同一目录锁；
//   4) 下载中托盘显示已用时间（每 15 秒刷新），随时可"停止服务并退出"，
//      停止时会连正在跑的 npm 一起收掉。
//  版本真正切换（v0.6.4）：
//   症结：启动器原本"3080 已有服务就直接复用"。若那个服务是被强杀的旧
//   启动器留下的**孤儿进程**，谁都停不掉它 —— 于是重启多少次都在跑旧版本，
//   用户记录的新版本永远不生效（实测：记录 rc.3，实际一直在跑 rc.7）。
//   现在：用 netstat 找出监听 3080 的 PID，若它的启动时间早于本程序（=不是
//   本程序启动的），就明确询问"是否结束它并用记录版本重新启动"；选择结束则
//   taskkill 整棵树、有界等待端口释放，再按记录版本启动。托盘也有
//   "结束 3080 服务并退出"用于手动接管。
//  ===== v0.6.6：服务记录，替掉"比我早启动就是孤儿"的启发式 =====
//   问题：v0.6.4/v0.6.5 用"监听进程比我启动得早"判断孤儿服务。它会把
//   **本程序自己上一次启动的服务**也判成孤儿（启动器重启 → 服务比新进程早），
//   于是每次重启都多弹一次"是否结束它"的问询，而且自检里 leftover_service
//   对一个完全健康的自有服务也会报 True。
//   现在：本程序启动服务成功后，把"这是我起的"写进状态文件 ——
//   service_pid（监听 PID）、service_start（该进程启动时间，防 PID 复用）、
//   service_version（启动时用的版本）。下次启动先用这三要素精确比对：
//     · 对得上且版本一致        → 直接复用（不弹窗）
//     · 对得上但版本已不同      → 问"是否换成记录版本重启"（托盘切版本后正好用上）
//     · 对不上（PID/时间不符）  → 才回退到"孤儿"启发式去问
//   本程序把自己的服务停掉时会清掉该记录，不留失效 PID。
//  ===== v0.6.7：一个出口 + 一处更新入口 + 启动自检（本轮实修）=====
//   1) **托盘只留一个退出项**："停止服务并退出"。旧版有两个
//      （"停止服务并退出"和"结束 3080 服务并退出…"），分别只处理
//      "自己起的服务"和"别人的服务"，用户得先判断该点哪个 —— 这次点错
//      就会留下一个谁都不管的服务。现在合并成一个：先看自己有没有服务，
//      有就常规停止；没有而 3080 上另有服务，就一次问清"结束它并退出"。
//   2) **更新检查合并成一张勾选菜单**：旧版只查 dsh 本体，插件版本全靠
//      用户自己盯。现在启动后在后台同时查 dsh latest 与每个 profile 里
//      第三方插件的最新版，做一个可多选的勾选列表（每项显示
//      当前版本 → 可用版本），点"更新选中项"逐个执行；托盘"检查更新…"
//      走同一张菜单。开发用 link:/符号链接插件（如 dsh-bridge-browser）
//      会被自动跳过，不去动本地开发检查点。
//   3) **插件更新走文件级安装**（不需要 pnpm）：npm pack 下载 → 解包 →
//      覆盖到 profile 的 node_modules（先整目录备份到 .backup-<时间戳>，
//      失败可回滚），并同步 package.json 里的版本号。
//   4) **启动环境净化**：启动 dsh 前在命令行里清掉 NODE_OPTIONS 与
//      CODEBUDDY_SAFE_DELETE_BULK_GUARD。实测（本机）：若启动器自身从
//      WorkBuddy 之类的宿主环境里被拉起，会继承一个 node 语言 shim，
//      它拦截 dsh 内部的锁文件删除 → dsh 直接启动失败
//      （`[safe-delete][SAFE_DELETE_BULK_CONFIRM_REQUIRED]`）。
//      净化后无论启动器从哪来，dsh 都能正常起。
//   5) **服务健康自检**（启动后、仅记日志）：用带 token 的地址换 cookie，
//      再 POST 一个只读的 client-api 接口。服务在跑但接口层没挂载时
//      （例如插件树半装载、或浏览器拿着旧版前端 bundle 调已改名的接口），
//      这一步会在日志里留下明确证据，省掉"页面能开但按钮全废"的盲猜。
//  ===== v0.6.5：插件不兼容与"服务在跑但页面打不开" =====
//   背景（实测踩到的坑）：插件是按具体 dsh 版本声明兼容范围的，版本错配时
//   插件会在 profile 装载阶段直接抛异常，**整个服务起不来**。旧版启动器只能
//   干等 180 秒后报一句"启动超时"；而且插件报错里建议的
//   `dsh plugin remove` 依赖 pnpm，本机没装 pnpm，照做也执行不了。
//   1) 启动失败**快速判定**：扫描服务器输出，识别
//      "plugin tree failed to load / failed to apply loader entry <id> (<插件>)"
//      并解析出插件名与 loader entry id，立即（不再等满 180 秒）弹出修复询问；
//   2) **文件级自动禁用**（不需要 pnpm）：改 profile 配置前先备份到
//      .backup-<时间戳>\，再删 package.json 里的依赖与 bundles 条目、清理
//      pnpm-workspace.yaml 里的对应行、注释掉 cordis.patch.yml 里该插件的
//      补丁条目（并保证补丁文件仍是顶层数组 —— 全注释掉时补 `[]`，
//      否则 dsh 会报 "must be a top-level YAML array" 直接启动失败）；
//      然后用记录版本自动重启服务，用户只需点一次"是"；
//   3) **插件兼容预检**：启动前按 node 的真实解析规则（复刻插件自己的
//      assertHarnessCompatibility：5 个 harness 包版本必须一致且在该插件
//      compatibility.json 的 supportedHosts 里）判定第三方插件是否兼容，
//      不兼容就先问"禁用插件启动 / 仍要试试 / 退出"，不再靠崩溃才发现；
//   4) **打开真正能用的地址**：dsh 启动时会打印带 token 的地址，本版把它
//      抓出来用于打开浏览器（裸地址可能被 token 校验拒绝）；
//   5) **半死服务识别**：3080 有 node 进程在监听但 HTTP 完全不响应时，
//      不再静默复用（那就是"页面 Failed to fetch"的来源），而是询问
//      "结束它并重新启动"；
//   6) **健康服务不再被误判**：dsh 对无 token 请求返回 401，旧版探测把
//      401 当异常 → 误报"3080 被其他程序占用"；本版把"HTTP 层有响应 +
//      占用者是 node"视为 dsh 服务；
//   7) 启动等待循环响应"停止"请求（旧版点了停止仍要等满 180 秒）；
//   8) 自检新增 pnpm 检测、profile/插件兼容清单、端口占用者分类。
//  特性：端口预检、页面去重、DPI 感知、托盘图标按 DPI 取原生帧、
//  热更新（替换 exe 即可）、自检（DSH_LAUNCHER_SELFTEST=1 →
//  %TEMP%\dsh-launcher-selftest.log，只读探测、无 UI 副作用）
//  编译：
//    %windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo ^
//      /target:winexe /optimize+ /codepage:65001 /utf8output ^
//      /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
//      /win32icon:app.ico /resource:app.ico,dsh_app.ico ^
//      /win32manifest:app.manifest /out:DeepSeek一键启动.exe launcher.cs
// ============================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

    [assembly: System.Reflection.AssemblyTitle("DeepSeek 一键启动器")]
[assembly: System.Reflection.AssemblyProduct("DeepSeek 一键启动器")]
[assembly: System.Reflection.AssemblyDescription("直接启动本机已装的 dsh，自动打开网页，关闭浏览器或托盘操作时自动停止服务；启动时检查新版本并在确认可安装后询问是否升级")]
[assembly: System.Reflection.AssemblyVersion("0.7.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.7.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("v0.7.0")]

internal static class DshLauncher
{
    private const string Url = "http://127.0.0.1:3080/";
    private const int Port = 3080;
    private const string PageTitleMarker = "DeepSeek Harness";
    private const int ReadyTimeoutSec = 180;
    private const string ExeName = "DeepSeek一键启动";
    private const string TrayResource = "dsh_app.ico";
    // v0.7.0：本启动器版本（日志、User-Agent、下载器统一引用这里，避免多处硬编码漂移）
    private const string LauncherVersion = "0.7.0";

    private static volatile bool stopRequested;
    private static NotifyIcon trayIcon;
    private static Icon trayIconObject;

    // 状态与日志统一放在 %LOCALAPPDATA%\DeepSeekLauncher\：
    // 不在 exe 所在目录（尤其是桌面）生成任何文件。
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekLauncher");
    // 注意：VersionFile 不是 readonly —— 自检可用 DSH_LAUNCHER_STATE_FILE 把它
    // 重定向到临时文件，从而在**不碰用户真实状态**的前提下验证读写/判定逻辑。
    private static string VersionFile = Path.Combine(AppDataDir, "version.txt");
    private static readonly string LogFile = Path.Combine(AppDataDir, "launcher.log");
    private static readonly object LogLock = new object();

    private const string AppTitle = "DeepSeek 一键启动";

    // 统一弹窗助手（标题固定）；后台线程调用时回投到 UI 线程
    private static void Msg(string text, MessageBoxIcon icon)
    {
        Ui(delegate { MessageBox.Show(text, AppTitle, MessageBoxButtons.OK, icon); });
    }

    [STAThread]
    private static int Main()
    {
        SetProcessDpiAware();
        Application.EnableVisualStyles();
        try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        uiThreadId = Thread.CurrentThread.ManagedThreadId;

        // v0.7.0：全局异常兜底 —— 任何未处理异常都先落日志再决定是否退出，
        // 绝不出现“双击后一闪而过、什么证据都没留下”的崩溃。
        Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
        {
            Log("未处理的 UI 线程异常: " + e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
        {
            Log("未处理的应用域异常: " + e.ExceptionObject);
        };

        // 自检专用：把状态文件重定向到临时路径（仅在自检模式下生效），
        // 这样"服务记录"的读写与判定可以在不碰用户真实 version.txt 的情况下被验证。
        if (Environment.GetEnvironmentVariable("DSH_LAUNCHER_SELFTEST") == "1")
        {
            string stateOverride = Environment.GetEnvironmentVariable("DSH_LAUNCHER_STATE_FILE");
            if (!string.IsNullOrEmpty(stateOverride)) VersionFile = stateOverride;
        }

        EnsureAppDataDir();
        MigrateLegacyState();
        PruneBackups(8);   // v0.7.0：只清理插件更新自动产生的备份，保留最近 8 份

        // 自检模式：无托盘、无浏览器、无服务器等副作用
        if (Environment.GetEnvironmentVariable("DSH_LAUNCHER_SELFTEST") == "1")
        {
            return RunSelfTest();
        }

        bool createdNew;
        instanceMutex = new Mutex(true, "DshWebLauncher_SingleInstance", out createdNew);
        if (!createdNew)
        {
            Msg(AlreadyRunningText, MessageBoxIcon.Information);
            return 0;
        }

        Log("=== 启动器开始运行（v" + LauncherVersion + "）===");
        return Run();
    }

    // ================= 主流程 =================

    private static int Run()
    {
        var sw = Stopwatch.StartNew();

        // 0. 环境检查
        string missing = MissingNodeNpx();
        if (missing != null)
        {
            Msg(
                "未检测到 " + missing + "。\n\nDeepSeek Harness 需要 Node.js（自带 npx）。\n" +
                "请到 https://nodejs.org 下载安装 LTS 版本，安装完成后再启动本程序。", MessageBoxIcon.Error);
            Log("环境检查失败: 缺少 " + missing);
            return 1;
        }

        // 0.5 版本决策：启动时询问是否升级（启动命令固定版本，不再静默升级）
        pinnedVersion = ResolveVersionAtStartup();

        // 0.9 插件兼容预检（v0.6.5）：第三方插件与当前 dsh 版本错配时，服务会在
        //     装载阶段直接抛错、整个起不来。与其等它崩，不如启动前就问清楚。
        if (!PreflightPluginCheck())
        {
            Log("用户选择自行处理插件问题，启动器退出");
            return 1;
        }

        // 1. 端口预检（v0.6.5：区分「没人监听 / 健康的 dsh / 半死的 dsh / 别的程序」）
        int ownerPid;
        string ownerProc, ownerTitle;
        int owner = ClassifyPortOwner(out ownerPid, out ownerProc, out ownerTitle);
        bool listening = owner != PortOwnerNone;
        if (owner == PortOwnerDsh)
        {
            // v0.6.6：先用"本程序上次启动的服务记录"（PID + 启动时间 + 版本）精确判定，
            // 不再只靠"比我早启动就是孤儿"的启发式 —— 那个启发式会把本程序自己
            // 上一次启动的服务也判成孤儿，于是每次重启都要多问一句。
            if (IsRecordedCurrentService(ownerPid))
            {
                if (!string.IsNullOrEmpty(serviceVersion) && !string.IsNullOrEmpty(pinnedVersion)
                    && serviceVersion != pinnedVersion)
                {
                    // 服务是本程序起的，但版本已不是记录版本（例如刚用托盘切了版本）
                    Log("3080 是本程序启动的服务，但版本为 " + serviceVersion
                        + "，记录版本已改为 " + pinnedVersion + "，询问是否更换");
                    if (AskSwitchRecordedService(ownerPid))
                    {
                        KillProcessTree(ownerPid);
                        if (WaitPortFree(Port, 20000))
                        {
                            listening = false;
                            serviceReplaced = true;
                            Log("已结束旧版本服务，准备按 " + pinnedVersion + " 启动");
                        }
                        else
                        {
                            Msg("结束服务后 3080 仍未释放（可能有另一个进程在监听）。\n\n" +
                                "将继续复用现有服务。", MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        Log("用户选择继续使用 " + serviceVersion);
                    }
                }
                else
                {
                    Log("3080 是本程序上次启动的同一个服务（PID " + ownerPid + "，版本 "
                        + (serviceVersion ?? "未记录") + "），直接复用");
                }
            }
            else if (IsLeftoverService(ownerPid))
            {
                if (AskReplaceLeftover(ownerPid))
                {
                    Log("用户确认结束 3080 上的旧服务（PID " + ownerPid + "），改用 "
                        + (pinnedVersion ?? "已记录版本") + " 启动");
                    KillProcessTree(ownerPid);
                    if (WaitPortFree(Port, 20000))
                    {
                        Log("3080 已释放，按记录版本启动");
                        listening = false;
                        serviceReplaced = true;
                    }
                    else
                    {
                        Msg("结束旧服务后 3080 仍未释放（可能有另一个进程在监听）。\n\n" +
                            "将继续复用现有服务；版本可能仍是旧的。", MessageBoxIcon.Warning);
                        Log("旧服务结束但端口未释放");
                    }
                }
                else
                {
                    Log("3080 上是不属于本程序的旧服务（PID " + ownerPid + "），用户选择继续复用");
                }
            }
            if (listening) Log("3080 已有 DeepSeek 服务，直接复用"
                + (ownerPid > 0 ? "（PID " + ownerPid + "）" : "")
                + (string.IsNullOrEmpty(ownerTitle) ? "" : "，探测标题：" + ownerTitle));
        }
        else if (owner == PortOwnerDshDead)
        {
            // v0.6.5：端口在监听、占用者是 node，但 HTTP 完全没响应 —— 这正是
            // "服务看着在跑、页面却 Failed to fetch"的来源。旧版会静默复用它，
            // 于是用户重启多少次都还是坏页面。
            Log("3080 上的服务无响应（PID " + ownerPid + "，进程 " + ownerProc + "），询问是否重启");
            if (AskRestartDeadService(ownerPid))
            {
                KillProcessTree(ownerPid);
                if (WaitPortFree(Port, 20000))
                {
                    listening = false;
                    serviceReplaced = true;
                    Log("已结束无响应的服务，准备按记录版本重启");
                }
                else
                {
                    Msg("结束无响应的服务后 3080 仍未释放，无法重新启动。\n\n" +
                        "请稍后在命令提示符中手动运行：\nnpx @deepseek-ai/dsh web", MessageBoxIcon.Warning);
                    return 1;
                }
            }
            else
            {
                Msg("已保持现状。\n\n3080 上的服务没有响应，页面会打不开或报 Failed to fetch；" +
                    "本次不启动新服务。需要时请再次双击本程序。", MessageBoxIcon.Information);
                return 1;
            }
        }
        else if (owner == PortOwnerOther)
        {
            Msg(
                "端口 3080 已被其他程序占用" +
                (string.IsNullOrEmpty(ownerTitle) ? "。" : "（页面标题：" + ownerTitle + "）。") +
                "\n\n打开的可能不是 DeepSeek Harness 页面。\n建议先关闭占用 3080 的程序再重试。" +
                "\n\n本程序不会重复启动服务器，也不会结束占用端口的程序。", MessageBoxIcon.Warning);
            Log("3080 被非 DeepSeek 程序占用: pid=" + ownerPid + " proc=" + (ownerProc ?? "?")
                + " title=" + (ownerTitle ?? "<null>"));
        }

        // 2. 端口空闲时，隐藏启动服务器；服务就绪后再打开浏览器（只开一个 dsh 标签页）
        string browserExe = GetDefaultBrowserPath();
        string browserName = browserExe == null ? null : Path.GetFileNameWithoutExtension(browserExe);
        Process server = null;
        if (!listening)
        {
            ShowTrayIcon();
            bool started = false;
            // v0.6.5：最多两轮 —— 第一轮失败且判定为"插件与 dsh 版本错配"时，
            // 自动禁用该插件（改 profile 配置，先备份）后再重试一次。
            for (int attempt = 0; attempt < 2 && !started; attempt++)
            {
                SetTrayText(attempt == 0 ? "DeepSeek Harness 服务启动中…" : "已禁用不兼容插件，正在重新启动…");
                startupFailure = null;
                failingPlugin = null;
                failingLoaderId = null;

                // 2. 常规启动：直接运行本机已安装的 dsh（不经 npx：秒起、离线可用）。
                //    版本升级不在这里做 —— 它与"是否要启动服务器"无关，改由
                //    BeginUpgradeInstall 在后台下载（见 3.5 节），避免出现
                //    "端口被占用 → 升级代码根本没跑" 的情况。
                try
                {
                    string cached = FindCachedCheckout(pinnedVersion) ?? FindCachedCheckout(null);
                    server = cached != null ? StartCachedDirect(cached) : StartViaNpx(null);
                }
                catch (Exception ex)
                {
                    Msg("无法启动 DeepSeek 服务：\n" + ex.Message +
                        "\n\n请确认已安装 Node.js（含 npx），并可在命令行运行：\nnpx @deepseek-ai/dsh web", MessageBoxIcon.Error);
                    Log("服务启动失败: " + ex.Message);
                    return 1;
                }

                if (WaitForServerReady(ReadyTimeoutSec, server))
                {
                    started = true;
                    break;
                }

                KillProcessTree(server);
                server = null;

                if (stopRequested)
                {
                    Log("启动过程中收到停止请求，立即收手（不再等满 " + ReadyTimeoutSec + " 秒）");
                    return 0;
                }

                // 插件与 dsh 版本错配 → 询问并自动禁用后重试一次
                if (attempt == 0 && startupFailure != null && !string.IsNullOrEmpty(failingPlugin))
                {
                    if (AskAndDisablePlugin(failingPlugin, failingLoaderId)) continue;
                    return 1;
                }

                string tail = LogTail(10);
                Msg(
                    "DeepSeek Harness 服务启动失败或超时（" + ReadyTimeoutSec + " 秒）。\n\n可能原因：\n" +
                    " · 首次运行需联网下载依赖，速度较慢\n · 网络不通\n · 3080 端口被其他程序占用\n" +
                    (startupFailure != null ? " · 服务报错：" + startupFailure + "\n" : "") +
                    (tail.Length > 0 ? "\n服务器日志（末尾）：\n" + tail + "\n" : "") +
                    "请稍后重试；若持续失败，请手动在命令提示符中运行：\nnpx @deepseek-ai/dsh web", MessageBoxIcon.Error);
                Log("服务启动失败（无自动修复路径）");
                return 1;
            }
            if (!started) return 1;
            Log("服务就绪，耗时 " + sw.Elapsed.TotalSeconds.ToString("0.0") + " 秒");
            RecordOurService();   // v0.6.6：记下"这是我起的服务"，下次启动精确复用
        }
        else
        {
            // 复用已有服务
            ShowTrayIcon();
        }
        SetTrayText("DeepSeek Harness 服务运行中（右键可停止）");

        if (trayIcon == null)
        {
            Msg(
                "系统托盘图标创建失败（可能系统资源不足）。\n\n服务仍会正常运行；请通过" +
                "关闭整个浏览器，或任务管理器结束 " + ExeName + ".exe 来停止服务。", MessageBoxIcon.Warning);
            Log("托盘图标创建失败");
        }

        // 3. 服务已就绪：短等已有页面（恢复的历史会话页），否则打开 URL（只开一个标签页）
        //    v0.6.5：如果本次替换/重启过 3080 上的服务，浏览器里的旧页面已经失效
        //    （就是"页面 Failed to fetch"），此时跳过"已有页面就不开"的去重逻辑，
        //    直接把新地址打开。
        try
        {
            OpenPageIfNeeded(browserExe, serviceReplaced);
        }
        catch (Exception ex)
        {
            Log("浏览器流程异常: " + ex.Message);
        }

        // 3.5 版本升级（与是否启动服务器无关，后台下载，不阻塞使用）
        if (!string.IsNullOrEmpty(requestedUpgrade))
        {
            string target = requestedUpgrade;
            requestedUpgrade = null;
            WriteVersionState(pinnedVersion, null, DateTime.Now, target);
            BeginUpgradeInstall(target);
        }

        // 3.6 v0.6.7：启动后在后台检查 dsh / 插件更新（只在确实有新版本时弹勾选菜单）
        StartStartupUpdateCheck();
        // 3.7 v0.6.7：client-api 通路自检（只记日志 + 失败时气泡提醒）
        StartApiHealthCheck();

        // 4. 监视放后台线程；UI 线程进入真正的消息循环（托盘菜单即时响应），
        //    并用一个 UI 线程定时器做两件事：① 执行后台线程回投的操作
        //    ② 一旦 stopRequested 置位就退出消息循环。
        //    不依赖 SynchronizationContext：消息循环启动前它不可靠，
        //    v0.6 初版因此出现过"点了停止却退不出去"。
        serverProcess = server;
        var monitor = new Thread(delegate ()
        {
            try
            {
                if (browserName == null) TrayLoop();
                else MonitorLoop(null, browserName, server);
            }
            catch (Exception ex)
            {
                Log("监视线程异常: " + ex.Message);
            }
            finally
            {
                RequestStop("监视结束");
            }
        });
        monitor.IsBackground = true;
        monitor.Start();

        var uiTimer = new System.Windows.Forms.Timer { Interval = 150 };
        uiTimer.Tick += delegate
        {
            DrainUiQueue();
            if (stopRequested)
            {
                uiTimer.Stop();
                Application.ExitThread();
            }
        };
        uiTimer.Start();
        uiLoopRunning = true;
        Log("UI 线程进入消息循环（监视线程已启动）");

        // 隐藏自检钩子：DSH_LAUNCHER_AUTOSTOP_MS=毫秒 → 到时自动请求停止，
        // 用于在无人值守的情况下验证"停止 → 退出"整条路径。
        string autoStop = Environment.GetEnvironmentVariable("DSH_LAUNCHER_AUTOSTOP_MS");
        int autoStopMs;
        if (!string.IsNullOrEmpty(autoStop) && int.TryParse(autoStop, out autoStopMs) && autoStopMs > 0)
        {
            var autoStopThread = new Thread(delegate ()
            {
                Thread.Sleep(autoStopMs);
                Log("自检钩子：自动请求停止（" + autoStopMs + "ms）");
                RequestStop("自检自动停止");
            });
            autoStopThread.IsBackground = true;
            autoStopThread.Start();
        }

        Application.Run(new ApplicationContext());

        uiLoopRunning = false;
        uiTimer.Stop();
        uiTimer.Dispose();

        // 5. 收尾
        Log("停止原因: " + (stopReason ?? "未知"));
        HideTrayIcon();
        if (mainForm != null && !mainForm.IsDisposed)
        {
            try { mainForm.Dispose(); } catch { }
            mainForm = null;
        }
        if (activeNpmProcess != null && !activeNpmProcess.HasExited) KillProcessTree(activeNpmProcess);
        if (server != null)
        {
            KillProcessTree(server);
            ClearServiceRecord();   // 本程序的服务已停，记录随之失效
        }
        Log("=== 启动器退出 ===");
        return 0;
    }

    /// <summary>把一段操作排到 UI 线程执行（后台线程调用；消息循环未启动时立即执行）。</summary>
    private static void Ui(Action action)
    {
        if (!uiLoopRunning || Thread.CurrentThread.ManagedThreadId == uiThreadId)
        {
            try { action(); } catch (Exception ex) { Log("UI 操作失败: " + ex.Message); }
            return;
        }
        lock (UiQueueLock) { uiQueue.Enqueue(action); }
    }

    private static void DrainUiQueue()
    {
        while (true)
        {
            Action action;
            lock (UiQueueLock)
            {
                if (uiQueue.Count == 0) return;
                action = uiQueue.Dequeue();
            }
            try { action(); } catch (Exception ex) { Log("UI 回投执行失败: " + ex.Message); }
        }
    }

    /// <summary>
    /// 请求停止：置位 → UI 定时器退出消息循环 → 正常收尾；
    /// 另有看门狗兜底：5 秒仍未退出就强制收尾并结束进程，保证一定能退出。
    /// </summary>
    private static void RequestStop(string reason)
    {
        if (!stopRequested)
        {
            stopRequested = true;
            stopReason = reason;
        }
        Log("收到停止请求（" + reason + "）");
        if (Thread.CurrentThread.ManagedThreadId == uiThreadId)
        {
            try { Application.ExitThread(); } catch { }
            return;
        }
        var watchdog = new Thread(delegate ()
        {
            Thread.Sleep(5000);
            Log("看门狗：常规收尾未完成，强制退出");
            try
            {
                // 正在下载的 npm 也要一起收掉，否则启动器退了、下载还在后台跑
                if (activeNpmProcess != null && !activeNpmProcess.HasExited) KillProcessTree(activeNpmProcess);
            }
            catch { }
            try
            {
                if (serverProcess != null && !serverProcess.HasExited)
                {
                    KillProcessTree(serverProcess);
                    ClearServiceRecord();
                }
            }
            catch { }
            Environment.Exit(0);
        });
        watchdog.IsBackground = true;
        watchdog.Start();
    }

    // 服务就绪后：短等（≤2 秒）捕捉 Edge 恢复出的历史会话页；没有则打开 URL
    private static void OpenPageIfNeeded(string browserExe, bool forceOpen)
    {
        if (string.IsNullOrEmpty(browserExe))
        {
            Msg(
                "未能识别系统默认浏览器，请手动打开：\n" + CurrentUrl() +
                "\n\n服务保持运行；完成后请右键托盘图标停止。", MessageBoxIcon.Warning);
            Log("未识别默认浏览器");
            return;
        }

        if (!forceOpen)
        {
            bool pageOpen = PumpWait(delegate { return DshPageAlreadyOpenInBrowser(); }, 2000);
            if (pageOpen)
            {
                Log("检测到 DeepSeek 页面已在浏览器中打开，不再新开标签页");
                SetTrayText("DeepSeek 页面已在浏览器中打开（双击托盘图标可重新打开）");
                return;
            }
        }
        else
        {
            Log("本次替换/重启过服务：跳过页面去重，直接打开新地址");
        }

        // 没有现成页面（或旧页面已随服务重启而失效）→ 打开 URL（只此一次）
        string url = CurrentUrl();
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Log("已请求打开 DeepSeek 网页: " + url);
            if (forceOpen) SetTrayText("服务已重启：请关闭浏览器里旧的 DeepSeek 标签页");
        }
        catch (Exception ex)
        {
            Msg("无法打开浏览器：\n" + ex.Message + "\n\n请手动打开 " + url +
                "\n服务保持运行；完成后请右键托盘图标停止。", MessageBoxIcon.Error);
            Log("浏览器打开失败: " + ex.Message);
        }
    }

    /// <summary>
    /// v0.6.5：打开页面用的地址。优先用 dsh 自己打印的**带 token 地址** ——
    /// 裸地址可能被 token 校验拒绝（浏览器里表现为打不开或 Failed to fetch）。
    /// </summary>
    private static string CurrentUrl()
    {
        return string.IsNullOrEmpty(serverUserUrl) ? Url : serverUserUrl;
    }

    /// <summary>托盘气泡提示（不打断操作；托盘不可用时静默）。</summary>
    private static void Balloon(string text, ToolTipIcon icon)
    {
        Ui(delegate
        {
            try
            {
                if (trayIcon == null) return;
                trayIcon.BalloonTipTitle = AppTitle;
                trayIcon.BalloonTipText = text;
                trayIcon.BalloonTipIcon = icon;
                trayIcon.ShowBalloonTip(8000);
            }
            catch { }
        });
    }

    /// <summary>
    /// v0.6.7：client-api 通路自检（只记日志 + 失败时一条气泡，不弹窗）。
    /// 步骤：用带 token 的地址换 cookie → POST 一个只读接口。
    /// 用途：区分"服务在跑"与"服务在跑但接口层没挂载"——后者正是
    /// "页面打得开、按钮全废（Failed to fetch）"的分水岭；
    /// 同时提醒"页面里是旧版前端 bundle"这类只能靠重开标签页解决的问题。
    /// </summary>
    private static void StartApiHealthCheck()
    {
        var t = new Thread(delegate ()
        {
            try
            {
                Thread.Sleep(6000);
                if (stopRequested) return;
                string url = CurrentUrl();
                if (string.IsNullOrEmpty(url)) { Log("接口自检：尚未捕获服务地址，跳过"); return; }

                var cookies = new CookieContainer();
                // ① token 换 cookie：服务器对带 token 的 / 返回 303 并种下会话 cookie
                var getReq = (HttpWebRequest)WebRequest.Create(url);
                getReq.CookieContainer = cookies;
                getReq.AllowAutoRedirect = true;
                getReq.Timeout = 5000;
                getReq.ReadWriteTimeout = 5000;
                getReq.UserAgent = "DshLauncher/0.6.7";
                using (var resp = (HttpWebResponse)getReq.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    sr.ReadToEnd();
                }

                // ② POST 一个只读的 client-api 接口
                var baseUri = new Uri(url);
                string api = baseUri.Scheme + "://" + baseUri.Authority + "/api/agentPresets/list";
                var post = (HttpWebRequest)WebRequest.Create(api);
                post.Method = "POST";
                post.ContentType = "application/json";
                post.CookieContainer = cookies;
                post.Timeout = 8000;
                post.ReadWriteTimeout = 8000;
                post.UserAgent = "DshLauncher/0.6.7";
                string body = "{\"type\":\"client-request\",\"rpcId\":\"launcher-health\","
                    + "\"method\":\"agentPresets/list\",\"payload\":{\"args\":{}}}";
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                post.ContentLength = bytes.Length;
                using (var s = post.GetRequestStream()) s.Write(bytes, 0, bytes.Length);

                string text;
                using (var resp = (HttpWebResponse)post.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    text = sr.ReadToEnd();
                }

                if (text.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0)
                {
                    Log("接口自检：通过（client-api 通路正常）");
                }
                else
                {
                    Log("接口自检：响应异常 " + Brief(text));
                    Balloon("服务在跑，但接口自检未通过。\n若页面里的按钮点了没反应，请关闭所有旧标签页后重开。",
                        ToolTipIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Log("接口自检未通过: " + ex.Message);
                Balloon("接口自检未通过：" + ex.Message + "\n若页面里的按钮点了没反应，请关闭旧标签页重开再试。",
                    ToolTipIcon.Warning);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    // 监视循环：浏览器全关 / 服务崩溃 / 托盘操作
    private static void MonitorLoop(Process browser, string browserName, Process server)
    {
        bool ownsBrowser = false;
        if (browser != null)
        {
            // 冷启动的浏览器进程 5 秒后仍存活 → 由本程序持有
            ownsBrowser = !browser.WaitForExit(5000);
        }
        bool sawBrowser = false;
        var sw = Stopwatch.StartNew();

        while (!stopRequested)
        {
            try
            {
                if (ownsBrowser)
                {
                    if (browser.HasExited) break; // 整个浏览器关闭 → 正常停止
                }
                else
                {
                    bool any = browserName != null && BrowserProcessExists(browserName);
                    if (any) sawBrowser = true;
                    else if (sawBrowser) break;
                    else if (sw.Elapsed.TotalSeconds >= 120) break;
                }

                // 服务异常退出（非本程序主动停止）
                if (server != null && server.HasExited)
                {
                    Log("服务进程已退出（PID " + server.Id + "）");
                    string tail = LogTail(8);
                    Msg(
                        "DeepSeek 服务进程已退出（可能崩溃、被其他程序结束，或端口被占用后自动退出）。\n\n" +
                        (tail.Length > 0 ? "服务器日志（末尾）：\n" + tail + "\n" : "") +
                        "如需继续使用，请重新启动本程序。", MessageBoxIcon.Warning);
                    break;
                }
            }
            catch
            {
                break;
            }
            Thread.Sleep(300); // 后台线程轮询：不占用 UI 线程
        }
    }

    private static void TrayLoop()
    {
        while (!stopRequested)
        {
            Thread.Sleep(200);
        }
    }

    // 带消息泵的条件等待：每 100ms 泵一次消息（托盘保持响应）
    private static bool PumpWait(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < timeoutMs)
        {
            Application.DoEvents();
            if (condition()) return true;
            Thread.Sleep(100);
        }
        Application.DoEvents();
        return condition();
    }

    // ================= 服务器 =================

    // 服务器启动统一走 StartCachedDirect（直接用本机已装 dsh）/ StartViaNpx
    // （联网安装指定版本），见"版本与升级"小节。

    private static bool TcpListening(int port, int timeoutMs)
    {
        try
        {
            using (var c = new TcpClient())
            {
                var ar = c.BeginConnect(IPAddress.Loopback, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                c.EndConnect(ar);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string ProbePageTitle()
    {
        int code;
        string header;
        return HttpProbe(1500, out code, out header);
    }

    /// <summary>
    /// HTTP 探测（v0.6.5 重写）：把 401/403 这类"有响应但状态码非 2xx"也算成功。
    /// 旧版直接 GetResponse()，dsh 对无 token 请求返回 401 → 抛异常 → 返回 null，
    /// 于是**健康的 dsh 服务被误报成"3080 被其他程序占用"**。
    /// code=0 才表示完全没响应（超时/连接被拒）。
    /// </summary>
    private static string HttpProbe(int timeoutMs, out int code, out string serverHeader)
    {
        code = 0;
        serverHeader = null;
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(Url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "DshLauncher/0.6.5";
            req.KeepAlive = false;
            req.AllowAutoRedirect = false;   // 303 也算"有响应"，不跟随跳转
            try
            {
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    code = (int)resp.StatusCode;
                    serverHeader = resp.Headers["Server"];
                    return TitleOf(resp);
                }
            }
            catch (WebException we)
            {
                var resp = we.Response as HttpWebResponse;
                if (resp == null) return null;      // 完全没响应
                code = (int)resp.StatusCode;
                serverHeader = resp.Headers["Server"];
                return TitleOf(resp);               // 401 的响应体也要读，不能再当异常丢掉
            }
        }
        catch
        {
            return null;
        }
    }

    private static string TitleOf(HttpWebResponse resp)
    {
        try
        {
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string html = sr.ReadToEnd();
                var m = Regex.Match(html, "<title[^>]*>(.*?)</title>",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                return m.Success ? m.Groups[1].Value.Trim() : "";
            }
        }
        catch
        {
            return "";
        }
    }

    private static bool IsServerReady()
    {
        int code;
        string header;
        HttpProbe(800, out code, out header);
        return code >= 200 && code < 500;   // 401 也算就绪（dsh 需要 token）
    }

    // ================= 3080 占用者分类（v0.6.5） =================

    private const int PortOwnerNone = 0;     // 没人监听 → 正常启动
    private const int PortOwnerDsh = 1;      // 健康的 dsh 服务 → 复用
    private const int PortOwnerDshDead = 2;  // node 占着但 HTTP 不应答 → 询问是否重启
    private const int PortOwnerOther = 3;    // 别的程序 → 只警告，不碰

    /// <summary>
    /// 给 3080 的占用者分类。旧版只看"页面标题里有没有 DeepSeek Harness"，
    /// 结果是两头都错：健康服务（401、无标题）被误报为"被其他程序占用"，
    /// 而半死服务（端口在听、HTTP 不答）反而被当成"已有服务"静默复用 ——
    /// 后者正是"重启多少次都还是 Failed to fetch"的来源。
    /// </summary>
    private static int ClassifyPortOwner(out int pid, out string procName, out string title)
    {
        pid = 0;
        procName = null;
        title = null;
        if (!TcpListening(Port, 400)) return PortOwnerNone;

        pid = NetstatListenPid(Port);
        procName = ProcessNameOf(pid);

        int code;
        string header;
        title = HttpProbe(1800, out code, out header);
        bool responded = code > 0;

        if (responded && title != null
            && title.IndexOf(PageTitleMarker, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return PortOwnerDsh;                       // 标题就是 DeepSeek Harness
        }
        if (responded && IsNodeProcess(procName))
        {
            return PortOwnerDsh;                       // dsh 需要 token，401 属正常
        }
        if (!responded && IsNodeProcess(procName))
        {
            // 再给一次机会：服务可能正在启动（HTTP 还没起来），避免误杀刚启动的服务
            Thread.Sleep(1200);
            title = HttpProbe(1800, out code, out header);
            if (code > 0) return IsNodeProcess(procName) ? PortOwnerDsh : PortOwnerOther;
            return PortOwnerDshDead;
        }
        return PortOwnerOther;
    }

    private static string ProcessNameOf(int pid)
    {
        if (pid <= 0) return null;
        try { using (var p = Process.GetProcessById(pid)) return p.ProcessName; }
        catch { return null; }
    }

    private static bool IsNodeProcess(string procName)
    {
        if (string.IsNullOrEmpty(procName)) return false;
        return procName.ToLowerInvariant().StartsWith("node");
    }

    /// <summary>询问是否结束 3080 上"没响应"的服务并重新启动（页面 Failed to fetch 的解法）。</summary>
    private static bool AskRestartDeadService(int pid)
    {
        string want = string.IsNullOrEmpty(pinnedVersion) ? "本机记录的版本" : pinnedVersion;
        string text =
            "3080 端口上的 DeepSeek 服务没有响应（PID " + pid + "，进程是 node）。\n\n" +
            "这就是浏览器里页面打不开、或报 “Failed to fetch” 的原因：\n" +
            "服务进程还在，但对页面请求完全不回应（常见于崩溃残留，\n" +
            "或被任务管理器强杀过的旧启动器留下的半死进程）。\n\n" +
            "是否结束它，并用 " + want + " 重新启动？\n\n" +
            "   [是]  结束无响应的服务并重新启动（浏览器里的旧页面请关掉重开）\n" +
            "   [否]  保持现状并退出本程序";
        return MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1) == DialogResult.Yes;
    }

    private static bool WaitForServerReady(int timeoutSec, Process server)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSec)
        {
            Application.DoEvents(); // 启动阶段仍在 UI 线程：泵消息让托盘及时可用
            if (IsServerReady()) return true;
            // v0.6.5：识别到致命错误（如插件装载失败）→ 立即返回，不再干等满 180 秒
            if (startupFailure != null)
            {
                Thread.Sleep(400);                      // 给剩余输出一点时间落盘
                return IsServerReady();
            }
            // 进程已退出（如 npm 报错/版本不存在）→ 立即失败，交给调用方回退
            if (server != null && server.HasExited) return IsServerReady();
            // v0.6.5：用户在启动过程中点了"停止服务并退出"→ 立即收手，
            // 旧版会继续等满 180 秒，表现为"点了停止却退不出去"
            if (stopRequested) return false;
            Thread.Sleep(500);
        }
        return IsServerReady();
    }

    private static bool WaitForServerReady(int timeoutSec)
    {
        return WaitForServerReady(timeoutSec, null);
    }

    private static string MissingNodeNpx()
    {
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            bool hasNode = false, hasNpx = false;
            foreach (string dir in pathEnv.Split(';'))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                if (File.Exists(Path.Combine(dir, "node.exe"))) hasNode = true;
                if (File.Exists(Path.Combine(dir, "npx.cmd")) || File.Exists(Path.Combine(dir, "npx.exe"))) hasNpx = true;
            }
            if (!hasNode) return "Node.js（node.exe）";
            if (!hasNpx) return "npx";
            return null;
        }
        catch
        {
            return null;
        }
    }

    // 浏览器中是否已有 DeepSeek 页面（按窗口标题判断，避免重复开标签页）
    private static bool DshPageAlreadyOpenInBrowser()
    {
        try
        {
            foreach (Process p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName.ToLowerInvariant();
                    bool isBrowser = name.Contains("chrome") || name.Contains("msedge")
                        || name.Contains("edge") || name.Contains("firefox")
                        || name.Contains("opera") || name.Contains("brave")
                        || name.Contains("vivaldi") || name.Contains("chromium")
                        || name.Contains("iexplore");
                    if (isBrowser && !string.IsNullOrEmpty(p.MainWindowTitle)
                        && p.MainWindowTitle.IndexOf(PageTitleMarker, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    // ================= 托盘 =================

    private static void ShowTrayIcon()
    {
        if (trayIcon != null) return;
        try
        {
            var menu = new ContextMenuStrip();
            // v0.7.0：控制面板是新的主入口，放在第一项；双击托盘图标也会打开它。
            var panelItem = new ToolStripMenuItem("打开控制面板");
            panelItem.Font = new Font(panelItem.Font, FontStyle.Bold);
            panelItem.Click += delegate { ShowMainWindow(); };
            var openItem = new ToolStripMenuItem("打开 DeepSeek 网页");
            openItem.Click += delegate { OpenUrl(); };
            // v0.6.7：更新检查合并成一张勾选菜单（dsh 本体 + 各 profile 的第三方插件），
            // v0.7.0：改为直接打开控制面板的“更新”页（界面里逐项进度条，不再是黑盒弹窗）。
            var updateItem = new ToolStripMenuItem("检查 dsh / 插件更新…");
            updateItem.Click += delegate { ShowMainWindowUpdates(); };
            // v0.6.5：插件与 dsh 版本错配是"服务起不来"的头号原因，给个随时可查的入口
            var pluginItem = new ToolStripMenuItem("插件兼容检查…");
            pluginItem.Click += delegate { ShowMainWindow(); ShowMainWindowPlugins(); };
            // 回退通道：插件与 dsh 版本互相挑剔时，一键切回缓存里已装好的其他版本
            var versionItem = new ToolStripMenuItem("切换版本（重启生效）");
            versionItem.DropDownOpening += delegate { RebuildVersionMenu(versionItem); };
            // v0.6.7：托盘只留一个出口。旧版有两个（"停止服务并退出"只管自己起的服务、
            // "结束 3080 服务并退出…"只管别人的服务），用户得先判断自己该点哪个；
            // 点错就会留下一个没人管的服务。现在由 ExitWithServiceHandling 一次判断清楚。
            var stopItem = new ToolStripMenuItem("停止服务并退出");
            stopItem.Click += delegate { ExitWithServiceHandling(); };
            menu.Items.Add(panelItem);
            menu.Items.Add(openItem);
            menu.Items.Add(updateItem);
            menu.Items.Add(pluginItem);
            menu.Items.Add(versionItem);
            if (IsSafeMode())
            {
                var exitSafe = new ToolStripMenuItem("退出安全模式（还原被摘除的插件）");
                exitSafe.Click += delegate { ExitSafeModeUi(); };
                menu.Items.Add(exitSafe);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(stopItem);

            trayIcon = new NotifyIcon
            {
                Icon = trayIconObject = CreateTrayIcon(),
                Text = "DeepSeek Harness 服务运行中（右键可停止）",
                Visible = true,
                ContextMenuStrip = menu
            };
            // v0.7.0：双击托盘 = 打开控制面板（原先是直接开网页，改由面板里的按钮承担）
            trayIcon.DoubleClick += delegate { ShowMainWindow(); };
        }
        catch
        {
            trayIcon = null;
        }
    }

    private static void SetTrayText(string text)
    {
        Ui(delegate
        {
            try
            {
                if (trayIcon == null) return;
                string suffix = string.IsNullOrEmpty(pinnedVersion) ? "" : "  [" + pinnedVersion + "]";
                string full = text + suffix;
                trayIcon.Text = full.Length > 63 ? full.Substring(0, 63) : full;
            }
            catch { }
        });
    }

    /// <summary>列出 npx 缓存里所有"可直接启动"的 dsh 版本（必须有 dsh.cmd）。</summary>
    private static List<KeyValuePair<string, string>> ListCachedVersions()
    {
        var list = new List<KeyValuePair<string, string>>();
        try
        {
            string npxRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx");
            if (!Directory.Exists(npxRoot)) return list;
            foreach (string dir in Directory.GetDirectories(npxRoot))
            {
                string pkg = Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh", "package.json");
                if (!File.Exists(pkg)) continue;
                if (!File.Exists(Path.Combine(dir, "node_modules", ".bin", "dsh.cmd"))) continue;
                var m = Regex.Match(File.ReadAllText(pkg), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) continue;
                string v = m.Groups[1].Value;
                bool dup = false;
                foreach (var kv in list) { if (kv.Key == v) { dup = true; break; } }
                if (!dup) list.Add(new KeyValuePair<string, string>(v, dir));
            }
        }
        catch (Exception ex)
        {
            Log("列出缓存版本失败: " + ex.Message);
        }
        return list;
    }

    /// <summary>
    /// v0.6.7：托盘唯一的退出项。一个入口处理三种情况，用户不用先判断该点哪个：
    ///   1) 本程序有正在跑的服务（句柄在）      → 常规停止并退出；
    ///   2) 本程序没有服务，但 3080 上另有服务  → 问一次"结束它并退出"，避免留下孤儿；
    ///   3) 3080 上什么都没有                    → 直接退出。
    /// 问答只在这唯一一处发生，不会再出现"点了一个出口、另一个出口的服务还留着"。
    /// </summary>
    private static void ExitWithServiceHandling()
    {
        // 情况 1：本程序自己启动的服务（serverProcess 有句柄）
        if (serverProcess != null && !serverProcess.HasExited)
        {
            RequestStop("托盘退出");
            return;
        }

        int pid = NetstatListenPid(Port);
        if (pid <= 0)
        {
            RequestStop("托盘退出（3080 无服务）");
            return;
        }

        // 情况 2：3080 上还有服务，但不是本进程启动的
        string who = IsRecordedCurrentService(pid)
            ? "本程序上次记录的"
            : "不属于本程序启动的";
        string text =
            "3080 端口上还有" + who + " DeepSeek 服务（PID " + pid + "）。\n\n" +
            "   [是]  结束它，然后退出本程序\n" +
            "   [否]  什么都不做（本程序继续保持运行）\n\n" +
            "结束后浏览器里的页面会断开；下次双击启动器时会按\n" +
            (string.IsNullOrEmpty(pinnedVersion) ? "本机记录的版本" : pinnedVersion) +
            " 干净启动。";
        if (MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }
        Log("托盘退出：结束 3080 上的服务（PID " + pid + "）");
        KillProcessTree(pid);
        ClearServiceRecord();
        RequestStop("托盘退出（同时结束 3080 服务）");
    }

    /// <summary>重建"切换版本"子菜单：缓存里已装好的每个版本一项，点哪个用哪个。</summary>
    private static void RebuildVersionMenu(ToolStripMenuItem parent)
    {
        var old = new List<ToolStripItem>();
        foreach (ToolStripItem item in parent.DropDownItems) old.Add(item);
        parent.DropDownItems.Clear();
        foreach (ToolStripItem item in old) item.Dispose();

        var versions = ListCachedVersions();
        if (versions.Count == 0)
        {
            parent.DropDownItems.Add(new ToolStripMenuItem("（缓存里没有可直接启动的版本）") { Enabled = false });
            return;
        }
        foreach (var kv in versions)
        {
            var item = new ToolStripMenuItem(kv.Key + (kv.Key == pinnedVersion ? "（当前）" : ""));
            string target = kv.Key;
            item.Click += delegate { SwitchVersion(target); };
            parent.DropDownItems.Add(item);
        }
        parent.DropDownItems.Add(new ToolStripSeparator());
        parent.DropDownItems.Add(new ToolStripMenuItem("点选即定为本机使用版本；重启启动器后生效") { Enabled = false });
    }

    /// <summary>
    /// 把缓存里某个已装好的版本定为使用版本（写状态文件，下次启动生效）。
    /// 这是插件与 dsh 版本互相挑剔时的回退通道：新版跑不起来就切回旧版。
    /// </summary>
    private static void SwitchVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return;
        if (version == pinnedVersion)
        {
            Msg("当前已经在用 " + version + "。", MessageBoxIcon.Information);
            return;
        }
        if (upgradeRunning != 0)
        {
            Msg("正在下载安装新版本，请等它结束再切换。", MessageBoxIcon.Information);
            return;
        }
        WriteVersionState(version, null, DateTime.Now); // 顺带清掉 declined / pending
        Log("托盘切换使用版本：" + (pinnedVersion ?? "未记录") + " → " + version);
        SetTrayText("已设为 " + version + "（重启启动器后生效）");
        Msg("已设为使用版本：" + version + "\n\n本次运行的服务仍是 " + (pinnedVersion ?? "原版本") + "。\n" +
            "请在托盘右键 → 停止服务并退出，再重新双击启动器，即用该版本启动。", MessageBoxIcon.Information);
    }

    // 按当前 DPI 取 app.ico 对应尺寸的原生帧，并把内容放大到接近边框
    // （托盘槽 100%/125%/150%/200% 分别约为 16/20/24/32 物理像素；
    //  内容放大后不会超出边框）
    private static Icon CreateTrayIcon()
    {
        int traySize = GetSystemMetrics(SM_CXSMICON);
        if (traySize < 16) traySize = 16;
        if (traySize > 32) traySize = 32;

        Icon src = null;
        try
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(TrayResource))
            {
                if (s != null) src = new Icon(s, traySize, traySize);
            }
        }
        catch { }
        if (src == null)
        {
            try
            {
                using (var ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                    src = new Icon(ico, traySize, traySize);
            }
            catch { }
        }
        if (src == null) return SystemIcons.Application;

        // 直接显示 app.ico 原生帧（像素级处理在本机托盘上会乱码，已放弃放大）
        return new Icon(src, traySize, traySize);
    }

    private static void HideTrayIcon()
    {
        try
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }
        catch { }
        if (trayIconObject != null)
        {
            try { trayIconObject.Dispose(); } catch { }
            trayIconObject = null;
        }
    }

    private static void OpenUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo(CurrentUrl()) { UseShellExecute = true });
        }
        catch { }
    }

    // ================= 插件兼容与自动修复（v0.6.5） =================
    //
    // 为什么需要这一块：插件按**具体 dsh 版本**声明兼容范围，版本错配时插件会在
    // profile 装载阶段直接抛异常，整个服务起不来（实测：dsh 运行时 0.1.5-rc.3，
    // 插件只支持到 0.1.5-rc.2，且它自己带了一套 0.1.0-rc.6 的 harness 依赖 →
    // "mixed Harness packages" → 服务完全启动不了）。
    // 而插件报错里推荐的 `dsh plugin --profile <name> remove <pkg>` 依赖 pnpm，
    // 本机没装 pnpm，照做只会得到 "'pnpm' 不是内部或外部命令"。
    // 所以这里直接改 profile 配置文件（改前备份），效果等价且立即可用。

    private sealed class PluginCompat
    {
        public string ProfileDir;
        public string Profile;      // profile 名
        public string Name;         // 插件包名
        public string Supported;    // 插件声明的支持版本（逗号分隔）
        public string Recommended;  // 插件推荐的版本
        public string Session, Tools, Llm, Presets, Approval;
        public bool Mixed;          // 5 个 harness 包版本是否混杂
        public string Verdict;      // fatal / risky / ok / unknown
        public string Note;         // 探测失败原因

        /// <summary>会在加载期抛错、让整个服务起不来（只有这类才需要禁用）。</summary>
        public bool Fatal { get { return Verdict == "fatal"; } }

        public string HarnessSummary()
        {
            if (Mixed) return Presets + "、" + Session + " 等（版本混杂）";
            return Presets;
        }

        public string VerdictText()
        {
            if (Verdict == "fatal") return "致命 —— 会让整个服务起不来";
            if (Verdict == "risky") return "版本混杂，但该插件不做加载期校验（能跑，行为可能异常）";
            if (Verdict == "unknown") return "无法判定（" + (Note ?? "探测失败") + "）";
            return "兼容";
        }

        /// <summary>表格列里用的短判定（长句在 ListView 列里会被截断）。</summary>
        public string VerdictShort()
        {
            if (Verdict == "fatal") return "致命（服务起不来）";
            if (Verdict == "risky") return "版本混杂（能跑）";
            if (Verdict == "unknown") return "未知";
            return "兼容";
        }
    }

    /// <summary>
    /// dsh profiles 根目录（%USERPROFILE%\.dsh\profiles）。
    /// 支持 DSH_LAUNCHER_PROFILES_ROOT 覆盖 —— 自检时指向临时副本，
    /// 这样"插件修复"回归测试绝不会碰到真实 profile。
    /// </summary>
    private static string DshProfilesRoot()
    {
        string over = Environment.GetEnvironmentVariable("DSH_LAUNCHER_PROFILES_ROOT");
        if (!string.IsNullOrEmpty(over)) return over;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dsh", "profiles");
    }

    /// <summary>在 PATH 里查找可执行文件（用于探测 pnpm / corepack 是否存在）。</summary>
    private static string FindOnPath(string exeName)
    {
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathEnv.Split(';'))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    string full = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(full)) return full;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    /// <summary>所有 dsh profile 目录（%USERPROFILE%\.dsh\profiles\*）。</summary>
    private static List<string> ListProfileDirs()
    {
        var list = new List<string>();
        try
        {
            string root = DshProfilesRoot();
            if (!Directory.Exists(root)) return list;
            foreach (string d in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(d);
                if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.StartsWith(".", StringComparison.Ordinal)) continue;
                if (!File.Exists(Path.Combine(d, "package.json"))) continue;
                list.Add(d);
            }
        }
        catch (Exception ex)
        {
            Log("枚举 dsh profile 失败: " + ex.Message);
        }
        return list;
    }

    /// <summary>
    /// 该 profile 是否含第三方插件（bundle 里有非 @deepseek-ai/ 的包）。
    /// 用于在启动前跳过不必要的 node 探针进程：只有官方包的 profile 不必探测，
    /// 省掉每次启动 1 秒左右的额外开销。
    /// </summary>
    private static bool ProfileHasThirdPartyBundle(string profileDir)
    {
        try
        {
            string text = File.ReadAllText(Path.Combine(profileDir, "package.json"));
            var m = Regex.Match(text, @"""bundles""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
            if (!m.Success) return false;
            foreach (Match e in Regex.Matches(m.Groups[1].Value, @"""([^""]+)"""))
            {
                if (!e.Groups[1].Value.StartsWith("@deepseek-ai/", StringComparison.Ordinal)) return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>用 node 跑插件探针脚本（真实解析规则），返回输出行；不可用返回 null。</summary>
    private static List<string> RunPluginProbe(string profileDir)
    {
        try
        {
            string script = Path.Combine(Path.GetTempPath(), "dsh-launcher-plugin-probe.js");
            // 每次都重写：脚本随启动器版本变化，若沿用 %TEMP% 里的旧文件会导致
            // "升级了启动器但判定逻辑还是旧的"这种诡异问题。
            File.WriteAllText(script, PluginProbeScript, new UTF8Encoding(false));
            string output = RunCapture("node", "\"" + script + "\" \"" + profileDir + "\"", 30000);
            if (string.IsNullOrEmpty(output)) return null;
            var lines = new List<string>();
            foreach (string l in output.Replace("\r", "").Split('\n'))
            {
                if (l.Trim().Length > 0) lines.Add(l);
            }
            return lines;
        }
        catch (Exception ex)
        {
            Log("插件探针执行失败: " + ex.Message);
            return null;
        }
    }

    /// <summary>扫描所有 profile 的第三方插件并判定兼容性（探针不可用则返回空表）。</summary>
    private static List<PluginCompat> ScanProfilePlugins()
    {
        var list = new List<PluginCompat>();
        foreach (string dir in ListProfileDirs())
        {
            if (!ProfileHasThirdPartyBundle(dir)) continue;   // 只有官方包的 profile 不必起 node
            List<string> lines = RunPluginProbe(dir);
            if (lines == null)
            {
                Log("插件探针不可用（node 缺失或脚本执行失败），跳过 " + Path.GetFileName(dir));
                continue;
            }
            foreach (string raw in lines)
            {
                string[] f = raw.Split('\t');
                if (f[0] == "ERR")
                {
                    Log("插件探针报错（" + Path.GetFileName(dir) + "）: " + (f.Length > 1 ? f[1] : ""));
                    continue;
                }
                if (f[0] != "PLUGIN" || f.Length < 12) continue;
                var pc = new PluginCompat();
                pc.ProfileDir = dir;
                pc.Profile = Path.GetFileName(dir);
                pc.Name = f[1];
                pc.Supported = f[2];
                pc.Recommended = f[3];
                pc.Session = f[4];
                pc.Tools = f[5];
                pc.Llm = f[6];
                pc.Presets = f[7];
                pc.Approval = f[8];
                pc.Mixed = f[9] == "1";
                pc.Verdict = f[10];
                pc.Note = string.IsNullOrEmpty(f[11]) || f[11] == "-" ? null : f[11];
                list.Add(pc);
            }
        }
        return list;
    }

    /// <summary>兼容性报告文本（托盘检查与预检共用）。</summary>
    private static string PluginReportText(List<PluginCompat> all)
    {
        if (all.Count == 0) return "没有检测到第三方插件（profile 里只有 DeepSeek 官方包）。";
        var sb = new StringBuilder();
        foreach (var p in all)
        {
            sb.AppendLine("· " + p.Name + "（profile：" + p.Profile + "）");
            sb.AppendLine("   判定：" + p.VerdictText());
            if (p.Supported != "-")
            {
                sb.AppendLine("   插件声明支持：" + p.Supported
                    + (p.Recommended != "-" ? "（推荐 " + p.Recommended + "）" : ""));
            }
            sb.AppendLine("   实际解析到：" + p.HarnessSummary());
        }
        return sb.ToString();
    }

    /// <summary>
    /// 启动前插件兼容预检：不兼容就问 [是] 禁用并继续 / [否] 仍要试 / [取消] 退出。
    /// 返回 false 表示用户选择退出（调用方直接结束）。
    /// </summary>
    private static bool PreflightPluginCheck()
    {
        try
        {
            var all = ScanProfilePlugins();
            if (all.Count == 0) return true;

            var bad = new List<PluginCompat>();
            foreach (var p in all)
            {
                if (p.Fatal) bad.Add(p);
            }
            if (bad.Count == 0)
            {
                Log("插件预检：没有致命插件（第三方插件 " + all.Count + " 个，"
                    + "其中版本混杂但不校验的会被记为 risky，不拦截启动）");
                return true;
            }

            var lines = new StringBuilder();
            foreach (var p in bad)
            {
                lines.AppendLine("· " + p.Name + "（profile：" + p.Profile + "）");
                lines.AppendLine("   插件只说支持：" + p.Supported);
                lines.AppendLine("   实际解析到：" + p.HarnessSummary());
            }
            string text =
                "检测到会在加载期抛错的插件 —— 这类插件会让整个服务起不来。\n\n" +
                "（它按具体 dsh 版本声明兼容范围，版本错配就在 profile 装载阶段抛异常；\n" +
                "旧版本启动器只会干等 180 秒然后报一句“启动超时”。）\n\n" +
                lines +
                "\n当前使用 dsh：" + (string.IsNullOrEmpty(pinnedVersion) ? "本机已装版本" : pinnedVersion) + "\n\n" +
                "   [是]  禁用这些插件并继续启动\n" +
                "        禁用 = 直接改 profile 配置（dsh 自带的 plugin 命令依赖 pnpm，\n" +
                "        本机没有装 pnpm，用不了），改动前会备份到 .backup-<时间戳>\n" +
                "   [否]  不禁用，仍然尝试启动（很可能失败）\n" +
                "   [取消] 退出，我自己处理";
            var r = MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == DialogResult.Cancel)
            {
                Log("插件预检：用户选择自行处理，退出");
                return false;
            }
            if (r == DialogResult.Yes)
            {
                var rep = new StringBuilder();
                foreach (var p in bad) rep.AppendLine(DisablePluginEverywhere(p.Name, null));
                Log("插件预检：已自动禁用不兼容插件" + Environment.NewLine + rep.ToString());
                Msg("已禁用 " + bad.Count + " 个不兼容插件。\n\n" + rep.ToString(), MessageBoxIcon.Information);
            }
            else
            {
                Log("插件预检：用户选择仍然尝试启动");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log("插件预检异常（跳过，改为运行期兜底）: " + ex.Message);
            return true;
        }
    }

    /// <summary>托盘"插件兼容检查…"：列出所有第三方插件的兼容判定，可一键禁用。</summary>
    private static void CheckPluginsFromTray()
    {
        ShowMainWindowPlugins();
    }

    /// <summary>
    /// 服务因插件不兼容起不来时询问用户；同意就禁用插件并返回 true 让调用方重试启动。
    /// </summary>
    private static bool AskAndDisablePlugin(string plugin, string loaderId)
    {
        string text =
            "服务启动失败：插件与当前 dsh 版本不兼容。\n\n" +
            "  插件：" + plugin + "\n" +
            (string.IsNullOrEmpty(loaderId) ? "" : "  装载条目：" + loaderId + "\n") +
            "  实际使用 dsh：" + (string.IsNullOrEmpty(pinnedVersion) ? "本机已装版本" : pinnedVersion) + "\n\n" +
            "这类插件是按具体 dsh 版本声明兼容范围的。版本错配时它在 profile 装载\n" +
            "阶段直接抛异常，导致**整个服务起不来**（不是单个功能失效）。\n\n" +
            "是否自动禁用它并重新启动服务？\n\n" +
            "   [是]  禁用后立即重启服务\n" +
            "        auto-mode 的自动权限模式会停用，其余功能不受影响；\n" +
            "        禁用 = 直接改 profile 配置（dsh 自带的 plugin 命令依赖 pnpm，\n" +
            "        本机没装 pnpm，用不了），改动前备份到 .backup-<时间戳>\n" +
            "   [否]  不处理，退出本程序";
        if (MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) != DialogResult.Yes)
        {
            Log("用户选择不自动禁用插件（" + plugin + "），退出");
            return false;
        }

        SetTrayText("正在禁用不兼容插件…");
        string report = DisablePluginEverywhere(plugin, loaderId);
        Log("已自动禁用不兼容插件 " + plugin + Environment.NewLine + report);
        Msg("已禁用插件：" + plugin + "\n\n" + report +
            "\n将按 " + (string.IsNullOrEmpty(pinnedVersion) ? "本机已装版本" : pinnedVersion) + " 重新启动服务。\n" +
            "要用回该插件，请等插件作者更新支持当前 dsh 版本（或切回它支持的版本）。",
            MessageBoxIcon.Information);
        return true;
    }

    /// <summary>
    /// 文件级禁用插件：处理 profile 里三处引用，改动前整份备份。
    ///   1) package.json   —— dependencies 与 dsh.profile.bundles 里的条目（清理悬空逗号）
    ///   2) pnpm-workspace.yaml —— 含该插件名的行
    ///   3) cordis.patch.yml —— 该插件贡献的 loader 条目对应的补丁整段注释掉，
    ///      并保证文件顶层仍是 YAML 数组（补 `[]`）：只注释不补数组会让 dsh 报
    ///      "must be a top-level YAML array of loader patch entries" 而启动失败。
    /// </summary>
    private static string DisablePluginEverywhere(string plugin, string loaderId)
    {
        var sb = new StringBuilder();
        var ids = new List<string>();
        if (!string.IsNullOrEmpty(loaderId)) ids.Add(loaderId);
        foreach (string id in InsertLoaderIdsOfPlugin(plugin))
        {
            if (!ids.Contains(id)) ids.Add(id);
        }

        int hit = 0;
        foreach (string dir in ListProfileDirs())
        {
            string pj = Path.Combine(dir, "package.json");
            string pkgText;
            try { pkgText = File.ReadAllText(pj); }
            catch { continue; }
            if (pkgText.IndexOf(plugin, StringComparison.Ordinal) < 0) continue;

            hit++;
            string backup = BackupProfileFiles(dir);
            sb.AppendLine("· profile " + Path.GetFileName(dir) + "（备份：" + backup + "）");

            bool changed;
            RemovePluginFromPackageJson(pj, plugin, out changed);
            sb.AppendLine("   package.json：" + (changed ? "已删除该插件条目" : "未发现条目"));

            string ws = Path.Combine(dir, "pnpm-workspace.yaml");
            if (File.Exists(ws) && RemovePluginLinesFromFile(ws, plugin, out changed) && changed)
                sb.AppendLine("   pnpm-workspace.yaml：已删除该插件行");

            string patch = Path.Combine(dir, "cordis.patch.yml");
            if (File.Exists(patch) && ids.Count > 0)
            {
                var done = new List<string>();
                foreach (string id in ids)
                {
                    if (CommentOutPatchEntry(patch, id, out changed) && changed) done.Add(id);
                }
                if (done.Count > 0)
                    sb.AppendLine("   cordis.patch.yml：已注释补丁条目 " + string.Join("、", done.ToArray()) + "（顶层仍为数组）");
            }
        }
        if (hit == 0) sb.AppendLine("（没有找到引用该插件的 profile）");
        return sb.ToString();
    }

    /// <summary>从插件自带的 cordis.patch.yml 里提取它 insert 进来的 loader 条目 id。</summary>
    private static List<string> InsertLoaderIdsOfPlugin(string plugin)
    {
        var ids = new List<string>();
        try
        {
            foreach (string dir in ListProfileDirs())
            {
                string f = Path.Combine(dir, "node_modules", plugin.Replace('/', Path.DirectorySeparatorChar), "cordis.patch.yml");
                if (!File.Exists(f)) continue;
                bool inInsert = false;
                int insertIndent = -1;
                foreach (string raw in File.ReadAllLines(f))
                {
                    string t = raw.Trim();
                    if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                    int ind = raw.Length - raw.TrimStart().Length;
                    if (t.StartsWith("insert:", StringComparison.Ordinal))
                    {
                        inInsert = true;
                        insertIndent = ind;
                        continue;
                    }
                    var m = Regex.Match(t, @"^-?\s*id\s*:\s*['""]?([A-Za-z0-9_\-]+)");
                    if (m.Success)
                    {
                        if (inInsert && ind > insertIndent && !ids.Contains(m.Groups[1].Value))
                            ids.Add(m.Groups[1].Value);
                        if (inInsert && ind <= insertIndent) inInsert = false;
                        continue;
                    }
                    if (inInsert && ind <= insertIndent) inInsert = false;
                }
                if (ids.Count > 0) break;
            }
        }
        catch (Exception ex)
        {
            Log("解析插件 loader id 失败: " + ex.Message);
        }
        return ids;
    }

    /// <summary>备份 profile 的配置文件到 .backup-&lt;时间戳&gt;（只备份，不删除任何东西）。</summary>
    private static string BackupProfileFiles(string profileDir)
    {
        try
        {
            string dest = Path.Combine(profileDir, ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            int n = 1;
            while (Directory.Exists(dest)) { dest = Path.Combine(profileDir, ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n); n++; }
            Directory.CreateDirectory(dest);
            foreach (string name in new string[] { "package.json", "pnpm-workspace.yaml", "cordis.patch.yml", "cordis.yml", "pnpm-lock.yaml" })
            {
                string src = Path.Combine(profileDir, name);
                if (File.Exists(src)) File.Copy(src, Path.Combine(dest, name), true);
            }
            Log("已备份 profile 配置到 " + dest);
            return dest;
        }
        catch (Exception ex)
        {
            Log("备份 profile 配置失败: " + ex.Message);
            return "(备份失败：" + ex.Message + ")";
        }
    }

    /// <summary>行级删除 package.json 里该插件的条目，并修掉被删行留下的悬空逗号。</summary>
    private static bool RemovePluginFromPackageJson(string path, string plugin, out bool changed)
    {
        changed = false;
        try
        {
            string[] lines = File.ReadAllLines(path);
            var kept = new List<string>();
            bool removed = false;
            foreach (string line in lines)
            {
                if (line.IndexOf("\"" + plugin + "\"", StringComparison.Ordinal) >= 0)
                {
                    removed = true;
                    continue;
                }
                kept.Add(line);
            }
            if (!removed) return true;
            string joined = string.Join(Environment.NewLine, kept.ToArray());
            joined = Regex.Replace(joined, @",\s*([\]}])", "$1");   // 悬空逗号（删掉最后一项时会出现）
            File.WriteAllText(path, joined, new UTF8Encoding(false));
            changed = true;
            Log("已从 " + path + " 删除插件 " + plugin);
            return true;
        }
        catch (Exception ex)
        {
            Log("修改 package.json 失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>删除文本文件里所有含该插件名的行（pnpm-workspace.yaml）。</summary>
    private static bool RemovePluginLinesFromFile(string path, string plugin, out bool changed)
    {
        changed = false;
        try
        {
            var kept = new List<string>();
            bool removed = false;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.IndexOf(plugin, StringComparison.Ordinal) >= 0)
                {
                    removed = true;
                    continue;
                }
                kept.Add(line);
            }
            if (!removed) return true;
            File.WriteAllText(path, string.Join(Environment.NewLine, kept.ToArray()), new UTF8Encoding(false));
            changed = true;
            Log("已从 " + path + " 删除含 " + plugin + " 的行");
            return true;
        }
        catch (Exception ex)
        {
            Log("修改 " + Path.GetFileName(path) + " 失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 注释掉 cordis.patch.yml 里指定 loader id 的补丁条目（整段），并保证文件顶层
    /// 仍是 YAML 数组：注释后若没有任何生效行，补一行 `[]`。这一步很关键 ——
    /// 只注释不补数组，dsh 会报 "overlay ... must be a top-level YAML array of
    /// loader patch entries" 直接启动失败（实测踩过）。
    /// </summary>
    private static bool CommentOutPatchEntry(string path, string loaderId, out bool changed)
    {
        changed = false;
        try
        {
            string[] lines = File.ReadAllLines(path);
            var outLines = new List<string>();
            int i = 0;
            bool commented = false;
            while (i < lines.Length)
            {
                string t = lines[i].Trim();
                if (IsIdEntry(t, loaderId))
                {
                    int baseIndent = lines[i].Length - lines[i].TrimStart().Length;
                    outLines.Add(Comment(lines[i]));
                    i++;
                    while (i < lines.Length)
                    {
                        string cur = lines[i];
                        string curT = cur.Trim();
                        if (curT.Length == 0) { outLines.Add(cur); i++; continue; }   // 空行不打断条目
                        int ind = cur.Length - cur.TrimStart().Length;
                        if (ind <= baseIndent) break;                                  // 回到同级 → 该条目结束
                        outLines.Add(Comment(cur));
                        i++;
                    }
                    commented = true;
                    continue;
                }
                outLines.Add(lines[i]);
                i++;
            }
            if (!commented) return true;

            bool hasEffective = false;
            foreach (string l in outLines)
            {
                string t = l.Trim();
                if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                hasEffective = true;
                break;
            }
            if (!hasEffective) outLines.Add("[]");   // 全被注释 → 顶层补空数组，否则 dsh 解析失败

            File.WriteAllText(path, string.Join(Environment.NewLine, outLines.ToArray()), new UTF8Encoding(false));
            changed = true;
            Log("已注释 " + path + " 里 id=" + loaderId + " 的补丁条目"
                + (hasEffective ? "" : "，并补上顶层 [] "));
            return true;
        }
        catch (Exception ex)
        {
            Log("修改 cordis.patch.yml 失败: " + ex.Message);
            return false;
        }
    }

    /// <summary>该行是否是目标 loader 条目（形如 "- id: auto-permission-mode"，允许引号）。</summary>
    private static bool IsIdEntry(string trimmedLine, string loaderId)
    {
        if (!trimmedLine.StartsWith("- id:", StringComparison.Ordinal)) return false;
        string val = trimmedLine.Substring(5).Trim().Trim('\'', '"');
        return val == loaderId;
    }

    private static string Comment(string line)
    {
        string t = line.TrimStart();
        if (t.StartsWith("#", StringComparison.Ordinal)) return line;
        int ind = line.Length - t.Length;
        return line.Substring(0, ind) + "# " + t;
    }

    /// <summary>执行一条命令并捕获 stdout（有界超时 + 超时杀整棵树）。用于 node 探针等短命令。</summary>
    private static string RunCapture(string fileName, string arguments, int timeoutMs)
    {
        Process p = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            p = Process.Start(psi);
            if (p == null) return null;
            var stdout = new StringBuilder();
            var outDone = new ManualResetEvent(false);
            var errDone = new ManualResetEvent(false);
            var readerOut = new Thread(delegate ()
            {
                try { stdout.Append(p.StandardOutput.ReadToEnd()); }
                catch { }
                finally { outDone.Set(); }
            });
            var readerErr = new Thread(delegate ()
            {
                try { p.StandardError.ReadToEnd(); }
                catch { }
                finally { errDone.Set(); }
            });
            readerOut.IsBackground = true;
            readerErr.IsBackground = true;
            readerOut.Start();
            readerErr.Start();
            if (!p.WaitForExit(timeoutMs))
            {
                Log(fileName + " 超时（" + (timeoutMs / 1000) + " 秒），终止整棵进程树");
                KillProcessTree(p);
                return null;
            }
            outDone.WaitOne(2000);
            errDone.WaitOne(2000);
            return p.ExitCode == 0 ? stdout.ToString() : null;
        }
        catch (Exception ex)
        {
            Log("命令执行失败(" + fileName + "): " + ex.Message);
            if (p != null) KillProcessTree(p);
            return null;
        }
    }

    /// <summary>
    /// 插件兼容性探针（node 脚本，写入 %TEMP%）。两层判断，避免误伤：
    ///  1) 版本层：复刻插件自己的 assertHarnessCompatibility 语义 —— 5 个 harness 包
    ///     （session/tools/llm/permission-presets/user-approval）必须版本一致，且该版本
    ///     要在插件 compatibility.json 的 supportedHosts 里。按 node 的真实解析规则解析
    ///     （会跟随符号链接/junction）：实测插件链接到开发检查点时，它自带一套 0.1.0-rc.6
    ///     的 harness 依赖，而 session/user-approval 落到 npx 缓存的 0.1.5-rc.3 ——
    ///     这正是"版本混杂"的来源，只有真实解析才看得见。
    ///  2) 代码层：**扫描插件已发布的 lib 代码，看它是否真的在加载期做这个断言**。
    ///     这一步非常关键：实测 @nanmicoder/dsh-agent-teams 的 harness 版本同样混杂，
    ///     但它不调用断言、能正常加载；而 @nanmicoder/dsh-auto-mode 在 apply 阶段调用
    ///     assertHarnessCompatibility()，会直接抛错让整个服务起不来。
    ///     不做这层区分就会把能用的插件也判成"致命"，误伤用户环境。
    /// 判定：fatal=会在加载期抛错、整个服务起不来 / risky=版本混杂但该插件不校验 /
    ///       ok=兼容 / unknown=探测失败
    /// 输出：每行 "PLUGIN\t名称\t支持版本\t推荐版本\tsession\ttools\tllm\tpresets\tapproval\t混杂\t判定\t备注"
    /// 全部字符串用单引号，避免 C# 逐字字符串转义。
    /// </summary>
    private const string PluginProbeScript = @"
const fs = require('fs');
const path = require('path');
const createRequire = require('module').createRequire;
const profileDir = process.argv[2];
const anchors = ['dsh-session', 'dsh-tools', 'dsh-llm', 'dsh-permission-presets', 'dsh-user-approval'];
function clean(s) { return String(s === undefined || s === null ? '' : s).replace(/[\t\r\n]+/g, ' ').slice(0, 160) || '-'; }
function line(tag, parts) { console.log([tag].concat(parts).join('\t')); }
// 插件已发布代码里是否真的做加载期断言（决定「版本混杂」是否会致命：注意本行不能用 ASCII 双引号，会提前闭合 C# 逐字字符串）
function hasLoadAssertion(root) {
  const markers = ['assertHarnessCompatibility', 'unsupported or mixed Harness'];
  const stack = [path.join(root, 'lib')];
  let scanned = 0;
  while (stack.length > 0 && scanned < 300) {
    const dir = stack.pop();
    let entries = [];
    try { entries = fs.readdirSync(dir, { withFileTypes: true }); } catch (e) { continue; }
    for (const en of entries) {
      const p = path.join(dir, en.name);
      if (en.isDirectory()) {
        if (en.name !== 'node_modules' && en.name !== '.git') stack.push(p);
        continue;
      }
      if (!/\.(js|mjs|cjs)$/.test(en.name)) continue;
      scanned++;
      let txt = '';
      try { txt = fs.readFileSync(p, 'utf8'); } catch (e) { continue; }
      for (const mk of markers) { if (txt.indexOf(mk) >= 0) return true; }
    }
  }
  return false;
}
try {
  const pkg = JSON.parse(fs.readFileSync(path.join(profileDir, 'package.json'), 'utf8'));
  const bundles = (pkg.dsh && pkg.dsh.profile && pkg.dsh.profile.bundles) || [];
  const req = createRequire(path.join(profileDir, 'node_modules', 'dsh-probe.js'));
  for (const b of bundles) {
    if (String(b).indexOf('@deepseek-ai/') === 0) continue;
    const o = { name: b, supported: '-', recommended: '-', v: {}, mixed: '0', verdict: 'ok', note: '-' };
    try {
      const root = path.dirname(req.resolve(b + '/package.json'));
      const r2 = createRequire(path.join(root, 'lib', 'index.js'));
      try {
        const comp = JSON.parse(fs.readFileSync(path.join(root, 'compatibility.json'), 'utf8'));
        const hosts = comp.supportedHosts || [];
        o.supported = hosts.map(function (h) { return h.version || String(h); }).join(',') || '-';
        o.recommended = comp.recommendedHost || '-';
      } catch (e) { }
      const list = [];
      for (const h of anchors) {
        let v = 'unresolved';
        try { v = r2('@deepseek-ai/' + h + '/package.json').version; } catch (e) { }
        o.v[h] = v;
        list.push(v);
      }
      const uniq = list.filter(function (x, i) { return list.indexOf(x) === i; });
      o.mixed = uniq.length > 1 ? '1' : '0';
      const host = o.v['dsh-permission-presets'];
      const inList = o.supported === '-' ? false : o.supported.split(',').indexOf(host) >= 0;
      const asserts = hasLoadAssertion(root);
      if (asserts) o.verdict = (o.mixed === '1' || !inList) ? 'fatal' : 'ok';
      else if (o.mixed === '1') o.verdict = 'risky';
      else o.verdict = 'ok';
    } catch (e) { o.verdict = 'unknown'; o.note = clean(e && e.message ? e.message : e); }
    line('PLUGIN', [o.name, o.supported, o.recommended, o.v['dsh-session'], o.v['dsh-tools'], o.v['dsh-llm'], o.v['dsh-permission-presets'], o.v['dsh-user-approval'], o.mixed, o.verdict, o.note]);
  }
  line('OK', ['1']);
} catch (e) { line('ERR', [clean(e && e.message ? e.message : e)]); }
";

    // ================= 版本与升级 =================
    //
    // 版本策略：
    //  1) 启动时优先**直接运行本机 npx 缓存里已安装的 dsh**（不联网、秒起，
    //     也不会有 npx 静默拉新版的行为）；
    //  2) 每次启动查 npm latest，有新版本且**确认可安装**时弹窗询问；
    //  3) 选择升级才联网安装新版本；安装/启动失败自动回退到原版本，
    //     绝不出现"升级失败又打不开"的情况；
    //  4) 状态与日志写在 %LOCALAPPDATA%\DeepSeekLauncher\，不在程序目录
    //     （尤其桌面）生成任何文件。

    private const string RegistryUrl = "https://registry.npmjs.org/@deepseek-ai%2fdsh";
    // 单个版本的下载安装上限：实测 200 MB 依赖在慢网络上要 16 分钟，
    // 30 分钟既给足余量，又保证"卡住"最迟半小时内一定有个明确结论。
    private const int InstallTimeoutMs = 30 * 60 * 1000;
    private static string pinnedVersion;    // 本次启动实际使用的版本；null = 未记录
    private static string requestedUpgrade; // 本次要升级到的版本；null = 无
    private static int upgradeRunning;      // 0=空闲 1=下载安装中（Interlocked 单飞）
    private static volatile Process activeNpmProcess; // 正在跑的 npm 进程（停止时一并收掉）
    private static readonly TimeSpan RecheckAfter = TimeSpan.FromHours(12);

    private static readonly Queue<Action> uiQueue = new Queue<Action>();
    private static readonly object UiQueueLock = new object();
    private static volatile bool uiLoopRunning;       // Application.Run 是否已在运行
    private static int uiThreadId;                    // UI 线程 id
    private static Process serverProcess;             // 本程序启动的服务器进程（看门狗用）
    private static Mutex instanceMutex;               // 单实例锁（全程持有）
    private static string stopReason;                 // 停止原因（日志用）

    // ---- v0.6.5：服务器输出解析（抓带 token 地址 / 识别插件装载失败）----
    private static volatile string serverUserUrl;     // dsh 打印的带 token 地址（打开页面用它）
    private static volatile string startupFailure;    // 启动阶段识别到的致命错误摘要
    private static volatile string failingPlugin;     // 出错插件包名，如 @nanmicoder/dsh-auto-mode
    private static volatile string failingLoaderId;   // 出错 loader entry id，如 auto-permission-mode
    private static readonly object ServerOutLock = new object();
    private static readonly Queue<string> serverOut = new Queue<string>();  // 服务器输出最近若干行
    private static bool serviceReplaced;              // 本次是否替换/重启过 3080 上的服务

    // v0.6.6：把"本程序启动的那个服务"记进状态文件（PID + 启动时间 + 版本），
    // 下次启动可精确识别"这就是我上次起的服务"，不再只靠"比我早启动就是孤儿"
    // 的启发式（那个启发式会把本程序自己刚起的服务也判成孤儿 → 每次重启都弹窗）。
    private static int servicePid;                    // 记录的服务 PID；0 = 无记录
    private static DateTime serviceStartedAt = DateTime.MinValue; // 该进程的启动时间（防 PID 复用）
    private static string serviceVersion;             // 该服务启动时使用的 dsh 版本

    private static void EnsureAppDataDir()
    {
        try { Directory.CreateDirectory(AppDataDir); } catch { }
    }

    /// <summary>把 v0.5 写在 exe 目录旁的 dsh-version.txt 迁移到 AppData 并删除旧文件。</summary>
    private static void MigrateLegacyState()
    {
        try
        {
            string legacy = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dsh-version.txt");
            if (!File.Exists(legacy) || File.Exists(VersionFile)) return;
            EnsureAppDataDir();
            File.Copy(legacy, VersionFile, true);
            File.Delete(legacy);
            Log("已迁移旧状态文件到 " + VersionFile + "，并删除程序目录下的旧文件");
        }
        catch (Exception ex)
        {
            Log("旧状态文件迁移失败: " + ex.Message);
        }
    }

    /// <summary>读取版本状态文件（version / declined / checked / pending / service_*）。</summary>
    private static void ReadVersionState(out string version, out string declined, out DateTime checkedAt, out string pending)
    {
        version = null;
        declined = null;
        pending = null;
        checkedAt = DateTime.MinValue;
        servicePid = 0;
        serviceStartedAt = DateTime.MinValue;
        serviceVersion = null;
        try
        {
            if (!File.Exists(VersionFile)) return;
            int parsed = ParseStateLines(File.ReadAllLines(VersionFile),
                ref version, ref declined, ref checkedAt, ref pending);
            // v0.7.0 自愈：文件在、却一个已知键都没解析出来（写坏/被清空/被截断）
            // → 用 WriteVersionState 留下的 .bak 救回，避免“状态丢了就再也认不出自己的服务”。
            if (parsed == 0)
            {
                string bak = VersionFile + ".bak";
                if (File.Exists(bak))
                {
                    Log("状态文件疑似损坏，尝试从备份恢复：" + bak);
                    int parsedBak = ParseStateLines(File.ReadAllLines(bak),
                        ref version, ref declined, ref checkedAt, ref pending);
                    if (parsedBak > 0)
                    {
                        try { File.Copy(bak, VersionFile, true); } catch { }
                        Log("已从备份恢复状态文件（解析到 " + parsedBak + " 项）");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log("版本状态读取失败: " + ex.Message);
        }
    }

    /// <summary>解析状态文件的若干行；返回解析到的已知键数量（0 = 文件损坏或为空）。</summary>
    private static int ParseStateLines(string[] lines, ref string version, ref string declined,
        ref DateTime checkedAt, ref string pending)
    {
        int parsed = 0;
        foreach (string line in lines)
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim();
            if (key == "version") { version = val; parsed++; }
            else if (key == "declined") { declined = val; parsed++; }
            else if (key == "pending") { pending = val; parsed++; }
            else if (key == "checked")
            {
                DateTime t;
                if (DateTime.TryParse(val, out t)) checkedAt = t;
                parsed++;
            }
            else if (key == "service_pid")
            {
                int p;
                if (int.TryParse(val, out p)) servicePid = p;
                parsed++;
            }
            else if (key == "service_start")
            {
                DateTime t;
                if (DateTime.TryParse(val, out t)) serviceStartedAt = t;
                parsed++;
            }
            else if (key == "service_version") { serviceVersion = val; parsed++; }
        }
        return parsed;
    }

    private static void WriteVersionState(string version, string declined, DateTime checkedAt)
    {
        WriteVersionState(version, declined, checkedAt, null);
    }

    private static void WriteVersionState(string version, string declined, DateTime checkedAt, string pending)
    {
        try
        {
            EnsureAppDataDir();
            var sb = new StringBuilder();
            sb.AppendLine("# DeepSeek Harness 版本状态（启动器自动维护；位于 %LOCALAPPDATA%\\DeepSeekLauncher\\）");
            sb.AppendLine("# version=当前使用版本；pending=待升级到的版本；declined=已拒绝版本；checked=上次检查时间");
            sb.AppendLine("# service_*=本程序启动的那个服务（PID/启动时间/版本），用于精确识别“该不该复用”");
            if (!string.IsNullOrEmpty(version)) sb.AppendLine("version=" + version);
            if (!string.IsNullOrEmpty(pending)) sb.AppendLine("pending=" + pending);
            if (!string.IsNullOrEmpty(declined)) sb.AppendLine("declined=" + declined);
            if (checkedAt > DateTime.MinValue) sb.AppendLine("checked=" + checkedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            if (servicePid > 0)
            {
                sb.AppendLine("service_pid=" + servicePid);
                if (serviceStartedAt > DateTime.MinValue)
                    sb.AppendLine("service_start=" + serviceStartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                if (!string.IsNullOrEmpty(serviceVersion))
                    sb.AppendLine("service_version=" + serviceVersion);
            }
            // v0.7.0：原子写 —— 先写 .tmp，再替换，并把旧文件留成 .bak。
            // 这样“写到一半断电/被杀”不会留下半截文件（半截文件会让启动器认不出自己的服务）。
            string text = sb.ToString();
            string tmp = VersionFile + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(VersionFile))
            {
                try { File.Replace(tmp, VersionFile, VersionFile + ".bak"); }
                catch
                {
                    // 某些文件系统不支持 Replace → 退化为覆盖 + 删除临时文件
                    try { File.Copy(tmp, VersionFile, true); } catch { }
                    try { File.Delete(tmp); } catch { }
                }
            }
            else
            {
                File.Move(tmp, VersionFile);
            }
        }
        catch (Exception ex)
        {
            Log("版本状态写入失败: " + ex.Message);
        }
    }

    /// <summary>探测 npx 缓存中已安装的 dsh 版本（取最近写入的检查点）。</summary>
    private static string DetectInstalledVersion()
    {
        try
        {
            string npxRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx");
            if (!Directory.Exists(npxRoot)) return null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string dir in Directory.GetDirectories(npxRoot))
            {
                string pkg = Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh", "package.json");
                if (!File.Exists(pkg)) continue;
                DateTime t = File.GetLastWriteTimeUtc(pkg);
                if (t <= bestTime) continue;
                var m = Regex.Match(File.ReadAllText(pkg), "\"version\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) continue;
                best = m.Groups[1].Value;
                bestTime = t;
            }
            return best;
        }
        catch (Exception ex)
        {
            Log("本地版本探测失败: " + ex.Message);
            return null;
        }
    }

    /// <summary>在 npx 缓存里找指定版本的 dsh 检查点目录；version 为 null 时取最近写入的。</summary>
    private static string FindCachedCheckout(string version)
    {
        try
        {
            string npxRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache", "_npx");
            if (!Directory.Exists(npxRoot)) return null;
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (string dir in Directory.GetDirectories(npxRoot))
            {
                string pkg = Path.Combine(dir, "node_modules", "@deepseek-ai", "dsh", "package.json");
                if (!File.Exists(pkg)) continue;
                if (!File.Exists(Path.Combine(dir, "node_modules", ".bin", "dsh.cmd"))) continue;
                string text = File.ReadAllText(pkg);
                if (!string.IsNullOrEmpty(version))
                {
                    var mv = Regex.Match(text, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (!mv.Success || mv.Groups[1].Value != version) continue;
                    return dir;
                }
                DateTime t = File.GetLastWriteTimeUtc(pkg);
                if (t > bestTime)
                {
                    best = dir;
                    bestTime = t;
                }
            }
            return best;
        }
        catch (Exception ex)
        {
            Log("缓存检查点查找失败: " + ex.Message);
            return null;
        }
    }

    /// <summary>直接运行本机已安装的 dsh（不经 npx：秒起、离线可用、版本确定）。</summary>
    private static Process StartCachedDirect(string checkoutDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            // v0.6.7：启动前清掉两个会破坏 dsh 的注入变量 ——
            //   NODE_OPTIONS：WorkBuddy 之类的宿主会给子进程注入 node 语言 shim
            //     （--require node-language-shim.cjs），它带一个 safe-delete 守卫，
            //     会拦截 dsh 内部对自己锁文件的删除，导致 profile 装载阶段直接抛错
            //     `[safe-delete][SAFE_DELETE_BULK_CONFIRM_REQUIRED]` 起不来；
            //   CODEBUDDY_SAFE_DELETE_BULK_GUARD：同一守卫的独立开关。
            // 用命令行内 set 清空（而不是 psi.EnvironmentVariables —— 本机
            // HTTP_PROXY/http_proxy 大小写重复会让那个字典一访问就抛异常）。
            Arguments = "/c set NODE_OPTIONS=&& set CODEBUDDY_SAFE_DELETE_BULK_GUARD=&& \""
                + Path.Combine(checkoutDir, "node_modules", ".bin", "dsh.cmd") + "\" web --no-open",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var p = Process.Start(psi);
        HookServerOutput(p);
        Log("已直接启动本机 dsh（PID " + p.Id + " ← " + checkoutDir + "）");
        return p;
    }

    /// <summary>经 npx 启动（可指定版本，用于安装/升级；未指定则用缓存）。</summary>
    private static Process StartViaNpx(string version)
    {
        // v0.6.5：npm 的开关一律用**命令行参数**表达，不再写 psi.EnvironmentVariables。
        // 原因（实测）：本机环境同时存在 HTTP_PROXY 与 http_proxy（HTTPS_PROXY/https_proxy 同理），
        // 而 .NET 的 ProcessStartInfo.EnvironmentVariables 是大小写不敏感的 StringDictionary，
        // 一访问它就抛 "已添加项。字典中的关键字:“HTTP_PROXY”所添加的关键字:“http_proxy”"，
        // Process.Start 直接失败 —— 结果所有走 npm 的路径（npx 启动、检查更新、
        // 可安装性校验、升级下载）全部静默失效。改用命令行开关即彻底绕开该字典。
        // 指定版本时必须允许联网解析：强制离线元数据会误报"版本不存在"。
        string flags = "--yes --no-fund --no-update-notifier "
            + (string.IsNullOrEmpty(version) ? "--prefer-offline " : "--prefer-online ");
        var psi = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            // v0.6.7：同 StartCachedDirect —— 先清掉宿主注入的 NODE_OPTIONS / safe-delete 开关，
            // 否则从被注入的环境里启动 dsh 会在装载阶段失败。
            Arguments = "/c set NODE_OPTIONS=&& set CODEBUDDY_SAFE_DELETE_BULK_GUARD=&& npx " + flags
                + (string.IsNullOrEmpty(version) ? "@deepseek-ai/dsh" : "@deepseek-ai/dsh@" + version)
                + " web --no-open",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var p = Process.Start(psi);
        HookServerOutput(p);
        Log("已启动 npx（PID " + p.Id + "，规格 " + (string.IsNullOrEmpty(version) ? "缓存/latest" : version) + "）");
        return p;
    }

    private static void HookServerOutput(Process p)
    {
        p.OutputDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            Log("SVR> " + e.Data);
            OnServerLine(e.Data);
        };
        p.ErrorDataReceived += (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            Log("SVR! " + e.Data);
            OnServerLine(e.Data);
        };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
    }

    /// <summary>
    /// v0.6.5：解析服务器每一行输出，做两件事：
    ///  1) 抓 dsh 打印的带 token 地址（打开浏览器要用它，裸地址可能被 401 拒绝）；
    ///  2) 识别"插件树装载失败"（插件与 dsh 版本错配时插件直接抛错，整个服务起不来）。
    ///     旧版对这种情况只会干等 180 秒后报"启动超时"，用户无从下手；
    ///     本版立刻拿到插件名 + loader entry id，交给自动修复流程。
    /// </summary>
    private static void OnServerLine(string line)
    {
        try
        {
            lock (ServerOutLock)
            {
                serverOut.Enqueue(line);
                while (serverOut.Count > 80) serverOut.Dequeue();  // 有界，不涨内存
            }

            // 1) 带 token 的访问地址（形如 dsh web: http://127.0.0.1:3080/?token=xxx）
            var mu = Regex.Match(line, @"https?://127\.0\.0\.1:\d+/\?token=[A-Za-z0-9_\-]+");
            if (mu.Success && string.IsNullOrEmpty(serverUserUrl))
            {
                serverUserUrl = mu.Value;
                Log("已捕获服务地址（带 token）：" + serverUserUrl);
            }

            // 2) 插件装载失败
            if (line.IndexOf("plugin tree failed to load", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("failed to apply loader entry", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (string.IsNullOrEmpty(startupFailure)) startupFailure = Brief(line);
                var ml = Regex.Match(line, @"failed to apply loader entry\s+(\S+)\s+\(([^)]+)\)");
                if (ml.Success)
                {
                    if (string.IsNullOrEmpty(failingLoaderId)) failingLoaderId = ml.Groups[1].Value;
                    string pkg = ml.Groups[2].Value.Trim();
                    if (pkg.StartsWith("@", StringComparison.Ordinal) || pkg.IndexOf('/') > 0)
                    {
                        if (string.IsNullOrEmpty(failingPlugin)) failingPlugin = pkg;
                    }
                }
                Log("识别到插件装载失败: plugin=" + (failingPlugin ?? "<未知>")
                    + " loader=" + (failingLoaderId ?? "<未知>"));
            }
            else if (line.IndexOf("must be a top-level YAML array", StringComparison.OrdinalIgnoreCase) >= 0
                && string.IsNullOrEmpty(startupFailure))
            {
                // 补丁文件被改坏（例如条目全被注释掉、顶层不再是数组）也要给明确结论
                startupFailure = Brief(line);
                Log("识别到补丁文件格式错误");
            }
        }
        catch (Exception ex)
        {
            Log("服务器输出解析失败: " + ex.Message);
        }
    }

    /// <summary>
    /// 执行一条 npm 命令并返回 stdout；失败或超时返回 null。
    /// v0.6.3 防卡死要点（旧版三个坑全在这里）：
    ///  1) 先起读线程、再 WaitForExit，超时才真正生效。旧版先 ReadToEnd 再
    ///     WaitForExit：stdout 不关闭就永远停在 ReadToEnd，300 秒上限形同虚设
    ///     （实测升级下载跑了 16 分钟，界面既不成功也不失败）。
    ///  2) 超时后杀整棵进程树（cmd → npm.cmd → node）。只 Kill 那个 cmd 会留下
    ///     npm/node 孤儿进程继续下载、继续占 npx 目录的 concurrency.lock，
    ///     于是"再点一次升级"只会更慢。
    ///  3) 读线程是后台线程 + 有界 Join，收尾不会因为管道没关而挂住。
    /// </summary>
    private static string RunNpmCapture(string npmArgs, int timeoutMs)
    {
        Process p = null;
        try
        {
            // v0.6.5：同 StartViaNpx —— 绝不触碰 psi.EnvironmentVariables（本机环境有
            // HTTP_PROXY/http_proxy 大小写重复，访问该属性会抛字典冲突异常，命令直接跑不起来）。
            // 开关放在子命令**之前**，避免影响 `--` 之后透传给 node 的参数。
            var psi = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/c npm --no-fund --no-update-notifier " + npmArgs,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            p = Process.Start(psi);
            if (p == null) return null;
            activeNpmProcess = p;   // 供 RequestStop 时一并收掉

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var outDone = new ManualResetEvent(false);
            var errDone = new ManualResetEvent(false);
            var readerOut = new Thread(delegate ()
            {
                try { stdout.Append(p.StandardOutput.ReadToEnd()); }
                catch { }
                finally { outDone.Set(); }
            });
            var readerErr = new Thread(delegate ()
            {
                try { stderr.Append(p.StandardError.ReadToEnd()); }
                catch { }
                finally { errDone.Set(); }
            });
            readerOut.IsBackground = true;
            readerErr.IsBackground = true;
            readerOut.Start();
            readerErr.Start();

            if (!p.WaitForExit(timeoutMs))
            {
                Log("npm 命令超时（" + (timeoutMs / 1000) + " 秒），终止整棵进程树: " + Brief(npmArgs));
                KillProcessTree(p);
                return null;
            }
            // 进程已退出：有界等输出读完（最多 3 秒），不为了凑齐输出而无限等
            outDone.WaitOne(3000);
            errDone.WaitOne(3000);
            if (!string.IsNullOrEmpty(stderr.ToString()))
            {
                string err = stderr.ToString().Trim();
                Log("npm stderr: " + Brief(err));
            }
            return p.ExitCode == 0 ? stdout.ToString() : null;
        }
        catch (Exception ex)
        {
            Log("npm 查询异常: " + ex.Message);
            if (p != null) KillProcessTree(p);
            return null;
        }
        finally
        {
            activeNpmProcess = null;
        }
    }

    /// <summary>日志里截断长文本，避免一行几万字符。</summary>
    private static string Brief(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string one = text.Replace("\r", " ").Replace("\n", " ");
        return one.Length <= 200 ? one : one.Substring(0, 200) + "…";
    }

    /// <summary>压成一行并截断到指定长度（自检报告里回读文件内容用）。</summary>
    private static string OneLine(string text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string one = Regex.Replace(text, @"\s+", " ").Trim();
        return one.Length <= max ? one : one.Substring(0, max) + "…";
    }

    /// <summary>确认某版本当前真的可安装（dist-tags 可能领先于可下载版本）。</summary>
    private static bool VerifyVersionInstallable(string version)
    {
        if (string.IsNullOrEmpty(version)) return false;
        string output = RunNpmCapture("view @deepseek-ai/dsh@" + version + " version", 20000);
        bool ok = output != null && output.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0;
        Log("可安装性校验 " + version + ": " + (ok ? "通过" : "未通过"));
        return ok;
    }

    /// <summary>
    /// 下载并安装指定版本到本机 npx 缓存（只安装、不启动服务器）。
    /// 装好后 FindCachedCheckout(version) 即可找到它，下次启动直接使用。
    /// 超时上限 InstallTimeoutMs（30 分钟）：实测 200 MB 依赖在慢网络下要
    /// 16 分钟，旧版 5 分钟上限会把成功当失败，所以这里给足余量；超时则由
    /// RunNpmCapture 杀整棵树并如实报失败。
    /// </summary>
    private static bool InstallVersion(string version)
    {
        // npm exec 把指定版本装进 npx 缓存并执行其 dsh 命令；--version 只打印版本号，
        // 既确认安装成功，也确认该版本可运行。
        string output = RunNpmCapture(
            "exec --yes --package=@deepseek-ai/dsh@" + version + " -- dsh --version", InstallTimeoutMs);
        bool ok = output != null && output.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0;
        Log("下载安装 " + version + ": " + (ok ? "成功" : "失败"));
        return ok;
    }

    /// <summary>
    /// 后台执行版本升级：下载成功 → 记为新版本（下次启动生效）并提示；
    /// 失败 → 保持当前版本并提示。全程不阻塞启动、不影响正在运行的服务。
    /// 关键点是它**不依赖"端口空闲"**，因此在复用已有服务时也能真正完成升级。
    /// </summary>
    private static void BeginUpgradeInstall(string target)
    {
        // 单飞：同一时刻只允许一个下载安装。重复点击只会起第二个 npm 去抢同一个
        // npx 目录的 concurrency.lock，实测两次下载几乎同时结束一次（纯白等）。
        if (Interlocked.CompareExchange(ref upgradeRunning, 1, 0) != 0)
        {
            Log("忽略重复的升级请求（已有下载在进行中）: " + target);
            Msg("已经有一个版本正在下载中，请等它结束。\n\n进度会显示在托盘提示上；" +
                "若要中断，右键托盘 → 停止服务并退出。", MessageBoxIcon.Information);
            return;
        }

        var thread = new Thread(delegate ()
        {
            // 进度提示线程：每 15 秒按已用时间刷新托盘文字，让人看得出"在跑"
            // 而不是"卡住了"（旧版只有一句"正在下载…"，16 分钟里毫无变化）。
            var progressStop = new ManualResetEvent(false);
            var ticker = new Thread(delegate ()
            {
                int elapsed = 0;
                while (!progressStop.WaitOne(15000))
                {
                    elapsed += 15;
                    SetTrayText("正在下载 DeepSeek Harness " + target + "…（已 "
                        + (elapsed / 60) + " 分 " + (elapsed % 60) + " 秒）");
                }
            });
            ticker.IsBackground = true;
            ticker.Start();

            try
            {
                SetTrayText("正在下载 DeepSeek Harness " + target + "…");
                bool ok = InstallVersion(target);
                if (ok)
                {
                    pinnedVersion = target;
                    WriteVersionState(target, null, DateTime.Now); // 清 pending、记为当前版本
                    SetTrayText("DeepSeek Harness 服务运行中（新版本 " + target + " 已就绪）");
                    Msg("新版本 " + target + " 已下载完成。\n\n本次运行的服务仍是旧版本；" +
                        "停止并重新启动本程序后即使用新版本。", MessageBoxIcon.Information);
                }
                else
                {
                    WriteVersionState(pinnedVersion, target, DateTime.Now); // 清 pending、记为已拒绝
                    SetTrayText("DeepSeek Harness 服务运行中（右键可停止）");
                    Msg("新版本 " + target + " 下载失败（网络不通、超时或该版本暂不可用）。\n\n" +
                        "已保持当前版本 " + (pinnedVersion ?? "本机已装版本") + "，服务不受影响。\n" +
                        "失败原因见日志：" + LogFile, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Log("后台升级异常: " + ex.Message);
                SetTrayText("DeepSeek Harness 服务运行中（右键可停止）");
            }
            finally
            {
                progressStop.Set();
                Interlocked.Exchange(ref upgradeRunning, 0);
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    /// <summary>查询 npm registry 的 latest 版本：优先 HTTPS，失败时用 npm 命令兜底。</summary>
    private static string FetchLatestVersion()
    {
        string viaHttp = FetchLatestViaHttp();
        if (!string.IsNullOrEmpty(viaHttp)) return viaHttp;
        Log("HTTPS 查询失败，改用 npm 命令兜底…");
        return FetchLatestViaNpm();
    }

    /// <summary>HTTPS 直查 npm registry（部分受限环境会拦截 SChannel，由兜底路径接管）。</summary>
    private static string FetchLatestViaHttp()
    {
        try
        {
            ServicePointManager.Expect100Continue = false;
            var req = (HttpWebRequest)WebRequest.Create(RegistryUrl);
            req.Timeout = 4000;
            req.ReadWriteTimeout = 4000;
            req.UserAgent = "DshLauncher/0.5";
            req.KeepAlive = false;
            req.Proxy = null; // 直接连接（避免误用不可用的系统代理）
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                var m = Regex.Match(json, "\"dist-tags\"\\s*:\\s*\\{[^}]*?\"latest\"\\s*:\\s*\"([^\"]+)\"",
                    RegexOptions.Singleline);
                if (m.Success) return m.Groups[1].Value;
                Log("版本检查：npm 响应中未找到 latest 标签");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log("版本检查失败（HTTPS）: " + ex.Message);
            return null;
        }
    }

    /// <summary>npm CLI 兜底查询（与 npx 使用同一网络栈，成功率高）。</summary>
    private static string FetchLatestViaNpm()
    {
        try
        {
            // v0.6.5 两处修正：
            //  1) 不再写 psi.EnvironmentVariables（本机 HTTP_PROXY/http_proxy 大小写重复，
            //     访问该属性会抛字典冲突异常 → 兜底查询静默失效）；改用 npm 命令行开关。
            //  2) 改走 RunCapture：旧写法先 ReadToEnd 再 WaitForExit，stdout 不关闭就
            //     永远等下去，那个 10 秒上限形同虚设（与 v0.6.3 修的是同一类坑，
            //     当时漏了这一处）。
            string output = RunCapture(
                Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                "/c npm --no-fund --no-update-notifier view @deepseek-ai/dsh dist-tags.latest",
                20000);
            if (string.IsNullOrEmpty(output))
            {
                Log("npm 兜底查询失败或超时");
                return null;
            }
            var m = Regex.Match(output, "(\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.\\-]+)?)");
            if (!m.Success) return null;
            Log("npm 兜底查询成功: " + m.Groups[1].Value);
            return m.Groups[1].Value;
        }
        catch (Exception ex)
        {
            Log("npm 兜底查询异常: " + ex.Message);
            return null;
        }
    }

    /// <summary>语义化版本比较：a &gt; b 返回正数，相等 0，a &lt; b 负数。</summary>
    private static int CompareVersions(string a, string b)
    {
        string[] sa = (a ?? "").Split('-');
        string[] sb = (b ?? "").Split('-');
        int[] na = VersionNumbers(sa[0]);
        int[] nb = VersionNumbers(sb[0]);
        for (int i = 0; i < 3; i++)
        {
            int d = na[i].CompareTo(nb[i]);
            if (d != 0) return d;
        }
        bool preA = sa.Length > 1;
        bool preB = sb.Length > 1;
        if (preA != preB) return preA ? -1 : 1; // 正式版 > 预发布版
        if (!preA) return 0;
        string[] ia = sa[1].Split('.');
        string[] ib = sb[1].Split('.');
        for (int i = 0; i < Math.Max(ia.Length, ib.Length); i++)
        {
            if (i >= ia.Length) return -1;
            if (i >= ib.Length) return 1;
            int x, y;
            bool nx = int.TryParse(ia[i], out x);
            bool ny = int.TryParse(ib[i], out y);
            if (nx && ny)
            {
                if (x != y) return x.CompareTo(y);
                continue;
            }
            if (nx != ny) return nx ? -1 : 1; // 数字标识 < 字母标识
            int c = string.Compare(ia[i], ib[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return 0;
    }

    private static int[] VersionNumbers(string core)
    {
        string[] parts = (core ?? "").Split('.');
        var n = new int[3];
        for (int i = 0; i < 3 && i < parts.Length; i++) int.TryParse(parts[i], out n[i]);
        return n;
    }

    /// <summary>启动时的版本决策：读记录 → 补探测 → 查最新 → 必要时弹窗询问。</summary>
    private static string ResolveVersionAtStartup()
    {
        string version, declined, pending;
        DateTime checkedAt;
        ReadVersionState(out version, out declined, out checkedAt, out pending);

        if (string.IsNullOrEmpty(version))
        {
            version = DetectInstalledVersion();
            if (!string.IsNullOrEmpty(version)) Log("首次运行：采用本地已安装版本 " + version);
        }

        // 上次已确认的升级：本次直接执行（不再重复询问）
        if (!string.IsNullOrEmpty(pending) && pending != version)
        {
            // 关键：若该版本本机已经装好（例如上次"超时"其实早已下完，或用户
            // 手动装过），直接切过去用，绝不重复下载十几分钟。
            if (FindCachedCheckout(pending) != null)
            {
                Log("待升级版本 " + pending + " 本机已装好，直接切换（不重复下载）");
                WriteVersionState(pending, null, DateTime.Now);
                return pending;
            }
            Log("执行上次确认的升级：" + version + " → " + pending);
            requestedUpgrade = pending;
            return version ?? pending;
        }

        string latest = FetchLatestVersion();
        if (string.IsNullOrEmpty(latest))
        {
            Log("版本检查跳过（离线）。使用版本：" + (version ?? "未记录"));
            if (!string.IsNullOrEmpty(version)) WriteVersionState(version, declined, checkedAt);
            return version;
        }
        if (string.IsNullOrEmpty(version))
        {
            Log("未检测到已安装版本，采用 npm 最新版 " + latest);
            WriteVersionState(latest, null, DateTime.Now);
            return latest;
        }
        if (CompareVersions(latest, version) <= 0)
        {
            Log("版本检查：已是最新（" + version + "）");
            WriteVersionState(version, null, DateTime.Now);
            return version;
        }
        if (declined == latest && DateTime.Now - checkedAt < RecheckAfter)
        {
            Log("版本检查：已拒绝过 " + latest + "，本次不再询问");
            return version;
        }
        // dist-tags 可能领先于可下载版本：先确认真的能装，再询问
        if (!VerifyVersionInstallable(latest))
        {
            Log("版本检查：" + latest + " 当前不可安装，跳过升级询问");
            WriteVersionState(version, null, DateTime.Now);
            return version;
        }

        if (AskUpgrade(version, latest))
        {
            askedUpgradeThisRun = true;   // v0.6.7：启动后的插件更新检查不再重复问 dsh
            Log("用户选择升级：" + version + " → " + latest + "（本次尝试安装，失败自动回退）");
            requestedUpgrade = latest;
            return version;
        }
        askedUpgradeThisRun = true;
        Log("用户选择保持版本：" + version + "（已拒绝 " + latest + "）");
        WriteVersionState(version, latest, DateTime.Now);
        return version;
    }

    /// <summary>升级询问弹窗（默认按钮为"否"，回车不会误升级）。</summary>
    private static bool AskUpgrade(string current, string latest)
    {
        string text =
            "检测到 DeepSeek Harness 新版本。\n\n" +
            "    当前版本：" + current + "\n" +
            "    最新版本：" + latest + "\n\n" +
            "是否升级到 " + latest + "？\n\n" +
            "   [是]  改用新版本（本次启动即生效）\n" +
            "   [否]  继续使用 " + current + "（12 小时内不再询问）\n\n" +
            "提示：DeepSeek Harness 仍处于开发者预览阶段，升级可能导致\n" +
            "已安装的插件（浏览器桥 / auto-mode / agent-teams 等）不兼容。";
        return MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    // ================= v0.6.7：更新检查（dsh 本体 + 第三方插件）=================

    /// <summary>一条待更新项：dsh 本体，或某个 profile 里的一个第三方插件。</summary>
    private sealed class UpdateCandidate
    {
        public string Kind;        // "dsh" | "plugin"
        public string Profile;     // plugin：profile 名（dsh 为空）
        public string ProfileDir;  // plugin：profile 目录
        public string Package;     // plugin：包名
        public string Label;       // 显示名
        public string Current;     // 当前版本
        public string Latest;      // npm latest

        public string Line()
        {
            return Label + "   " + Current + "  →  " + Latest;
        }
    }

    /// <summary>本次启动是否已经就 dsh 升级问过用户（避免启动时被问两遍）。</summary>
    private static bool askedUpgradeThisRun;

    /// <summary>插件名的 latest 版本查询缓存（同一次会话里不重复问 npm）。</summary>
    private static readonly Dictionary<string, string> npmLatestCache = new Dictionary<string, string>();

    /// <summary>被"与当前 dsh 不兼容"挡下的插件更新（给日志/提示用）。</summary>
    private static List<string> skippedPluginUpdates = new List<string>();

    /// <summary>
    /// 收集可更新项（只读，不改任何东西）。
    /// 返回 null = 网络查询失败；返回空表 = 都最新。
    /// </summary>
    private static List<UpdateCandidate> CollectUpdateCandidates()
    {
        var list = new List<UpdateCandidate>();
        var skipped = new List<string>();

        // 1) dsh 本体
        string running = string.IsNullOrEmpty(pinnedVersion) ? DetectInstalledVersion() : pinnedVersion;
        string latestDsh = FetchLatestVersion();
        if (string.IsNullOrEmpty(latestDsh))
        {
            Log("更新检查：dsh 版本查询失败（离线？）");
            return null;   // 连 dsh 都查不到，就当网络不通，别再逐个查插件
        }
        if (!string.IsNullOrEmpty(running) && CompareVersions(latestDsh, running) > 0)
        {
            list.Add(new UpdateCandidate
            {
                Kind = "dsh",
                Label = "DeepSeek Harness（本体）",
                Current = running,
                Latest = latestDsh
            });
        }

        // 2) 各 profile 的第三方插件
        string runningDshForPlugins = string.IsNullOrEmpty(pinnedVersion) ? DetectInstalledVersion() : pinnedVersion;
        foreach (string dir in ListProfileDirs())
        {
            string profileName = Path.GetFileName(dir);
            string pkgPath = Path.Combine(dir, "package.json");
            string text;
            try { text = File.ReadAllText(pkgPath); }
            catch (Exception ex) { Log("读取 profile 失败 " + profileName + ": " + ex.Message); continue; }

            // 只取 dependencies 段里的条目
            var depBlock = Regex.Match(text, "\"dependencies\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
            if (!depBlock.Success) continue;
            foreach (Match e in Regex.Matches(depBlock.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
            {
                string pkg = e.Groups[1].Value;
                string spec = e.Groups[2].Value;
                if (pkg.StartsWith("@deepseek-ai/", StringComparison.Ordinal)) continue;  // 官方基础包不归这里管
                if (spec.StartsWith("link:", StringComparison.OrdinalIgnoreCase)
                    || spec.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                    || spec.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase))
                {
                    Log("更新检查：跳过本地开发插件 " + pkg + "（" + spec + "）");
                    continue;
                }
                string pluginDir = Path.Combine(dir, "node_modules", pkg);
                if (!Directory.Exists(pluginDir)) continue;
                // 符号链接/junction = 指向本地开发检查点，绝不能拿 npm 覆盖它
                if (IsReparsePoint(pluginDir))
                {
                    Log("更新检查：跳过链接型插件 " + pkg + "（指向本地开发目录）");
                    continue;
                }
                string installed = ReadPackageVersion(Path.Combine(pluginDir, "package.json"));
                if (string.IsNullOrEmpty(installed)) continue;
                string latest = FetchPackageLatest(pkg);
                if (string.IsNullOrEmpty(latest)) { Log("更新检查：" + pkg + " 版本查询失败"); continue; }
                if (CompareVersions(latest, installed) <= 0) continue;

                // v0.6.7 关键闸门：**只提示与当前 dsh 版本相容的插件版本**。
                // 实测教训：dsh-perm-gate 4.2.1 只声明支持 `>=0.1.7-rc.1 <0.1.8-0`，
                // 而本机 dsh 是 0.1.5-rc.3 —— 若照"有新版本就升"提示，用户一升级
                // 就会重演"插件在装载阶段抛错、整个 profile 起不来"的故障。
                string runningDsh = runningDshForPlugins;
                string why;
                if (!PluginVersionFitsDsh(pkg, latest, runningDsh, out why))
                {
                    Log("更新检查：跳过 " + pkg + " " + latest + "（" + why + "）");
                    skipped.Add(pkg + " " + latest + "：" + why);
                    continue;
                }

                list.Add(new UpdateCandidate
                {
                    Kind = "plugin",
                    Profile = profileName,
                    ProfileDir = dir,
                    Package = pkg,
                    Label = pkg + "（" + profileName + "）",
                    Current = installed,
                    Latest = latest
                });
            }
        }
        skippedPluginUpdates = skipped;
        return list;
    }

    /// <summary>
    /// 候选插件版本能否装在当前 dsh 上（v0.6.7 的安全闸门）。
    /// 依据：npm 上该版本声明的 engines.dsh 与 peerDependencies 里所有
    /// `@deepseek-ai/dsh-*` 约束；只要有一条不满足就不提示升级。
    /// 查不到约束（本地包、老包没写）时按"未知"处理 —— 放行，由启动前的
    /// 插件兼容预检兜底（宁可少拦，也不要因为网络问题让菜单永远空白）。
    /// </summary>
    private static bool PluginVersionFitsDsh(string pkg, string version, string runningDsh, out string why)
    {
        why = null;
        if (string.IsNullOrEmpty(runningDsh)) return true;
        string json = RunNpmCapture(
            "view " + pkg + "@" + version + " peerDependencies engines --json", 30000);
        if (string.IsNullOrEmpty(json)) return true;   // 查不到约束：交给兼容预检判定

        var ranges = new List<string>();
        var eng = Regex.Match(json, "\"dsh\"\\s*:\\s*\"([^\"]+)\"");
        if (eng.Success) ranges.Add(eng.Groups[1].Value);
        var peers = Regex.Match(json, "\"peerDependencies\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.Singleline);
        if (peers.Success)
        {
            foreach (Match e in Regex.Matches(peers.Groups[1].Value, "\"(@deepseek-ai/dsh-[^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
                ranges.Add(e.Groups[2].Value);
        }
        if (ranges.Count == 0) return true;
        foreach (string range in ranges)
        {
            if (!VersionSatisfies(range, runningDsh))
            {
                why = "该版本要求 " + range + "，当前 dsh 是 " + runningDsh;
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 简版 semver 区间判定：够用即可 —— 支持 `&gt;=A &lt;B`、`^A`、`~A`、`A`，
    /// 以及 `||` 组合（任一命中即算满足）。插件写的都是这种简单约束。
    /// </summary>
    private static bool VersionSatisfies(string range, string version)
    {
        if (string.IsNullOrEmpty(range)) return true;
        if (range.IndexOf("||", StringComparison.Ordinal) >= 0)
        {
            foreach (string part in range.Split(new string[] { "||" }, StringSplitOptions.None))
                if (VersionSatisfies(part, version)) return true;
            return false;
        }
        foreach (Match m in Regex.Matches(range,
            "(>=|<=|>|<|\\^|~|=)?\\s*([0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.\\-]+)?)"))
        {
            string op = m.Groups[1].Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : "";
            string bound = m.Groups[2].Value;
            int cmp = CompareVersions(version, bound);
            if (op == ">=" && cmp < 0) return false;
            if (op == ">" && cmp <= 0) return false;
            if (op == "<=" && cmp > 0) return false;
            if (op == "<" && cmp >= 0) return false;
            if (op == "=" && cmp != 0) return false;
            if (op == "" || op == "^")
            {
                if (cmp < 0) return false;
                if (!WithinCaretUpper(bound, version)) return false;
            }
            if (op == "~")
            {
                if (cmp < 0) return false;
                if (!WithinTildeUpper(bound, version)) return false;
            }
        }
        return true;
    }

    /// <summary>^A 的上界：A 的 major&gt;0 → 同一 major；否则同一 minor。</summary>
    private static bool WithinCaretUpper(string bound, string version)
    {
        int[] b = VersionNumbers(bound.Split('-')[0]);
        int[] v = VersionNumbers(version.Split('-')[0]);
        if (b[0] > 0) return v[0] == b[0];
        return v[0] == 0 && v[1] == b[1];
    }

    /// <summary>~A 的上界：同一个 minor 线内。</summary>
    private static bool WithinTildeUpper(string bound, string version)
    {
        int[] b = VersionNumbers(bound.Split('-')[0]);
        int[] v = VersionNumbers(version.Split('-')[0]);
        return v[0] == b[0] && v[1] == b[1];
    }

    /// <summary>路径是否是符号链接/junction（本地开发检查点的标志）。</summary>
    private static bool IsReparsePoint(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch { return false; }
    }

    private static string ReadPackageVersion(string pkgJsonPath)
    {
        try
        {
            var m = Regex.Match(File.ReadAllText(pkgJsonPath), "\"version\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    /// <summary>查一个 npm 包的 latest 版本（带缓存；失败返回 null）。</summary>
    private static string FetchPackageLatest(string pkg)
    {
        string cached;
        if (npmLatestCache.TryGetValue(pkg, out cached)) return cached;
        string output = RunNpmCapture(
            "view " + pkg + " version --json", 20000);
        string version = null;
        if (!string.IsNullOrEmpty(output))
        {
            var m = Regex.Match(output, "([0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.\\-]+)?)");
            if (m.Success) version = m.Groups[1].Value;
        }
        npmLatestCache[pkg] = version;
        return version;
    }

    /// <summary>托盘"检查 dsh / 插件更新…"：与启动时自动检查共用同一套收集与界面。</summary>
    private static void CheckUpdatesFromTray()
    {
        ShowMainWindowUpdates();
    }

    /// <summary>启动后的后台自动检查：只在确实有新版本时弹一次勾选菜单。</summary>
    private static void StartStartupUpdateCheck()
    {
        var t = new Thread(delegate ()
        {
            try
            {
                Thread.Sleep(12000);   // 等服务就绪、页面打开之后再做，不抢启动阶段的注意力
                if (stopRequested) return;
                // 等一下 UI 消息循环（勾选菜单要在 UI 线程上显示）；最多 20 秒
                for (int i = 0; i < 40 && !uiLoopRunning && !stopRequested; i++) Thread.Sleep(500);
                if (!uiLoopRunning) { Log("启动更新检查：UI 消息循环未就绪，跳过"); return; }
                List<UpdateCandidate> items = CollectUpdateCandidates();
                if (items == null || items.Count == 0)
                {
                    Log("启动更新检查：无可用更新");
                    return;
                }
                // 本次启动已经在"版本检查"里问过 dsh 升级了，就别再问一遍
                if (askedUpgradeThisRun)
                {
                    var onlyPlugins = new List<UpdateCandidate>();
                    foreach (var it in items) if (it.Kind == "plugin") onlyPlugins.Add(it);
                    items = onlyPlugins;
                }
                if (items.Count == 0) { Log("启动更新检查：仅 dsh 有新版（本次已询问过）"); return; }
                Log("启动更新检查：发现 " + items.Count + " 项可更新");
                // v0.7.0：不再用对话框打断启动 —— 只发一条气泡，由用户自己决定何时更新；
                // 控制面板若已打开，顺手刷新出列表。
                Balloon("发现 " + items.Count + " 项可更新（dsh / 插件）。\n双击托盘图标打开控制面板查看详情。",
                    ToolTipIcon.Info);
                Ui(delegate
                {
                    try { if (mainForm != null && mainForm.Visible) mainForm.RefreshUpdates(); }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                Log("启动更新检查异常: " + ex.Message);
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    /// <summary>递归复制目录（overwrite=false 时目标已存在也照写；子 node_modules 可选保留）。</summary>
    private static void CopyDir(string src, string dst, bool keepNodeModulesInDst)
    {
        Directory.CreateDirectory(dst);
        foreach (string file in Directory.GetFiles(src))
        {
            string target = Path.Combine(dst, Path.GetFileName(file));
            File.Copy(file, target, true);
        }
        foreach (string dir in Directory.GetDirectories(src))
        {
            string name = Path.GetFileName(dir);
            if (keepNodeModulesInDst && name.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
            {
                // 新包自带的 node_modules 跳过：保留目标里已装好的依赖
                continue;
            }
            CopyDir(dir, Path.Combine(dst, name), keepNodeModulesInDst);
        }
    }

    // ================= 互斥提示 =================

    private const string AlreadyRunningText =
        "DeepSeek 启动器已在运行。\n\n" +
        "如需停止服务：右键系统托盘中的 DeepSeek 图标 → 停止服务并退出。\n" +
        "如需打开网页：双击系统托盘中的 DeepSeek 图标。\n" +
        "如需检查新版本：右键系统托盘图标 → 检查更新…\n\n" +
        "（更新采用热更新方式：替换 exe 后，正在运行的旧实例不受影响，下次启动即为新版。）";

    // ================= 日志 =================

    private static void Log(string msg)
    {
        try
        {
            lock (LogLock)
            {
                var fi = new FileInfo(LogFile);
                if (fi.Exists && fi.Length > 200 * 1024) File.WriteAllText(LogFile, "");
                File.AppendAllText(LogFile,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine);
            }
        }
        catch { }
    }

    private static string LogTail(int lines)
    {
        try
        {
            if (!File.Exists(LogFile)) return "";
            string[] all = File.ReadAllLines(LogFile);
            int take = Math.Min(lines, all.Length);
            var sb = new StringBuilder();
            for (int i = all.Length - take; i < all.Length; i++) sb.AppendLine("  " + all[i]);
            return sb.ToString();
        }
        catch
        {
            return "";
        }
    }

    // ================= 自检（无 UI 副作用） =================

    /// <summary>当前所有 node 进程的 PID（防卡死测试用来判断有没有留下孤儿进程）。</summary>
    private static HashSet<int> NodeProcessIds()
    {
        var set = new HashSet<int>();
        try
        {
            foreach (Process p in Process.GetProcessesByName("node")) set.Add(p.Id);
        }
        catch { }
        return set;
    }

    /// <summary>after 里新增的 PID（即本轮新起的进程是否残留）。</summary>
    private static string LeftoverIds(HashSet<int> before, HashSet<int> after)
    {
        var list = new List<string>();
        foreach (int id in after) { if (!before.Contains(id)) list.Add(id.ToString()); }
        return list.Count == 0 ? "<none>" : string.Join(",", list.ToArray());
    }

    private static int RunSelfTest()
    {
        string outFile = Path.Combine(Path.GetTempPath(), "dsh-launcher-selftest.log");
        var sb = new StringBuilder();
        sb.AppendLine("=== DSH launcher selftest ===");
        sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        try
        {
            sb.AppendLine("version=" + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
        }
        catch (Exception ex) { sb.AppendLine("version_fail=" + ex.Message); }

        sb.AppendLine("missing=" + (MissingNodeNpx() ?? "none"));

        // 版本/升级自检：只读探测，不弹窗、不写版本状态
        try
        {
            string v, d, pend;
            DateTime c;
            ReadVersionState(out v, out d, out c, out pend);
            pinnedVersion = v;
            sb.AppendLine("state_version=" + (v ?? "<none>"));
            sb.AppendLine("state_pending=" + (pend ?? "<none>"));
            sb.AppendLine("state_declined=" + (d ?? "<none>"));
            sb.AppendLine("state_checked=" + (c > DateTime.MinValue ? c.ToString("yyyy-MM-dd HH:mm:ss") : "<none>"));
        }
        catch (Exception ex) { sb.AppendLine("state_fail=" + ex.Message); }
        sb.AppendLine("appdata_dir=" + AppDataDir);
        sb.AppendLine("version_file=" + VersionFile);
        sb.AppendLine("log_file=" + LogFile);
        sb.AppendLine("detected_installed=" + (DetectInstalledVersion() ?? "<none>"));
        string checkoutForVersion = FindCachedCheckout(pinnedVersion);
        sb.AppendLine("cached_checkout_for_" + (pinnedVersion ?? "none") + "=" + (checkoutForVersion ?? "<none>"));
        sb.AppendLine("cached_checkout_newest=" + (FindCachedCheckout(null) ?? "<none>"));
        string stLatest = FetchLatestVersion();
        sb.AppendLine("registry_latest=" + (stLatest ?? "<offline>"));
        if (!string.IsNullOrEmpty(pinnedVersion) && !string.IsNullOrEmpty(stLatest))
        {
            int cmp = CompareVersions(stLatest, pinnedVersion);
            sb.AppendLine("compare_latest_vs_pinned=" + cmp + (cmp > 0 ? " (有更新)" : " (已最新)"));
            sb.AppendLine("upgrade_installable=" + VerifyVersionInstallable(stLatest));
        }

        string testUrl = Environment.GetEnvironmentVariable("DSH_LAUNCHER_TEST_URL");
        if (string.IsNullOrEmpty(testUrl)) testUrl = Url;
        int testPort;
        try { testPort = new Uri(testUrl).Port; } catch { testPort = Port; }

        sb.AppendLine("tcp_listen(" + testPort + ")=" + TcpListening(testPort, 500));
        try
        {
            int listenPid = NetstatListenPid(testPort);
            sb.AppendLine("netstat_listen_pid(" + testPort + ")=" + (listenPid > 0 ? listenPid.ToString() : "<none>"));
            sb.AppendLine("leftover_service=" + IsLeftoverService(listenPid));
            sb.AppendLine("service_record_pid=" + (servicePid > 0 ? servicePid.ToString() : "<none>")
                + " version=" + (serviceVersion ?? "<none>")
                + " start=" + (serviceStartedAt > DateTime.MinValue
                    ? serviceStartedAt.ToString("HH:mm:ss") : "<none>"));
            sb.AppendLine("is_recorded_service=" + IsRecordedCurrentService(listenPid));
            sb.AppendLine("svcrec_version_switch_needed=" + (IsRecordedCurrentService(listenPid)
                && !string.IsNullOrEmpty(serviceVersion) && !string.IsNullOrEmpty(pinnedVersion)
                && serviceVersion != pinnedVersion));
            sb.AppendLine("reuse_decision=" + (listenPid > 0
                ? (IsRecordedCurrentService(listenPid) ? "reuse(recorded)" : (IsLeftoverService(listenPid) ? "ask(replace)" : "reuse(unknown)"))
                : "start"));

            // v0.6.6 服务记录回归测试：拿"本进程自己"当被测对象，三组断言。
            // 只在 DSH_LAUNCHER_STATE_FILE 重定向生效时跑，绝不碰真实状态文件。
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DSH_LAUNCHER_STATE_FILE")))
            {
                int selfPid;
                DateTime selfStart;
                using (var me = Process.GetCurrentProcess()) { selfPid = me.Id; selfStart = me.StartTime; }

                // 断言 1：PID + 启动时间都对得上 → 必须判定为"本程序记录的服务"
                servicePid = selfPid;
                serviceStartedAt = selfStart;
                serviceVersion = "0.1.5-rc.3-test";
                WriteVersionState("0.1.5-rc.3-test", null, DateTime.Now, null);
                string svV, svD, svP;
                DateTime svC;
                ReadVersionState(out svV, out svD, out svC, out svP);
                sb.AppendLine("svcrec_match_self=" + IsRecordedCurrentService(selfPid)
                    + " (pid=" + servicePid + " start=" + serviceStartedAt.ToString("HH:mm:ss")
                    + " ver=" + (serviceVersion ?? "<none>") + ")");

                // 断言 2：启动时间差 5 秒（模拟 PID 被系统复用）→ 必须判 false
                serviceStartedAt = serviceStartedAt.AddSeconds(5);
                sb.AppendLine("svcrec_mismatched_start_rejected=" + (!IsRecordedCurrentService(selfPid)));

                // 断言 3：清记录后，状态文件里不应再出现 service_pid
                ClearServiceRecord();
                string stText = File.Exists(VersionFile) ? File.ReadAllText(VersionFile) : "";
                sb.AppendLine("svcrec_clear_ok=" + (servicePid == 0
                    && stText.IndexOf("service_pid=", StringComparison.Ordinal) < 0));
                sb.AppendLine("svcrec_state_file=" + VersionFile);
            }
        }
        catch (Exception ex) { sb.AppendLine("leftover_check_fail=" + ex.Message); }
        sb.AppendLine("title_probe=" + (ProbePageTitle() ?? "<null>"));

        // ===== v0.6.5 新增：环境、端口占用者分类、插件兼容清单、插件修复回归 =====
        sb.AppendLine("pnpm=" + (FindOnPath("pnpm.cmd") ?? FindOnPath("pnpm.exe") ?? FindOnPath("pnpm") ?? "<none>"));
        sb.AppendLine("corepack=" + (FindOnPath("corepack.cmd") ?? "<none>"));
        sb.AppendLine("dsh_profiles_root=" + DshProfilesRoot());
        try
        {
            int opid;
            string oproc, otitle;
            int owner = ClassifyPortOwner(out opid, out oproc, out otitle);
            string ownerName = owner == PortOwnerNone ? "none"
                : owner == PortOwnerDsh ? "dsh"
                : owner == PortOwnerDshDead ? "dsh-dead" : "other";
            sb.AppendLine("port_owner=" + ownerName + " pid=" + opid
                + " proc=" + (oproc ?? "-") + " title=" + (otitle ?? "-"));
            sb.AppendLine("server_url_captured=" + (serverUserUrl ?? "<none>"));
        }
        catch (Exception ex) { sb.AppendLine("port_owner_fail=" + ex.Message); }

        try
        {
            var plugins = ScanProfilePlugins();
            sb.AppendLine("plugins_scanned=" + plugins.Count);
            foreach (var pc in plugins)
            {
                sb.AppendLine("plugin[" + pc.Profile + "]" + pc.Name + "=" + pc.Verdict
                    + " supported=" + pc.Supported
                    + " resolved=" + pc.HarnessSummary()
                    + (pc.Note != null ? " note=" + pc.Note : ""));
            }
        }
        catch (Exception ex) { sb.AppendLine("plugin_scan_fail=" + ex.Message); }

        // v0.6.7：更新检查（dsh 本体 + 第三方插件）只读预演。
        // 注意：这一项会真的去问 npm（每包一次，约 1-2 秒），所以只在自检里跑。
        try
        {
            var ups = CollectUpdateCandidates();
            if (ups == null) sb.AppendLine("update_candidates=<网络查询失败>");
            else
            {
                sb.AppendLine("update_candidates=" + ups.Count);
                foreach (var u in ups)
                    sb.AppendLine("update[" + u.Kind + "]" + u.Label + "=" + u.Current + "->" + u.Latest);
            }
            sb.AppendLine("npm_latest_cache=" + npmLatestCache.Count);
        }
        catch (Exception ex) { sb.AppendLine("update_check_fail=" + ex.Message); }

        // v0.6.7：托盘退出合并后的行为预演（只判断会走哪条分支，不做任何动作）
        try
        {
            bool haveOwn = serverProcess != null && !serverProcess.HasExited;
            int ePid = NetstatListenPid(Port);
            string branch = haveOwn ? "stop-own" : (ePid > 0 ? "ask-about-foreign" : "exit-only");
            sb.AppendLine("tray_exit_branch=" + branch + " pid=" + ePid);
        }
        catch (Exception ex) { sb.AppendLine("tray_exit_probe_fail=" + ex.Message); }

        // 插件修复回归测试：把 DSH_LAUNCHER_REPAIR_TEST 指向**profile 的临时副本**，
        // 走完整的"禁用插件"路径（备份 → 删 package.json 条目 → 删 workspace 行 →
        // 注释补丁条目并补顶层数组），然后把结果回读出来核对。绝不碰真实 profile。
        string repairRoot = Environment.GetEnvironmentVariable("DSH_LAUNCHER_REPAIR_TEST");
        if (!string.IsNullOrEmpty(repairRoot) && Directory.Exists(repairRoot))
        {
            string rp = Environment.GetEnvironmentVariable("DSH_LAUNCHER_REPAIR_PLUGIN");
            if (string.IsNullOrEmpty(rp)) rp = "@nanmicoder/dsh-auto-mode";
            string rid = Environment.GetEnvironmentVariable("DSH_LAUNCHER_REPAIR_ID");
            if (string.IsNullOrEmpty(rid)) rid = "auto-permission-mode";
            Environment.SetEnvironmentVariable("DSH_LAUNCHER_PROFILES_ROOT", repairRoot);
            try
            {
                sb.AppendLine("repair_test_root=" + repairRoot);
                sb.AppendLine("repair_plugin=" + rp);
                string rep = DisablePluginEverywhere(rp, rid);
                sb.AppendLine("repair_report=" + OneLine(rep, 700));
                foreach (string d in ListProfileDirs())
                {
                    string pj = Path.Combine(d, "package.json");
                    if (File.Exists(pj))
                        sb.AppendLine("after_package_json[" + Path.GetFileName(d) + "]="
                            + OneLine(File.ReadAllText(pj), 700));
                    string patch = Path.Combine(d, "cordis.patch.yml");
                    if (File.Exists(patch))
                        sb.AppendLine("after_patch[" + Path.GetFileName(d) + "]="
                            + OneLine(File.ReadAllText(patch), 700));
                }
            }
            catch (Exception ex) { sb.AppendLine("repair_test_fail=" + ex.Message); }
            finally { Environment.SetEnvironmentVariable("DSH_LAUNCHER_PROFILES_ROOT", null); }
        }

        sb.AppendLine("tray_slot_size=" + GetSystemMetrics(SM_CXSMICON));
        try
        {
            int traySize = GetSystemMetrics(SM_CXSMICON);
            if (traySize < 16) traySize = 16;
            if (traySize > 32) traySize = 32;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(TrayResource))
            {
                if (s == null) sb.AppendLine("icon_resource=missing");
                else using (var ic = new Icon(s, traySize, traySize))
                {
                    sb.AppendLine("icon_resource_tray(" + traySize + ")=" + ic.Width + "x" + ic.Height);
                }
            }
        }
        catch (Exception ex) { sb.AppendLine("icon_resource_fail=" + ex.Message); }

        sb.AppendLine("dsh_page_open=" + DshPageAlreadyOpenInBrowser());
        sb.AppendLine("log_write=" + (TryLogWrite() ? "ok" : "fail"));

        // 防卡死回归测试（默认关闭）：设 DSH_LAUNCHER_NPM_HANG_TEST=超时毫秒
        // 真跑一条会挂住的 npm 命令，验证 ① 超时真的生效 ② 整棵进程树被杀干净、
        // 不留孤儿 node。这是 v0.6.2 "300 秒超时形同虚设 + 留下孤儿" 的回归测试。
        string hangMs = Environment.GetEnvironmentVariable("DSH_LAUNCHER_NPM_HANG_TEST");
        int hangTimeout;
        if (!string.IsNullOrEmpty(hangMs) && int.TryParse(hangMs, out hangTimeout) && hangTimeout > 0)
        {
            var beforePids = NodeProcessIds();
            string hangResult = null;
            var hangWatch = Stopwatch.StartNew();
            var hangWorker = new Thread(delegate ()
            {
                hangResult = RunNpmCapture(
                    "exec --yes --package=@deepseek-ai/dsh@0.1.5-rc.3 -- node -e \"setTimeout(function(){},600000)\"",
                    hangTimeout);
            });
            hangWorker.IsBackground = true;
            hangWorker.Start();
            // 测试自己也有界：最多等 超时 + 30 秒，绝不让自检本身挂住
            hangWorker.Join(hangTimeout + 30000);
            long hangElapsed = (long)hangWatch.Elapsed.TotalMilliseconds;
            Thread.Sleep(1500); // 给 taskkill 收尾时间
            sb.AppendLine("npmhang_timeout_ms=" + hangTimeout);
            sb.AppendLine("npmhang_elapsed_ms=" + hangElapsed);
            sb.AppendLine("npmhang_worker_alive=" + hangWorker.IsAlive);
            sb.AppendLine("npmhang_result=" + (hangResult == null
                ? "<null:按预期超时>"
                : "unexpected:" + Brief(hangResult)));
            sb.AppendLine("npmhang_leftover_node_pids=" + LeftoverIds(beforePids, NodeProcessIds()));
        }

        // ===== v0.7.0 新增：进度引擎 / 解包器 / 状态自愈 的回归钩子 =====
        // 全部只在显式设置环境变量时运行，且都在临时目录里操作，绝不碰真实状态。

        // (a) registry 元数据：DSH_LAUNCHER_META_TEST=<pkg>[@version]
        string metaSpec = Environment.GetEnvironmentVariable("DSH_LAUNCHER_META_TEST");
        if (!string.IsNullOrEmpty(metaSpec))
        {
            try
            {
                int at = metaSpec.LastIndexOf('@');
                string mp = at > 0 ? metaSpec.Substring(0, at) : metaSpec;
                string mv = at > 0 ? metaSpec.Substring(at + 1) : null;
                if (string.IsNullOrEmpty(mv)) mv = FetchPackageLatest(mp);
                sb.AppendLine("meta[" + mp + "]_version=" + (mv ?? "<none>"));
                if (!string.IsNullOrEmpty(mv))
                {
                    VersionMeta vm = FetchVersionMeta(mp, mv);
                    sb.AppendLine("meta_tarball=" + (vm == null || vm.Tarball == null ? "<none>" : vm.Tarball));
                    sb.AppendLine("meta_shasum=" + (vm == null || vm.Shasum == null ? "<none>" : vm.Shasum));
                }
            }
            catch (Exception ex) { sb.AppendLine("meta_test_fail=" + ex.Message); }
        }

        // (b) 带真实进度的下载：DSH_LAUNCHER_DL_TEST=<url>[;<期望字节数>]
        string dlSpec = Environment.GetEnvironmentVariable("DSH_LAUNCHER_DL_TEST");
        if (!string.IsNullOrEmpty(dlSpec))
        {
            try
            {
                string[] parts = dlSpec.Split(';');
                long expect = -1;
                if (parts.Length > 1) long.TryParse(parts[1], out expect);
                string dst = Path.Combine(Path.GetTempPath(), "dsh-launcher-dltest-" + DateTime.Now.Ticks + ".bin");
                var prog = new ProgressInfo();
                var dlSw = Stopwatch.StartNew();
                string dlErr = HttpDownloadToFile(parts[0], dst, prog, null);
                dlSw.Stop();
                long actual = File.Exists(dst) ? new FileInfo(dst).Length : -1;
                sb.AppendLine("dl_error=" + (dlErr ?? "<none>"));
                sb.AppendLine("dl_bytes=" + actual + " total_reported=" + prog.Total
                    + " expect=" + (expect >= 0 ? expect.ToString() : "<unset>"));
                sb.AppendLine("dl_match=" + (expect < 0 ? "<n/a>" : (actual == expect ? "True" : "False")));
                sb.AppendLine("dl_elapsed_ms=" + (long)dlSw.Elapsed.TotalMilliseconds);
                sb.AppendLine("dl_progress_text=" + OneLine(prog.DetailText(), 200));
                try { File.Delete(dst); } catch { }
            }
            catch (Exception ex) { sb.AppendLine("dl_test_fail=" + ex.Message); }
        }

        // (c) 纯 C# 解包 .tar.gz：DSH_LAUNCHER_TAR_TEST=<tgz 路径>[;<期望文件数>]
        string tarSpec = Environment.GetEnvironmentVariable("DSH_LAUNCHER_TAR_TEST");
        if (!string.IsNullOrEmpty(tarSpec))
        {
            try
            {
                string[] parts = tarSpec.Split(';');
                int expectFiles = -1;
                if (parts.Length > 1) int.TryParse(parts[1], out expectFiles);
                string dir = Path.Combine(Path.GetTempPath(), "dsh-launcher-tartest-" + DateTime.Now.Ticks);
                bool ok = ExtractTarGz(parts[0], dir);
                int files = (ok && Directory.Exists(dir))
                    ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length : -1;
                long size = (ok && Directory.Exists(dir)) ? DirectorySize(dir, 1000000) : -1;
                sb.AppendLine("tar_ok=" + ok);
                sb.AppendLine("tar_files=" + files + " expect=" + (expectFiles >= 0 ? expectFiles.ToString() : "<unset>")
                    + " match=" + (expectFiles < 0 ? "<n/a>" : (files == expectFiles ? "True" : "False")));
                sb.AppendLine("tar_bytes=" + size);
                string pj = Path.Combine(dir, "package", "package.json");
                sb.AppendLine("tar_package_json=" + (File.Exists(pj) ? "present" : "missing"));
                if (File.Exists(pj)) sb.AppendLine("tar_package_version=" + (ReadPackageVersion(pj) ?? "<none>"));
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
            catch (Exception ex) { sb.AppendLine("tar_test_fail=" + ex.Message); }
        }

        // (d) 状态文件自愈：仅在状态文件已重定向到临时路径时执行（绝不碰真实 version.txt）
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DSH_LAUNCHER_STATE_FILE")))
        {
            try
            {
                // 连写两次，让 .bak 也承载同一个值；再破坏主文件，读取时应从 .bak 恢复
                WriteVersionState("9.9.9-heal", null, DateTime.Now, null);
                WriteVersionState("9.9.9-heal", null, DateTime.Now, null);
                bool bakOk = File.Exists(VersionFile) && File.Exists(VersionFile + ".bak");
                File.WriteAllText(VersionFile, "{{{ not a valid state file }}}\n", new UTF8Encoding(false));
                string hv, hd, hp;
                DateTime hc;
                ReadVersionState(out hv, out hd, out hc, out hp);
                sb.AppendLine("heal_bak_created=" + bakOk);
                sb.AppendLine("heal_recovered=" + (hv == "9.9.9-heal" ? "True" : "False") + " value=" + (hv ?? "<none>"));
            }
            catch (Exception ex) { sb.AppendLine("heal_test_fail=" + ex.Message); }
        }

        // (e) 插件原子替换端到端：DSH_LAUNCHER_ATOMIC_TEST=<profileDir>;<pkg>;<version>
        //     用真实 registry 包验证 下载 → SHA1 校验 → 解包 → 原子换入 → 同步 package.json。
        //     注意：务必指向一份 profile 的**临时副本**，不要指向真实 profile。
        string atomicSpec = Environment.GetEnvironmentVariable("DSH_LAUNCHER_ATOMIC_TEST");
        if (!string.IsNullOrEmpty(atomicSpec))
        {
            try
            {
                string[] parts = atomicSpec.Split(';');
                if (parts.Length >= 3)
                {
                    var aprog = new ProgressInfo();
                    string aerr = UpdatePluginAtomic(parts[0], parts[1], parts[2], aprog, null);
                    string installed = ReadPackageVersion(Path.Combine(parts[0], "node_modules", parts[1], "package.json"));
                    sb.AppendLine("atomic_error=" + (aerr ?? "<none>"));
                    sb.AppendLine("atomic_installed_version=" + (installed ?? "<none>"));
                    sb.AppendLine("atomic_match=" + (installed == parts[2] ? "True" : "False"));
                    sb.AppendLine("atomic_progress=" + OneLine(aprog.DetailText(), 200));
                    string pj = Path.Combine(parts[0], "package.json");
                    string spec = null;
                    if (File.Exists(pj))
                    {
                        var m = Regex.Match(File.ReadAllText(pj),
                            "\"" + Regex.Escape(parts[1]) + "\"\\s*:\\s*\"([^\"]*)\"");
                        if (m.Success) spec = m.Groups[1].Value;
                    }
                    sb.AppendLine("atomic_package_json_spec=" + (spec ?? "<none>"));
                    // 结构完整性：换入后不应残留 .old-* / .dsh-launcher-staging-* 目录
                    int leftovers = 0;
                    string nm = Path.Combine(parts[0], "node_modules");
                    if (Directory.Exists(nm))
                    {
                        foreach (string d in Directory.GetDirectories(nm))
                        {
                            string n = Path.GetFileName(d);
                            if (n.StartsWith(".dsh-launcher-staging-", StringComparison.Ordinal)) leftovers++;
                        }
                        string tgt = Path.Combine(nm, parts[1].Replace('/', Path.DirectorySeparatorChar));
                        string parent = Path.GetDirectoryName(tgt);
                        string baseName = Path.GetFileName(tgt);
                        if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                        {
                            foreach (string d in Directory.GetDirectories(parent))
                            {
                                if (Path.GetFileName(d).StartsWith(baseName + ".old-", StringComparison.Ordinal)) leftovers++;
                            }
                        }
                    }
                    sb.AppendLine("atomic_leftover_dirs=" + leftovers);
                }
                else sb.AppendLine("atomic_test_fail=需要 <profileDir>;<pkg>;<version>");
            }
            catch (Exception ex) { sb.AppendLine("atomic_test_fail=" + ex.Message); }
        }

        // (f) 主窗口构造冒烟测试：DSH_LAUNCHER_UI_TEST=1
        //     构造真实的主窗口、强制创建句柄并跑一遍布局与进度渲染，验证新 UI 不会抛异常。
        //     不调用 Show()，所以屏幕上不会闪窗口。
        if (Environment.GetEnvironmentVariable("DSH_LAUNCHER_UI_TEST") == "1")
        {
            try
            {
                var mf = new MainForm();
                try
                {
                    sb.AppendLine(mf.SmokeTest());
                    string shotDir = Path.Combine(Path.GetTempPath(), "dsh-launcher-ui-shots");
                    try { Directory.CreateDirectory(shotDir); } catch { }
                    sb.AppendLine(mf.RenderShots(shotDir));
                }
                finally { mf.Dispose(); }

                // 进度条渲染路径（百分比 / 未知总量走 marquee / 完成 / 失败）
                var cand = new UpdateCandidate { Kind = "plugin", Label = "demo-plugin", Current = "1.0.0", Latest = "1.1.0" };
                var row = new UpdateRow(cand);
                try
                {
                    var pi = new ProgressInfo();
                    pi.Phase = "下载中";
                    pi.Received = 500;
                    pi.Total = 1000;
                    pi.BytesPerSec = 123456;
                    pi.Elapsed = TimeSpan.FromSeconds(3);
                    row.ShowRunning();
                    row.ShowProgress(pi);
                    sb.AppendLine("ui_row_percent=" + pi.Percent());
                    sb.AppendLine("ui_row_bar_value=" + row.BarValue);
                    sb.AppendLine("ui_row_detail=" + OneLine(pi.DetailText(), 160));
                    var pi2 = new ProgressInfo();
                    pi2.Phase = "下载并安装依赖";
                    pi2.Received = 2 * 1024 * 1024;
                    pi2.Total = -1;
                    row.ShowProgress(pi2);
                    sb.AppendLine("ui_row_unknown_total_detail=" + OneLine(pi2.DetailText(), 160));
                    row.ShowDone(null);
                    row.ShowDone("模拟失败原因");
                    sb.AppendLine("ui_row_render_ok=True");
                }
                finally { row.Dispose(); }

                sb.AppendLine("ui_test_ok=True");
            }
            catch (Exception ex) { sb.AppendLine("ui_test_fail=" + ex.Message + " @ " + Brief(ex.StackTrace)); }
        }

        try
        {
            File.WriteAllText(outFile, sb.ToString());
        }
        catch (Exception ex)
        {
            outFile = Path.Combine(Path.GetTempPath(), "dsh-launcher-selftest-" + DateTime.Now.Ticks + ".log");
            try { File.WriteAllText(outFile, sb.ToString() + "\nfirst_write_fail=" + ex.Message); } catch { }
        }
        sb.AppendLine("report=" + outFile);
        try { File.AppendAllText(outFile, "report=" + outFile + Environment.NewLine); } catch { }
        return 0;
    }

    private static bool TryLogWrite()
    {
        try
        {
            string tmp = Path.Combine(Path.GetTempPath(), "dsh-launcher-logtest-" + DateTime.Now.Ticks + ".tmp");
            File.WriteAllText(tmp, "ok");
            File.Delete(tmp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ================= 浏览器路径与进程清理 =================

    private static string GetDefaultBrowserPath()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice"))
            {
                string progId = key == null ? null : key.GetValue("ProgId") as string;
                if (string.IsNullOrEmpty(progId)) return null;

                using (var cmdKey = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                {
                    string command = cmdKey == null ? null : cmdKey.GetValue("") as string;
                    return ParseExePath(command);
                }
            }
        }
        catch
        {
            return null;
        }
    }

    private static string ParseExePath(string command)
    {
        if (string.IsNullOrEmpty(command)) return null;
        command = command.Trim();
        if (command.StartsWith("\"", StringComparison.Ordinal))
        {
            int end = command.IndexOf('"', 1);
            if (end > 1) return command.Substring(1, end - 1);
            return null;
        }
        int sp = command.IndexOf(' ');
        return sp < 0 ? command : command.Substring(0, sp);
    }

    private static bool BrowserProcessExists(string browserName)
    {
        try
        {
            return Process.GetProcessesByName(browserName).Length > 0;
        }
        catch
        {
            return true;
        }
    }

    private static void KillProcessTree(Process proc)
    {
        try
        {
            if (proc == null || proc.HasExited) return;
            KillProcessTree(proc.Id);
        }
        catch { }
    }

    /// <summary>按 PID 结束整棵进程树（taskkill /F /T）。有界：最多等 5 秒。</summary>
    private static void KillProcessTree(int pid)
    {
        try
        {
            if (pid <= 0) return;
            Process k = Process.Start(new ProcessStartInfo("taskkill.exe", "/F /T /PID " + pid)
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });
            if (k != null) k.WaitForExit(5000);
        }
        catch { }
    }

    /// <summary>
    /// 查询监听指定端口的进程 PID（解析 `netstat -ano -p tcp`，失败返回 0）。
    /// 为什么需要它：判断 3080 上的服务是不是"本程序启动的"——不是的话它很可能是
    /// 被强杀的旧启动器留下的孤儿，本程序无法停止它，于是记录的新版本永远不生效。
    /// </summary>
    private static int NetstatListenPid(int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = "/c netstat -ano -p tcp",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var p = Process.Start(psi);
            if (p == null) return 0;
            var sb = new StringBuilder();
            var done = new ManualResetEvent(false);
            var reader = new Thread(delegate ()
            {
                try { sb.Append(p.StandardOutput.ReadToEnd()); }
                catch { }
                finally { done.Set(); }
            });
            reader.IsBackground = true;
            reader.Start();
            if (!p.WaitForExit(8000))
            {
                KillProcessTree(p);   // 有界：netstat 卡住就杀掉，绝不干等
                Log("netstat 超时，无法确定 3080 的占用进程");
                return 0;
            }
            done.WaitOne(3000);
            string needle = ":" + port + " ";
            foreach (string line in sb.ToString().Split('\n'))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;
                if (t.IndexOf("LISTENING", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (t.IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                var m = Regex.Match(t, @"\s(\d+)\s*$");
                if (m.Success)
                {
                    int pid;
                    if (int.TryParse(m.Groups[1].Value, out pid)) return pid;
                }
            }
        }
        catch (Exception ex)
        {
            Log("netstat 解析失败: " + ex.Message);
        }
        return 0;
    }

    /// <summary>安全取某进程的启动时间（取不到返回 null）。</summary>
    private static DateTime? SafeStartTime(int pid)
    {
        if (pid <= 0) return null;
        try
        {
            using (var p = Process.GetProcessById(pid)) return p.StartTime;
        }
        catch { return null; }
    }

    /// <summary>
    /// 3080 上的服务是否就是"本程序上次启动的那个"（v0.6.6）。
    /// 判定用三要素：PID 相同 + 启动时间一致（防 PID 被系统复用）+ 有版本记录。
    /// </summary>
    private static bool IsRecordedCurrentService(int pid)
    {
        if (pid <= 0 || servicePid <= 0 || pid != servicePid) return false;
        DateTime? actual = SafeStartTime(pid);
        if (actual == null || serviceStartedAt == DateTime.MinValue) return false;
        return Math.Abs((actual.Value - serviceStartedAt).TotalSeconds) < 2;
    }

    /// <summary>把本程序刚启动的服务记进状态文件（PID + 启动时间 + 版本）。</summary>
    private static void RecordOurService()
    {
        try
        {
            int pid = NetstatListenPid(Port);
            if (pid <= 0)
            {
                Log("服务已就绪，但没能从 netstat 认出监听进程，跳过服务记录");
                return;
            }
            string v, d, pend;
            DateTime c;
            ReadVersionState(out v, out d, out c, out pend);   // 先读，避免覆盖其它字段
            servicePid = pid;
            serviceVersion = pinnedVersion;
            DateTime? st = SafeStartTime(pid);
            serviceStartedAt = st.HasValue ? st.Value : DateTime.Now;
            WriteVersionState(v ?? pinnedVersion, d, c, pend);
            Log("已记录本程序启动的服务：PID " + pid + "，版本 " + (serviceVersion ?? "未记录"));
        }
        catch (Exception ex)
        {
            Log("服务记录写入失败: " + ex.Message);
        }
    }

    /// <summary>清掉服务记录（本程序把自己的服务停掉后调用，避免留下失效的 PID）。</summary>
    private static void ClearServiceRecord()
    {
        try
        {
            if (servicePid <= 0) return;
            string v, d, pend;
            DateTime c;
            ReadVersionState(out v, out d, out c, out pend);
            servicePid = 0;
            serviceStartedAt = DateTime.MinValue;
            serviceVersion = null;
            WriteVersionState(v ?? pinnedVersion, d, c, pend);
        }
        catch { }
    }

    /// <summary>询问是否把"本程序上次启动的服务"换成新的记录版本（托盘切版本后用）。</summary>
    private static bool AskSwitchRecordedService(int pid)
    {
        string from = string.IsNullOrEmpty(serviceVersion) ? "旧版本" : serviceVersion;
        string to = string.IsNullOrEmpty(pinnedVersion) ? "记录版本" : pinnedVersion;
        string text =
            "3080 上运行的是本程序上次启动的服务：" + from + "（PID " + pid + "）。\n\n" +
            "你当前的记录版本已改为 " + to + "，但正在跑的还是 " + from + "。\n\n" +
            "是否结束它并用 " + to + " 重新启动？\n\n" +
            "   [是]  结束 " + from + " 并启动 " + to + "（页面会断开，稍后刷新即可）\n" +
            "   [否]  保持现状，继续用 " + from;
        return MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1) == DialogResult.Yes;
    }

    /// <summary>该 PID 是否"早于本程序启动"——是则多半是别人留下的孤儿服务。</summary>
    private static bool IsLeftoverService(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            DateTime mine;
            using (var me = Process.GetCurrentProcess()) mine = me.StartTime;
            DateTime? theirs = null;
            try { using (var other = Process.GetProcessById(pid)) theirs = other.StartTime; }
            catch { return false; }
            return theirs.HasValue && theirs.Value < mine;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>询问是否结束"不属于本程序"的 3080 服务，改用记录版本启动。</summary>
    private static bool AskReplaceLeftover(int pid)
    {
        string want = string.IsNullOrEmpty(pinnedVersion) ? "本机记录的版本" : pinnedVersion;
        string text =
            "3080 端口上的 DeepSeek 服务不是本程序启动的（PID " + pid + "，比本程序更早启动）。\n\n" +
            "这种服务本程序停不掉，只能复用它 —— 如果它是旧版本，你记录的 "
            + want + " 就永远不会生效（这正是“重启了却没换版本”的原因）。\n\n" +
            "是否结束它，并用 " + want + " 重新启动？\n\n" +
            "   [是]  结束该服务并启动 " + want + "（浏览器里的页面会断开，稍后刷新即可）\n" +
            "   [否]  继续复用它（版本可能仍是旧的）";
        return MessageBox.Show(text, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1) == DialogResult.Yes;
    }

    /// <summary>有界等待端口不再被监听（每 500ms 探测一次）。</summary>
    private static bool WaitPortFree(int port, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (!TcpListening(port, 400)) return true;
            Thread.Sleep(500);
        }
        return !TcpListening(port, 400);
    }

    // ============================================================================
    // v0.7.0：进度感知的下载/安装引擎
    // ----------------------------------------------------------------------------
    // 为什么重写这一块：旧版升级只有托盘上一句“正在下载…”（每 15 秒刷一次），
    // 插件升级更是黑盒（npm pack 无输出）。用户既看不出“在跑”还是“卡住”，
    // 也拿不到速度/剩余时间，违反“防卡死 + 只报证据”的纪律。
    // 新引擎直接对 registry 的 tarball 发 HTTP 请求：只要服务端给了
    // Content-Length，就能给出**真实字节进度 + 实时速度 + 预计剩余时间**；
    // 解包/校验/替换各自成一个可观测阶段。
    // ============================================================================

    /// <summary>一条更新任务的实时进度（UI 与托盘共同读取）。</summary>
    private sealed class ProgressInfo
    {
        public string Phase = "等待中";
        public long Received;
        public long Total = -1;              // -1 / 0 = 未知总量（此时不显示百分比，只显示已下载量）
        public double BytesPerSec;
        public TimeSpan Elapsed;
        public string Message;
        public bool Finished;
        public bool Failed;
        public DateTime StartedAt = DateTime.Now;

        public int Percent()
        {
            if (Total <= 0) return -1;
            long r = Received < 0 ? 0 : Received;
            long pct = r * 100L / Total;
            if (pct > 100) pct = 100;
            return (int)pct;
        }

        /// <summary>给界面用的一行说明：阶段 / 进度 / 速度 / 已用时间 / 剩余时间。</summary>
        public string DetailText()
        {
            var sb = new StringBuilder();
            sb.Append(Phase);
            if (Total > 0)
            {
                int pct = Percent();
                sb.Append(" ").Append(pct).Append("%（")
                  .Append(FormatBytes(Received)).Append(" / ").Append(FormatBytes(Total)).Append("）");
            }
            else if (Received > 0)
            {
                sb.Append(" 已下载 ").Append(FormatBytes(Received));
            }
            if (BytesPerSec > 1) sb.Append(" · ").Append(FormatSpeed(BytesPerSec));
            if (Elapsed.TotalSeconds >= 1)
            {
                sb.Append(" · 已用 ").Append(FormatDuration(Elapsed));
                if (Total > 0 && BytesPerSec > 1)
                {
                    double remain = (Total - Received) / BytesPerSec;
                    if (remain > 0 && remain < 86400) sb.Append(" · 剩余约 ").Append(FormatDuration(TimeSpan.FromSeconds(remain)));
                }
            }
            if (!string.IsNullOrEmpty(Message)) sb.Append(" · ").Append(Message);
            return sb.ToString();
        }
    }

    private static string FormatBytes(long b)
    {
        if (b < 0) return "?";
        if (b < 1024) return b + " B";
        double kb = b / 1024.0;
        if (kb < 1024) return kb.ToString("0.0") + " KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return mb.ToString("0.0") + " MB";
        return (mb / 1024.0).ToString("0.00") + " GB";
    }

    private static string FormatSpeed(double bps)
    {
        if (bps <= 1) return "";
        return FormatBytes((long)bps) + "/s";
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return ((int)t.TotalSeconds) + " 秒";
        if (t.TotalMinutes < 60) return ((int)t.TotalMinutes) + " 分 " + t.Seconds + " 秒";
        return ((int)t.TotalHours) + " 时 " + t.Minutes + " 分";
    }

    /// <summary>简易 HTTP GET → 字符串（用于取 registry 元数据；失败返回 null）。</summary>
    private static string HttpGetString(string url, int timeoutMs)
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.UserAgent = "DshLauncher/" + LauncherVersion;
            req.AllowAutoRedirect = true;
            req.KeepAlive = false;
            req.Proxy = null;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            Log("HTTP 请求失败 " + url + " : " + ex.Message);
            return null;
        }
    }

    /// <summary>某个 npm 版本的下载信息（tarball 直链 + SHA1 校验和）。</summary>
    private sealed class VersionMeta
    {
        public string Tarball;
        public string Shasum;
        public long UnpackedSize = -1;
    }

    /// <summary>
    /// 从 npm registry 取指定包指定版本的 tarball 直链与校验和。
    /// 形如 https://registry.npmjs.org/@scope%2fname/1.2.3 —— 这一步替代了
    /// 旧版“npm pack 黑盒下载”，是新进度条能拿到总字节数的前提。
    /// </summary>
    private static VersionMeta FetchVersionMeta(string pkg, string version)
    {
        string url = "https://registry.npmjs.org/" + pkg.Replace("/", "%2f") + "/" + version;
        string json = HttpGetString(url, 20000);
        if (string.IsNullOrEmpty(json)) return null;
        var meta = new VersionMeta();
        var mt = Regex.Match(json, "\"tarball\"\\s*:\\s*\"([^\"]+)\"");
        if (mt.Success) meta.Tarball = mt.Groups[1].Value.Replace("\\/", "/");
        var ms = Regex.Match(json, "\"shasum\"\\s*:\\s*\"([0-9a-fA-F]{40})\"");
        if (ms.Success) meta.Shasum = ms.Groups[1].Value.ToLowerInvariant();
        var mu = Regex.Match(json, "\"unpackedSize\"\\s*:\\s*(\\d+)");
        if (mu.Success) { long v; if (long.TryParse(mu.Groups[1].Value, out v)) meta.UnpackedSize = v; }
        if (string.IsNullOrEmpty(meta.Tarball)) return null;
        return meta;
    }

    /// <summary>
    /// 带真实进度、**带重试与断点续传**的 HTTP 下载。
    /// 为什么需要重试：实测本机网络会在下载末尾（98%）瞬时卡死，旧实现一次失败就
    /// 前功尽弃（正是"0.2.0-rc.2 下载失败"那类现象）。现在：
    ///   · 失败最多重试 3 次；
    ///   · 重试用 HTTP Range 从已收到的字节继续（服务端不支持 Range 时自动从头来）；
    ///   · 每次失败/重试都写进日志与进度文本，卡顿期间也持续刷新"已用时间"。
    /// 返回 null = 成功；否则为失败原因（可直接展示给用户）。
    /// </summary>
    private static string HttpDownloadToFile(string url, string destPath, ProgressInfo p, ManualResetEvent cancel)
    {
        const int maxAttempts = 3;
        string lastErr = null;
        long written = 0;
        p.Received = 0;
        var sw = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 30000;            // 连接/响应头超时
                req.ReadWriteTimeout = 60000;   // 单次读取停顿上限（超过即判卡死，交给重试）
                req.UserAgent = "DshLauncher/" + LauncherVersion;
                req.AllowAutoRedirect = true;
                req.KeepAlive = false;
                req.Proxy = null;
                if (written > 0) req.AddRange(written);   // 断点续传

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    bool resumed = resp.StatusCode == HttpStatusCode.PartialContent;
                    if (!resumed && written > 0)
                    {
                        // 服务端不认 Range：只能重来
                        Log("下载：服务端未支持断点续传，从头下载");
                        written = 0;
                        p.Received = 0;
                    }
                    if (resumed && resp.ContentLength >= 0) p.Total = written + resp.ContentLength;
                    else p.Total = resp.ContentLength;

                    long lastBytes = written;
                    TimeSpan lastT = sw.Elapsed;
                    using (var dst = new FileStream(destPath, resumed ? FileMode.Append : FileMode.Create,
                               FileAccess.Write, FileShare.None))
                    using (var src = resp.GetResponseStream())
                    {
                        byte[] buf = new byte[65536];
                        int n;
                        while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (cancel != null && cancel.WaitOne(0)) { p.Message = "已取消"; return "已取消"; }
                            dst.Write(buf, 0, n);
                            written += n;
                            p.Received = written;
                            TimeSpan now = sw.Elapsed;
                            if ((now - lastT).TotalMilliseconds >= 400)
                            {
                                double dt = (now - lastT).TotalSeconds;
                                if (dt > 0) p.BytesPerSec = (written - lastBytes) / dt;
                                lastBytes = written;
                                lastT = now;
                            }
                            p.Elapsed = now;
                        }
                    }
                }

                p.Elapsed = sw.Elapsed;
                p.BytesPerSec = 0;

                // 已知总量却没下满 = 被服务端/网络截断，继续续传
                if (p.Total > 0 && written < p.Total)
                {
                    lastErr = "下载不完整（" + FormatBytes(written) + " / " + FormatBytes(p.Total) + "）";
                    if (attempt < maxAttempts && (cancel == null || !cancel.WaitOne(0)))
                    {
                        p.Message = "第 " + attempt + " 次不完整，正在续传…";
                        Log("下载：" + lastErr + "，第 " + (attempt + 1) + " 次续传");
                        Thread.Sleep(800);
                        continue;
                    }
                    return lastErr;
                }
                p.Message = null;
                return null;
            }
            catch (Exception ex)
            {
                p.Elapsed = sw.Elapsed;
                lastErr = ex.Message;
                Log("下载第 " + attempt + " 次失败：" + lastErr + "（已收到 " + FormatBytes(written) + "）");
                if (cancel != null && cancel.WaitOne(0)) return "已取消";
                if (attempt >= maxAttempts) return lastErr;
                p.Message = "第 " + attempt + " 次失败，1.2 秒后重试…";
                Thread.Sleep(1200);
            }
        }
        return lastErr ?? "下载失败";
    }

    private static string Sha1OfFile(string path)
    {
        try
        {
            using (var sha = SHA1.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] h = sha.ComputeHash(fs);
                var sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { return null; }
    }

    /// <summary>目录总字节数（有界：最多数 maxEntries 个文件，避免大目录卡住采样线程）。</summary>
    private static long DirectorySize(string dir, int maxEntries)
    {
        long total = 0;
        int count = 0;
        var stack = new Stack<string>();
        stack.Push(dir);
        while (stack.Count > 0 && count < maxEntries)
        {
            string d = stack.Pop();
            try
            {
                foreach (string f in Directory.GetFiles(d))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                    count++;
                    if (count >= maxEntries) break;
                }
                foreach (string sd in Directory.GetDirectories(d)) stack.Push(sd);
            }
            catch { }
        }
        return total;
    }

    // ---------- 纯 C# tar.gz 解包（不依赖系统 tar.exe，兼容没有 tar 的旧 Windows）----------

    private static bool ReadExact(Stream s, byte[] buf, int count)
    {
        int off = 0;
        while (off < count)
        {
            int n = s.Read(buf, off, count - off);
            if (n <= 0) return false;
            off += n;
        }
        return true;
    }

    private static bool IsAllZero(byte[] b)
    {
        for (int i = 0; i < b.Length; i++) if (b[i] != 0) return false;
        return true;
    }

    private static string ReadStr(byte[] b, int off, int len)
    {
        int end = off;
        while (end < off + len && b[end] != 0) end++;
        return Encoding.UTF8.GetString(b, off, end - off).Trim();
    }

    private static long ParseOctal(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        s = s.Trim().Trim('\0').Trim();
        long r = 0;
        foreach (char c in s)
        {
            if (c < '0' || c > '7') break;
            r = r * 8 + (c - '0');
        }
        return r;
    }

    private static void SkipBytes(Stream s, long count)
    {
        if (count <= 0) return;
        byte[] buf = new byte[8192];
        long left = count;
        while (left > 0)
        {
            int n = s.Read(buf, 0, (int)Math.Min((long)buf.Length, left));
            if (n <= 0) break;
            left -= n;
        }
    }

    private static void SkipPadding(Stream s, long size)
    {
        long pad = (512 - (size % 512)) % 512;
        if (pad > 0) SkipBytes(s, pad);
    }

    private static void CopyN(Stream src, Stream dst, long count)
    {
        byte[] buf = new byte[65536];
        long left = count;
        while (left > 0)
        {
            int n = src.Read(buf, 0, (int)Math.Min((long)buf.Length, left));
            if (n <= 0) break;
            dst.Write(buf, 0, n);
            left -= n;
        }
    }

    private static string ParsePaxPath(byte[] data)
    {
        try
        {
            string text = Encoding.UTF8.GetString(data);
            foreach (string line in text.Split('\n'))
            {
                if (line.Length == 0) continue;
                int sp = line.IndexOf(' ');
                if (sp <= 0) continue;
                int eq = line.IndexOf('=', sp + 1);
                if (eq <= 0) continue;
                string key = line.Substring(sp + 1, eq - sp - 1);
                if (key == "path") return line.Substring(eq + 1);
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 纯 C# 解包 .tar.gz。支持 ustar 的 prefix 长路径、PAX 扩展头('x') 与
    /// GNU longname('L')；对 ../ 路径穿越做防御。返回 false 表示解包失败。
    /// </summary>
    private static bool ExtractTarGz(string tgzPath, string destDir)
    {
        try
        {
            Directory.CreateDirectory(destDir);
            string rootFull = Path.GetFullPath(destDir);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                rootFull += Path.DirectorySeparatorChar;

            using (var fs = File.OpenRead(tgzPath))
            using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            {
                byte[] header = new byte[512];
                string pendingName = null;
                while (true)
                {
                    if (!ReadExact(gz, header, 512)) break;
                    if (IsAllZero(header)) break;                 // 归档结束块

                    string name = ReadStr(header, 0, 100);
                    long size = ParseOctal(ReadStr(header, 124, 12));
                    char type = (char)header[156];
                    string prefix = ReadStr(header, 345, 155);

                    // 扩展头：先吃掉它的数据，名字留给下一个真实条目
                    if (type == 'x' || type == 'g' || type == 'L' || type == 'K')
                    {
                        byte[] data = new byte[size];
                        if (size > 0) ReadExact(gz, data, (int)size);
                        SkipPadding(gz, size);
                        if (type == 'x') { string pn = ParsePaxPath(data); if (pn != null) pendingName = pn; }
                        else if (type == 'L') pendingName = Encoding.UTF8.GetString(data).TrimEnd('\0');
                        continue;
                    }

                    if (!string.IsNullOrEmpty(prefix) && name.IndexOf('/') < 0) name = prefix + "/" + name;
                    if (pendingName != null) { name = pendingName; pendingName = null; }

                    string rel = name.Replace('\\', '/').TrimStart('/');
                    if (rel.StartsWith("./", StringComparison.Ordinal)) rel = rel.Substring(2);
                    if (rel.Length == 0)
                    {
                        if (size > 0) { SkipBytes(gz, size); SkipPadding(gz, size); }
                        continue;
                    }

                    string outPath = Path.GetFullPath(Path.Combine(rootFull, rel.Replace('/', Path.DirectorySeparatorChar)));
                    if (!outPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("解包：跳过越界路径 " + rel);
                        if (size > 0) { SkipBytes(gz, size); SkipPadding(gz, size); }
                        continue;
                    }

                    bool isDir = (type == '5') || (rel.EndsWith("/", StringComparison.Ordinal) && size == 0);
                    if (isDir)
                    {
                        Directory.CreateDirectory(outPath);
                        if (size > 0) { SkipBytes(gz, size); SkipPadding(gz, size); }
                        continue;
                    }

                    if (type == '0' || type == '\0' || type == ' ' || type == '7')
                    {
                        string parent = Path.GetDirectoryName(outPath);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        using (var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            CopyN(gz, outFs, size);
                        }
                        SkipPadding(gz, size);
                        continue;
                    }

                    // 符号链接等其它类型：跳过内容（npm 包里不需要）
                    if (size > 0) { SkipBytes(gz, size); SkipPadding(gz, size); }
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Log("tar.gz 解包失败: " + ex.Message);
            return false;
        }
    }

    // ---------- 原子插件替换 ----------

    /// <summary>
    /// 文件级插件更新（v0.7.0 重写为“可观测 + 原子 + 可回滚”）：
    ///   ① 取 registry 元数据 → ② 带进度下载 tarball → ③ SHA1 校验
    ///   → ④ 解包 → ⑤ 在**同一卷**上拼装 staging（并保留原插件的 node_modules）
    ///   → ⑥ 备份原目录 → ⑦ rename 换入（失败自动 rename 回滚） → ⑧ 同步 package.json。
    /// 返回 null = 成功；否则返回可展示的失败原因。progress 会持续更新。
    /// </summary>
    private static string UpdatePluginAtomic(string profileDir, string pkg, string latest,
        ProgressInfo progress, ManualResetEvent cancel)
    {
        string nm = Path.Combine(profileDir, "node_modules");
        string target = Path.Combine(nm, pkg);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        string work = Path.Combine(Path.GetTempPath(), "dsh-launcher-upd-" + stamp);
        // staging 必须与目标同卷（node_modules 下），才能用 rename 原子换入
        string staging = Path.Combine(nm, ".dsh-launcher-staging-" + stamp);
        string backupDir = Path.Combine(profileDir, ".backup-update-" + stamp, pkg.Replace('/', '_'));
        string oldDir = target + ".old-" + stamp;
        try
        {
            if (!Directory.Exists(target)) return "插件目录不存在：" + target;

            // ① 元数据
            progress.Phase = "查询版本信息";
            progress.Total = -1;
            progress.Received = 0;
            progress.BytesPerSec = 0;
            VersionMeta meta = FetchVersionMeta(pkg, latest);
            if (meta == null || string.IsNullOrEmpty(meta.Tarball))
                return "无法获取下载地址（网络不通，或该版本在 registry 上不存在）";

            // ② 下载（真实字节进度）
            Directory.CreateDirectory(work);
            string tgz = Path.Combine(work, "package.tgz");
            progress.Phase = "下载中";
            progress.StartedAt = DateTime.Now;
            string derr = HttpDownloadToFile(meta.Tarball, tgz, progress, cancel);
            if (derr != null) return "下载失败：" + derr;

            // ③ 校验
            progress.Phase = "校验完整性";
            if (!string.IsNullOrEmpty(meta.Shasum))
            {
                string actual = Sha1OfFile(tgz);
                if (string.IsNullOrEmpty(actual) || !string.Equals(actual, meta.Shasum, StringComparison.OrdinalIgnoreCase))
                    return "完整性校验失败（SHA1 不符，下载可能被截断）";
            }

            // ④ 解包
            progress.Phase = "解包中";
            string ext = Path.Combine(work, "x");
            if (!ExtractTarGz(tgz, ext)) return "解包失败（tar.gz 损坏或读写出错）";
            string pkgSrc = Path.Combine(ext, "package");
            if (!Directory.Exists(pkgSrc)) return "解包失败（压缩包里没有 package 目录）";

            string newVersion = ReadPackageVersion(Path.Combine(pkgSrc, "package.json"));
            if (string.IsNullOrEmpty(newVersion)) return "新包缺少 package.json";
            if (CompareVersions(newVersion, latest) != 0)
                return "下载到的版本是 " + newVersion + "，与预期 " + latest + " 不符";

            // ⑤ 拼装 staging：新文件 + 保留原插件的 node_modules（依赖）
            progress.Phase = "准备文件";
            Directory.CreateDirectory(staging);
            CopyDir(pkgSrc, staging, false);
            string existingNm = Path.Combine(target, "node_modules");
            if (Directory.Exists(existingNm))
                CopyDir(existingNm, Path.Combine(staging, "node_modules"), false);

            // ⑥ 备份原目录（整份，失败可人工回滚）
            progress.Phase = "备份原版本";
            CopyDir(target, backupDir, false);

            // ⑦ 原子换入
            progress.Phase = "替换文件";
            bool movedOld = false;
            try
            {
                Directory.Move(target, oldDir);
                movedOld = true;
                Directory.Move(staging, target);
            }
            catch (Exception ex)
            {
                // 回滚：把原目录搬回去
                try
                {
                    if (movedOld && !Directory.Exists(target) && Directory.Exists(oldDir))
                        Directory.Move(oldDir, target);
                }
                catch (Exception rex) { Log("回滚失败（原目录仍在 " + oldDir + "）: " + rex.Message); }
                return "替换失败（已尝试回滚）：" + ex.Message;
            }

            // 覆盖后校验；失败立即回滚
            string after = ReadPackageVersion(Path.Combine(target, "package.json"));
            if (after != newVersion)
            {
                try
                {
                    if (Directory.Exists(target)) Directory.Delete(target, true);
                    if (Directory.Exists(oldDir)) Directory.Move(oldDir, target);
                }
                catch (Exception rex) { Log("覆盖校验失败后的回滚出错（原目录 " + oldDir + "）: " + rex.Message); }
                return "覆盖后版本号校验失败（已回滚）";
            }
            try { if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true); } catch { }

            // ⑧ 同步 profile package.json 里记录的版本
            SyncProfilePackageVersion(profileDir, pkg, latest);

            progress.Phase = "完成";
            progress.Received = progress.Total > 0 ? progress.Total : progress.Received;
            progress.BytesPerSec = 0;
            Log("插件更新：" + pkg + " → " + latest + " 完成（备份在 " + backupDir + "）");
            return null;
        }
        catch (Exception ex)
        {
            Log("插件更新异常 " + pkg + ": " + ex.Message);
            return ex.Message;
        }
        finally
        {
            try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch { }
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
        }
    }

    /// <summary>把 profile 的 package.json 里该插件的版本号同步为 latest（保留原格式）。</summary>
    private static void SyncProfilePackageVersion(string profileDir, string pkg, string latest)
    {
        try
        {
            string pkgPath = Path.Combine(profileDir, "package.json");
            if (!File.Exists(pkgPath)) return;
            string text = File.ReadAllText(pkgPath);
            string pattern = "(\"" + Regex.Escape(pkg) + "\"\\s*:\\s*\")([^\"]*)(\")";
            // 注意：绝不能用 "$1" + latest + "$3" —— latest 以数字开头时，$1 会与后面的
            // 数字拼成 $11（不存在的组）而把内容写坏（实测把 package.json 变成 {$11.3.0"}）。
            // 用 MatchEvaluator 明确拼接，天然没有这个歧义。
            string replaced = Regex.Replace(text, pattern,
                delegate (Match m) { return m.Groups[1].Value + latest + m.Groups[3].Value; });
            if (replaced != text)
            {
                File.WriteAllText(pkgPath, replaced, new UTF8Encoding(false));
                Log("已同步 " + Path.GetFileName(profileDir) + "/package.json 中 " + pkg + " 的版本号为 " + latest);
            }
        }
        catch (Exception ex)
        {
            Log("同步 package.json 版本失败: " + ex.Message);
        }
    }

    // ---------- dsh 本体：带进度观测的安装 ----------

    /// <summary>
    /// 安装指定 dsh 版本到 npx 缓存（只安装、不启动服务），并实时报告“已安装体量 + 速度”。
    /// 做法：比对 `_npx` 下新增的目录并对它采样 —— npm 会把整棵依赖树装在那个新目录里，
    /// 因此它的增长就是真实进度。依赖树总量无法预知，所以这里**不假装有百分比**，
    /// 只如实给“已下载 X / 速度 Y / 已用 Z”。返回 null 成功，否则失败原因。
    /// </summary>
    private static string InstallVersionProgress(string version, ProgressInfo progress, ManualResetEvent cancel)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string npxRoot = Path.Combine(local, "npm-cache", "_npx");
        var before = new HashSet<string>();
        try { foreach (string d in Directory.GetDirectories(npxRoot)) before.Add(d); } catch { }

        var stop = new ManualResetEvent(false);
        var monitor = new Thread(delegate ()
        {
            long lastSize = 0;
            DateTime lastT = DateTime.Now;
            while (!stop.WaitOne(700))
            {
                long total = 0;
                try
                {
                    foreach (string d in Directory.GetDirectories(npxRoot))
                    {
                        if (before.Contains(d)) continue;        // 只看这次新增的目录
                        total += DirectorySize(d, 200000);
                    }
                }
                catch { }
                progress.Received = total;
                DateTime now = DateTime.Now;
                double dt = (now - lastT).TotalSeconds;
                if (dt >= 0.5)
                {
                    double sp = (total - lastSize) / dt;
                    progress.BytesPerSec = sp > 0 ? sp : 0;
                    lastSize = total;
                    lastT = now;
                }
                progress.Elapsed = now - progress.StartedAt;
                if (cancel != null && cancel.WaitOne(0)) break;
            }
        });
        monitor.IsBackground = true;
        monitor.Start();
        try
        {
            progress.Phase = "下载并安装依赖";
            progress.StartedAt = DateTime.Now;
            progress.Total = -1;
            string output = RunNpmCapture(
                "exec --yes --package=@deepseek-ai/dsh@" + version + " -- dsh --version", InstallTimeoutMs);
            bool ok = output != null && output.IndexOf(version, StringComparison.OrdinalIgnoreCase) >= 0;
            progress.BytesPerSec = 0;
            if (!ok && cancel != null && cancel.WaitOne(0)) return "已取消";
            return ok ? null : "安装命令失败或超时（详见日志）";
        }
        finally
        {
            stop.Set();
        }
    }

    // ---------- 安全模式（插件把自己修坏时的“破窗”通道）----------

    /// <summary>
    /// 安全模式：把所有第三方插件暂时摘除（复用已验证的 DisablePluginEverywhere，
    /// 改前整份备份），让服务一定起得来；被摘除的清单写进状态目录，便于一键还原。
    /// 返回被禁用的插件名列表。
    /// </summary>
    private static List<string> EnterSafeMode()
    {
        var disabled = new List<string>();
        try
        {
            // 先对本机每个 profile 做一次显式备份，并**精确记录路径** —— 这样
            // “退出安全模式”能明确从哪一份还原，不靠“猜最新备份目录”。
            var backupMap = new List<string>();
            foreach (string dir in ListProfileDirs())
            {
                string bk = BackupProfileFiles(dir);
                backupMap.Add(Path.GetFileName(dir) + "=" + bk);
            }

            var plugins = new List<string>();
            foreach (string dir in ListProfileDirs())
            {
                string text;
                try { text = File.ReadAllText(Path.Combine(dir, "package.json")); }
                catch { continue; }
                var m = Regex.Match(text, @"""bundles""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                if (!m.Success) continue;
                foreach (Match e in Regex.Matches(m.Groups[1].Value, @"""([^""]+)"""))
                {
                    string b = e.Groups[1].Value;
                    if (b.StartsWith("@deepseek-ai/", StringComparison.Ordinal)) continue;
                    if (!plugins.Contains(b)) plugins.Add(b);
                }
            }
            foreach (string p in plugins)
            {
                DisablePluginEverywhere(p, null);
                disabled.Add(p);
            }
            try
            {
                EnsureAppDataDir();
                var lines = new List<string>();
                lines.Add("# 安全模式记录：第一段是已摘除的第三方插件，第二段是各 profile 的还原来源备份");
                lines.Add("# 如需还原请用托盘菜单“退出安全模式（还原被摘除的插件）”");
                lines.Add("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.Add("disabled=" + string.Join(",", disabled.ToArray()));
                foreach (string b in backupMap) lines.Add("backup:" + b);
                File.WriteAllLines(Path.Combine(AppDataDir, "safe-mode.txt"), lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
            Log("安全模式：已摘除第三方插件 " + disabled.Count + " 个，已记录还原来源 " + backupMap.Count + " 条");
        }
        catch (Exception ex)
        {
            Log("进入安全模式失败: " + ex.Message);
        }
        return disabled;
    }

    /// <summary>
    /// 退出安全模式：按记录把各 profile 的配置文件从备份还原回去，并删除标记。
    /// 这是“插件/版本被改坏后怎么救回来”的正式通道，改动可逆。
    /// </summary>
    private static string ExitSafeMode()
    {
        var report = new StringBuilder();
        var map = new List<string>();
        try
        {
            string marker = Path.Combine(AppDataDir, "safe-mode.txt");
            if (File.Exists(marker))
            {
                foreach (string line in File.ReadAllLines(marker))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (t.StartsWith("time=", StringComparison.Ordinal) || t.StartsWith("disabled=", StringComparison.Ordinal)) continue;
                    if (t.StartsWith("backup:", StringComparison.Ordinal)) map.Add(t.Substring(7));
                }
            }
            foreach (string entry in map)
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                string prof = entry.Substring(0, eq);
                string bk = entry.Substring(eq + 1);
                string dir = Path.Combine(DshProfilesRoot(), prof);
                if (!Directory.Exists(dir)) { report.AppendLine("· " + prof + "：profile 目录不存在，跳过"); continue; }
                int n = 0;
                foreach (string name in new string[] { "package.json", "pnpm-workspace.yaml", "cordis.patch.yml", "cordis.yml" })
                {
                    string src = Path.Combine(bk, name);
                    if (File.Exists(src)) { File.Copy(src, Path.Combine(dir, name), true); n++; }
                }
                report.AppendLine("· " + prof + "：已从 " + bk + " 还原 " + n + " 个配置文件");
            }
            if (map.Count == 0) report.AppendLine("（没有找到安全模式的还原记录）");
            try { File.Delete(Path.Combine(AppDataDir, "safe-mode.txt")); } catch { }
            Log("退出安全模式：" + OneLine(report.ToString(), 400));
        }
        catch (Exception ex)
        {
            report.AppendLine("还原失败：" + ex.Message);
            Log("退出安全模式失败: " + ex.Message);
        }
        return report.ToString();
    }

    private static void ExitSafeModeUi()
    {
        if (!IsSafeMode())
        {
            Msg("当前不在安全模式。", MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(
                "将把各 profile 的配置文件还原到“进入安全模式前”的状态，" +
                "第三方插件会恢复。\n\n确定退出安全模式吗？",
                AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1) != DialogResult.Yes) return;
        string rep = ExitSafeMode();
        Msg("已退出安全模式。\n\n" + rep +
            "\n请用托盘 →“停止服务并退出”，再重新双击启动器，让插件重新加载。",
            MessageBoxIcon.Information);
    }

    /// <summary>是否处于安全模式（存在标记文件）。</summary>
    private static bool IsSafeMode()
    {
        try { return File.Exists(Path.Combine(AppDataDir, "safe-mode.txt")); }
        catch { return false; }
    }

    /// <summary>
    /// 备份保留策略：**只清理插件更新自动产生的 .backup-update-* 目录**（每个 profile 留最近 keep 份），
    /// 绝不碰其它 .backup-* —— 那些是排障/回滚用的关键点，误删不可恢复。
    /// </summary>
    private static void PruneBackups(int keep)
    {
        try
        {
            foreach (string dir in ListProfileDirs())
            {
                var updates = new List<string>();
                try
                {
                    foreach (string d in Directory.GetDirectories(dir))
                    {
                        string n = Path.GetFileName(d);
                        if (n.StartsWith(".backup-update-", StringComparison.OrdinalIgnoreCase)) updates.Add(d);
                    }
                }
                catch { continue; }
                if (updates.Count <= keep) continue;
                updates.Sort(StringComparer.OrdinalIgnoreCase);
                int remove = updates.Count - keep;
                for (int i = 0; i < remove; i++)
                {
                    try { Directory.Delete(updates[i], true); Log("清理旧插件更新备份 " + updates[i]); }
                    catch (Exception ex) { Log("清理旧插件更新备份失败 " + updates[i] + ": " + ex.Message); }
                }
            }
        }
        catch (Exception ex) { Log("备份清理异常: " + ex.Message); }
    }

    // ============================================================================
    // v0.7.0：主窗口（控制面板）
    // ----------------------------------------------------------------------------
    // 旧版没有窗口：状态在托盘 tooltip、升级在 MessageBox、插件检查在 MessageBox、
    // 日志在文件里 —— 信息四处分散，用户得自己拼。新版把这一切收进一个窗口，
    // 分 5 个页签：概览 / 更新（带进度条）/ 插件 / 日志 / 关于。
    // 默认**隐藏**：仅当双击托盘、或点托盘“打开控制面板”时出现，不改变原有使用习惯。
    // 全部使用系统标准控件与系统字体，不画自绘皮肤（UI 风格保持系统风格）。
    // ============================================================================

    private static MainForm mainForm;                                   // 惰性创建，关闭只是隐藏
    private static readonly ManualResetEvent updateCancel = new ManualResetEvent(false);

    /// <summary>显示（必要时创建）控制面板。可从任意线程调用。</summary>
    private static void ShowMainWindow()
    {
        Ui(delegate
        {
            try
            {
                if (mainForm == null || mainForm.IsDisposed) mainForm = new MainForm();
                if (!mainForm.Visible) mainForm.Show();
                if (mainForm.WindowState == FormWindowState.Minimized)
                    mainForm.WindowState = FormWindowState.Normal;
                mainForm.Activate();
                mainForm.BringToFront();
                mainForm.RefreshStatus();
            }
            catch (Exception ex) { Log("打开控制面板失败: " + ex.Message); }
        });
    }

    /// <summary>切到“更新”页并立即检查（托盘“检查更新…”入口）。</summary>
    private static void ShowMainWindowUpdates()
    {
        ShowMainWindow();
        Ui(delegate
        {
            try
            {
                if (mainForm == null) return;
                mainForm.SelectUpdatesTab();
                mainForm.RefreshUpdates();
            }
            catch (Exception ex) { Log("打开更新页失败: " + ex.Message); }
        });
    }

    /// <summary>切到“插件”页并立即扫描（托盘“插件兼容检查…”入口）。</summary>
    private static void ShowMainWindowPlugins()
    {
        ShowMainWindow();
        Ui(delegate
        {
            try
            {
                if (mainForm == null) return;
                mainForm.SelectPluginsTab();
                mainForm.RefreshPlugins();
            }
            catch (Exception ex) { Log("打开插件页失败: " + ex.Message); }
        });
    }

    /// <summary>主窗口：状态条 + 主操作 + 5 个页签。系统风格，无自绘。</summary>
    private sealed class MainForm : Form
    {
        // 顶部状态条
        private Label dot, state, ver, pid;
        private Button actOpen, actCheck, actStop;
        private TabControl tabs;

        // 概览
        private Label ovState, ovVersion, ovPid, ovStart, ovUptime, ovAddr;
        private TextBox ovUrlBox;
        private Button ovCopy, ovReopen;

        // 更新
        private FlowLayoutPanel updList;
        private Label updHint;
        private Button updRefresh, updSelectAll, updSelectNone, updApply, updCancelBtn;
        private readonly List<UpdateRow> updRows = new List<UpdateRow>();
        private volatile bool updBusy;

        // 插件
        private ListView plugList;
        private Button plugRefresh, plugDisable;

        // 日志
        private TextBox logBox;
        private Button logRefresh, logOpen;

        // 关于
        private Label aboutLabel;

        private System.Windows.Forms.Timer fastTimer, slowTimer;

        public MainForm()
        {
            // ---- 窗体本身：系统风格 ----
            Text = AppTitle + " — 控制面板";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 560);
            MinimumSize = new Size(600, 440);
            ShowInTaskbar = true;
            Icon = CreateTrayIcon();

            // ---- 根布局：状态条（固定高）+ 页签（填满）----
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(14, 5);
            tabs.TabPages.Add(BuildOverviewTab());
            tabs.TabPages.Add(BuildUpdatesTab());
            tabs.TabPages.Add(BuildPluginsTab());
            tabs.TabPages.Add(BuildLogTab());
            tabs.TabPages.Add(BuildAboutTab());
            root.Controls.Add(tabs, 0, 1);

            // ---- 刷新定时器：进度 200ms / 状态与日志 2s（都不阻塞 UI）----
            fastTimer = new System.Windows.Forms.Timer();
            fastTimer.Interval = 200;
            fastTimer.Tick += delegate { TickProgress(); };
            fastTimer.Start();

            slowTimer = new System.Windows.Forms.Timer();
            slowTimer.Interval = 2000;
            slowTimer.Tick += delegate { RefreshStatus(); RefreshLog(); };
            slowTimer.Start();
        }

        private Panel BuildHeader()
        {
            var header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = SystemColors.Control;

            dot = new Label();
            dot.SetBounds(16, 16, 14, 14);
            dot.Text = "";
            dot.BackColor = SystemColors.GrayText;
            header.Controls.Add(dot);

            state = new Label();
            state.AutoSize = true;
            state.Font = new Font(Font, FontStyle.Bold);
            state.Text = "正在检查服务状态…";
            state.SetBounds(38, 10, 300, 22);
            header.Controls.Add(state);

            ver = new Label();
            ver.AutoSize = true;
            ver.ForeColor = SystemColors.GrayText;
            ver.Text = "";
            ver.SetBounds(40, 34, 320, 18);
            header.Controls.Add(ver);

            pid = new Label();
            pid.AutoSize = true;
            pid.ForeColor = SystemColors.GrayText;
            pid.Text = "";
            pid.SetBounds(40, 54, 400, 18);
            header.Controls.Add(pid);

            // 按钮独占一行靠左：旧写法 Dock=Right 的面板会盖住较宽的版本/进程文字，
            // 而且按钮自动变宽后会顶到窗体边缘被裁掉。
            var actions = new FlowLayoutPanel();
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.Location = new Point(10, 76);
            actions.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            header.Controls.Add(actions);

            actOpen = NewButton("打开网页", 92);
            actOpen.Click += delegate { OpenUrl(); };
            actCheck = NewButton("检查更新", 92);
            actCheck.Click += delegate { SelectUpdatesTab(); RefreshUpdates(); };
            actStop = NewButton("停止服务", 92);
            actStop.Click += delegate
            {
                if (MessageBox.Show(this, "停止服务会断开浏览器里正在使用的页面。\n\n确定要停止并退出启动器吗？",
                        AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)
                    == DialogResult.Yes)
                    ExitWithServiceHandling();
            };
            actions.Controls.Add(actOpen);
            actions.Controls.Add(actCheck);
            actions.Controls.Add(actStop);
            return header;
        }

        /// <summary>
        /// 统一按钮工厂：按文字自动定宽（GrowAndShrink + 最小宽度）。
        /// 这样任何语言/字号下按钮文字都不会被裁掉 —— 旧写法固定像素宽，
        /// 实测在较大系统字体下会把“禁用不兼容插件”裁成“禁用不兼容插”。
        /// </summary>
        private static Button NewButton(string text, int minWidth)
        {
            var b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(minWidth, 28);
            b.Padding = new Padding(8, 0, 8, 0);
            b.Margin = new Padding(6, 0, 0, 0);
            return b;
        }

        // ---------------- 概览 ----------------

        private TabPage BuildOverviewTab()
        {
            var page = new TabPage("概览");
            page.Padding = new Padding(14, 12, 14, 12);

            var t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // 访问地址（输入框 + 两个按钮）
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));   // 状态提示
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 底部说明（吃掉剩余高度并可换行）
            t.RowCount = 8;
            page.Controls.Add(t);

            ovState = AddRow(t, 0, "服务状态");
            ovVersion = AddRow(t, 1, "使用版本");
            ovPid = AddRow(t, 2, "服务进程");
            ovStart = AddRow(t, 3, "启动时间");
            ovUptime = AddRow(t, 4, "运行时长");

            var cap = new Label();
            cap.Text = "访问地址";
            cap.AutoSize = true;
            cap.ForeColor = SystemColors.GrayText;
            cap.Anchor = AnchorStyles.Left;
            cap.Margin = new Padding(0, 6, 8, 0);
            t.Controls.Add(cap, 0, 5);

            // 固定列宽交给 TableLayoutPanel 排版：旧写法靠 Resize 手算坐标，
            // 结果“重新打开”按钮被父容器裁掉一半（实测）。
            var urlRow = new TableLayoutPanel();
            urlRow.Dock = DockStyle.Fill;
            urlRow.ColumnCount = 2;
            urlRow.RowCount = 1;
            urlRow.Margin = new Padding(0);
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            urlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            ovUrlBox = new TextBox();
            ovUrlBox.ReadOnly = true;
            ovUrlBox.Dock = DockStyle.Fill;
            ovUrlBox.Margin = new Padding(0, 6, 6, 6);
            urlRow.Controls.Add(ovUrlBox, 0, 0);

            // 两颗按钮放进自适应流式面板：宽度由文字决定，任何字体下都不会被挤掉半个字
            var urlBtns = new FlowLayoutPanel();
            urlBtns.FlowDirection = FlowDirection.LeftToRight;
            urlBtns.WrapContents = false;
            urlBtns.AutoSize = true;
            urlBtns.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            urlBtns.Margin = new Padding(0);
            urlBtns.Padding = new Padding(0, 4, 0, 4);

            ovCopy = NewButton("复制", 60);
            ovCopy.Click += delegate
            {
                try
                {
                    if (!string.IsNullOrEmpty(ovUrlBox.Text)) Clipboard.SetText(ovUrlBox.Text);
                    SetHint(ovAddr, "已复制到剪贴板。");
                }
                catch (Exception ex) { SetHint(ovAddr, "复制失败：" + ex.Message); }
            };
            urlBtns.Controls.Add(ovCopy);

            ovReopen = NewButton("重新打开", 90);
            ovReopen.Click += delegate { OpenUrl(); };
            urlBtns.Controls.Add(ovReopen);
            urlRow.Controls.Add(urlBtns, 1, 0);
            t.Controls.Add(urlRow, 1, 5);

            ovAddr = new Label();
            ovAddr.AutoSize = false;
            ovAddr.Dock = DockStyle.Fill;
            ovAddr.ForeColor = SystemColors.GrayText;
            ovAddr.TextAlign = ContentAlignment.MiddleLeft;
            ovAddr.Margin = new Padding(0);
            ovAddr.Text = "—";
            t.Controls.Add(ovAddr, 1, 6);

            var note = new Label();
            note.Dock = DockStyle.Fill;
            note.AutoSize = false;
            note.ForeColor = SystemColors.GrayText;
            note.Padding = new Padding(0, 10, 0, 0);
            note.Text = "提示：访问地址带一次性 token，只在本机 127.0.0.1 生效；粘贴给别人前请先去掉 ?token=… 部分。\r\n"
                + "若页面里的按钮点了没反应，多半是浏览器里还开着旧版页面：关掉旧标签页重开即可（或按 Ctrl+F5）。";
            t.Controls.Add(note, 0, 7);
            t.SetColumnSpan(note, 2);
            return page;
        }

        private static Label AddRow(TableLayoutPanel t, int row, string caption)
        {
            var c = new Label();
            c.Text = caption;
            c.AutoSize = true;
            c.ForeColor = SystemColors.GrayText;
            c.Anchor = AnchorStyles.Left;
            c.Margin = new Padding(0, 6, 8, 0);
            t.Controls.Add(c, 0, row);

            var v = new Label();
            v.Text = "—";
            v.AutoSize = true;
            v.Anchor = AnchorStyles.Left;
            v.Margin = new Padding(0, 6, 0, 0);
            t.Controls.Add(v, 1, row);
            return v;
        }

        private static void SetHint(Label target, string text)
        {
            target.Text = text;
        }

        // ---------------- 更新 ----------------

        private TabPage BuildUpdatesTab()
        {
            var page = new TabPage("更新");
            page.Padding = new Padding(10, 10, 10, 10);

            var t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 3;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            page.Controls.Add(t);

            updHint = new Label();
            updHint.Dock = DockStyle.Fill;
            updHint.Text = "点“检查更新”查询 dsh 本体与第三方插件的新版本。";
            t.Controls.Add(updHint, 0, 0);

            updList = new FlowLayoutPanel();
            updList.Dock = DockStyle.Fill;
            updList.FlowDirection = FlowDirection.TopDown;
            updList.WrapContents = false;
            updList.AutoScroll = true;
            updList.BackColor = SystemColors.Window;
            updList.BorderStyle = BorderStyle.FixedSingle;
            updList.SizeChanged += delegate { FitUpdateRows(); };
            t.Controls.Add(updList, 0, 1);

            var bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            t.Controls.Add(bar, 0, 2);

            updRefresh = NewButton("检查更新", 92);
            updRefresh.Click += delegate { RefreshUpdates(); };
            updSelectAll = NewButton("全选", 60);
            updSelectAll.Click += delegate { SetAllChecks(true); };
            updSelectNone = NewButton("全不选", 68);
            updSelectNone.Click += delegate { SetAllChecks(false); };
            updApply = NewButton("开始更新选中项", 128);
            updApply.Click += delegate { StartUpdate(); };
            updCancelBtn = NewButton("取消", 60);
            updCancelBtn.Enabled = false;
            updCancelBtn.Click += delegate
            {
                updateCancel.Set();
                updHint.Text = "已请求取消，正在收尾…";
            };
            bar.Controls.Add(updRefresh);
            bar.Controls.Add(updSelectAll);
            bar.Controls.Add(updSelectNone);
            bar.Controls.Add(updApply);
            bar.Controls.Add(updCancelBtn);
            return page;
        }

        public void SelectUpdatesTab()
        {
            try { if (tabs.TabPages.Count > 1) tabs.SelectedIndex = 1; } catch { }
        }

        public void SelectPluginsTab()
        {
            try { if (tabs.TabPages.Count > 2) tabs.SelectedIndex = 2; } catch { }
        }

        private void SetAllChecks(bool value)
        {
            foreach (UpdateRow r in updRows) r.Check.Checked = value;
        }

        public void RefreshUpdates()
        {
            if (updBusy) return;
            updHint.Text = "正在查询 npm（dsh 本体 + 各 profile 第三方插件）…";
            updRefresh.Enabled = false;
            var t = new Thread(delegate ()
            {
                List<UpdateCandidate> items = null;
                try { items = CollectUpdateCandidates(); }
                catch (Exception ex) { Log("检查更新异常: " + ex.Message); }
                List<UpdateCandidate> copy = items;
                Ui(delegate { PopulateUpdates(copy); });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void PopulateUpdates(List<UpdateCandidate> items)
        {
            updRefresh.Enabled = true;
            foreach (UpdateRow r in updRows) r.Dispose();
            updRows.Clear();
            updList.Controls.Clear();

            if (items == null)
            {
                updHint.Text = "检查失败：无法访问 npm 仓库（可能断网）。可稍后重试。";
                return;
            }
            if (items.Count == 0)
            {
                string extra = "";
                if (skippedPluginUpdates != null && skippedPluginUpdates.Count > 0)
                    extra = "（另有 " + skippedPluginUpdates.Count + " 个插件新版本与当前 dsh 不兼容，已自动跳过）";
                updHint.Text = "已是最新：dsh 本体与第三方插件都没有可用新版本。" + extra;
                return;
            }
            foreach (UpdateCandidate it in items)
            {
                var row = new UpdateRow(it);
                updRows.Add(row);
                updList.Controls.Add(row);
            }
            FitUpdateRows();
            updHint.Text = "发现 " + items.Count + " 项可更新。勾选后点“开始更新选中项”。";
        }

        /// <summary>让每一行铺满列表面板宽度（否则明细里的“速度/剩余时间”会被右侧裁掉）。</summary>
        private void FitUpdateRows()
        {
            try
            {
                int w = updList.ClientSize.Width - 28;
                if (w < 220) w = 220;
                foreach (UpdateRow r in updRows) r.Width = w;
            }
            catch { }
        }

        private UpdateRow RowFor(UpdateCandidate item)
        {
            foreach (UpdateRow r in updRows) if (object.ReferenceEquals(r.Item, item)) return r;
            return null;
        }

        private void StartUpdate()
        {
            if (updBusy) return;
            var chosen = new List<UpdateCandidate>();
            foreach (UpdateRow r in updRows) if (r.Check.Checked) chosen.Add(r.Item);
            if (chosen.Count == 0)
            {
                MessageBox.Show(this, "请至少勾选一项要更新的内容。", AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            updateCancel.Reset();
            updBusy = true;
            updApply.Enabled = false;
            updRefresh.Enabled = false;
            updSelectAll.Enabled = false;
            updSelectNone.Enabled = false;
            updCancelBtn.Enabled = true;
            updHint.Text = "正在更新…可以点“取消”中断；已备份的内容可回滚。";
            var t = new Thread(delegate () { RunUpdateWorker(chosen); });
            t.IsBackground = true;
            t.Start();
        }

        private void RunUpdateWorker(List<UpdateCandidate> chosen)
        {
            int okCount = 0, failCount = 0;
            var summary = new StringBuilder();
            foreach (UpdateCandidate item in chosen)
            {
                UpdateRow row = RowFor(item);
                var prog = new ProgressInfo();
                if (row != null) { row.Info = prog; Ui(delegate { row.ShowRunning(); }); }

                string err;
                if (item.Kind == "dsh")
                {
                    try { WriteVersionState(pinnedVersion, null, DateTime.Now, item.Latest); } catch { }
                    err = InstallVersionProgress(item.Latest, prog, updateCancel);
                    if (err == null)
                    {
                        pinnedVersion = item.Latest;
                        try { WriteVersionState(item.Latest, null, DateTime.Now); } catch { }
                        try { SetTrayText("DeepSeek Harness 服务运行中（新版本 " + item.Latest + " 已装好）"); } catch { }
                        summary.AppendLine("· dsh 本体已装好：" + item.Latest + "（重启启动器后生效）");
                    }
                    else
                    {
                        try { WriteVersionState(pinnedVersion, item.Latest, DateTime.Now); } catch { }
                        summary.AppendLine("· dsh 本体更新失败：" + err);
                    }
                }
                else
                {
                    err = UpdatePluginAtomic(item.ProfileDir, item.Package, item.Latest, prog, updateCancel);
                    if (err == null) summary.AppendLine("· 插件已更新：" + item.Label + " → " + item.Latest);
                    else summary.AppendLine("· 插件更新失败：" + item.Label + "：" + err);
                }

                if (err == null) okCount++; else failCount++;
                prog.Finished = true;
                prog.Failed = err != null;
                if (row != null) { string e = err; Ui(delegate { row.ShowDone(e); }); }

                if (updateCancel.WaitOne(0))
                {
                    summary.AppendLine("（已按请求取消，剩余项未执行）");
                    break;
                }
            }

            try { SetTrayText("DeepSeek Harness 服务运行中（右键可停止）"); } catch { }
            string text = summary.ToString().TrimEnd();
            int ok = okCount, fail = failCount;
            Ui(delegate
            {
                updBusy = false;
                updApply.Enabled = true;
                updRefresh.Enabled = true;
                updSelectAll.Enabled = true;
                updSelectNone.Enabled = true;
                updCancelBtn.Enabled = false;
                updHint.Text = "本次更新结束：成功 " + ok + " 项，失败 " + fail + " 项。";
                if (text.Length > 0)
                    MessageBox.Show(this, text + (ok > 0 ? "\n\n插件更新即刻生效；dsh 本体更新需重启启动器。" : ""),
                        AppTitle, MessageBoxButtons.OK,
                        fail > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                RefreshStatus();
            });
        }

        /// <summary>进度定时器：把后台线程写进 ProgressInfo 的数据刷到对应进度条。</summary>
        private void TickProgress()
        {
            if (!updBusy) return;
            foreach (UpdateRow r in updRows)
            {
                if (r.Info != null && r.Running) r.ShowProgress(r.Info);
            }
        }

        // ---------------- 插件 ----------------

        private TabPage BuildPluginsTab()
        {
            var page = new TabPage("插件");
            page.Padding = new Padding(10, 10, 10, 10);

            var t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            page.Controls.Add(t);

            plugList = new ListView();
            plugList.Dock = DockStyle.Fill;
            plugList.View = View.Details;
            plugList.FullRowSelect = true;
            plugList.GridLines = true;
            plugList.ShowItemToolTips = true;
            plugList.Columns.Add("插件", 240);
            plugList.Columns.Add("配置", 70);
            plugList.Columns.Add("判定", 150);
            plugList.Columns.Add("说明", 300);
            plugList.SizeChanged += delegate { FitPluginColumns(); };
            t.Controls.Add(plugList, 0, 0);

            var bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            t.Controls.Add(bar, 0, 1);

            plugRefresh = NewButton("重新检查", 92);
            plugRefresh.Click += delegate { RefreshPlugins(); };
            plugDisable = NewButton("禁用不兼容插件", 128);
            plugDisable.Click += delegate { DisableBadPlugins(); };
            // 按钮文字保持短，详细说明放在确认对话框里（长文字会被按钮边界裁掉）
            var safe = NewButton("进入安全模式", 120);
            safe.Click += delegate { EnterSafeModeUi(); };
            bar.Controls.Add(plugRefresh);
            bar.Controls.Add(plugDisable);
            bar.Controls.Add(safe);
            return page;
        }

        /// <summary>插件列表按窗口宽度分配列宽，避免“判定/说明”被右边裁掉。</summary>
        private void FitPluginColumns()
        {
            try
            {
                int w = plugList.ClientSize.Width;
                if (w < 300 || plugList.Columns.Count < 4) return;
                plugList.Columns[0].Width = (int)(w * 0.24);
                plugList.Columns[1].Width = (int)(w * 0.09);
                plugList.Columns[2].Width = (int)(w * 0.24);
                plugList.Columns[3].Width = (int)(w * 0.43);
            }
            catch { }
        }

        public void RefreshPlugins()
        {
            plugRefresh.Enabled = false;
            var t = new Thread(delegate ()
            {
                List<PluginCompat> all = null;
                try { all = ScanProfilePlugins(); }
                catch (Exception ex) { Log("插件扫描异常: " + ex.Message); }
                List<PluginCompat> copy = all;
                Ui(delegate { PopulatePlugins(copy); });
            });
            t.IsBackground = true;
            t.Start();
        }

        private void PopulatePlugins(List<PluginCompat> all)
        {
            plugRefresh.Enabled = true;
            plugList.Items.Clear();
            if (all == null || all.Count == 0)
            {
                var empty = new ListViewItem("（没有检测到第三方插件）");
                plugList.Items.Add(empty);
                return;
            }
            foreach (PluginCompat p in all)
            {
                var it = new ListViewItem(p.Name);
                it.SubItems.Add(p.Profile);
                it.SubItems.Add(p.VerdictShort());
                it.SubItems.Add(p.Supported == "-" ? "" : ("声明支持：" + p.Supported));
                it.ToolTipText = p.Name + "（" + p.Profile + "）：" + p.VerdictText()
                    + (p.Supported != "-" ? "\n声明支持：" + p.Supported : "")
                    + "\n实际解析到：" + p.HarnessSummary();
                if (p.Verdict == "fatal") it.ForeColor = Color.Firebrick;
                else if (p.Verdict == "risky") it.ForeColor = Color.DarkOrange;
                plugList.Items.Add(it);
            }
            FitPluginColumns();
        }

        private void DisableBadPlugins()
        {
            List<PluginCompat> all;
            try { all = ScanProfilePlugins(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "插件检查失败：" + ex.Message, AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var bad = new List<PluginCompat>();
            foreach (PluginCompat p in all) if (p.Fatal) bad.Add(p);
            if (bad.Count == 0)
            {
                MessageBox.Show(this, "没有发现会在加载期抛错的插件，无需禁用。", AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var names = new StringBuilder();
            foreach (PluginCompat p in bad) names.AppendLine("· " + p.Name + "（" + p.Profile + "）");
            if (MessageBox.Show(this, "以下插件会让服务起不来，是否禁用（改前会备份）？\n\n" + names,
                    AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var rep = new StringBuilder();
            foreach (PluginCompat p in bad) rep.AppendLine(DisablePluginEverywhere(p.Name, null));
            Log("控制面板：已禁用不兼容插件" + Environment.NewLine + rep.ToString());
            MessageBox.Show(this, "已禁用 " + bad.Count + " 个插件。\n\n" + rep,
                AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshPlugins();
        }

        private void EnterSafeModeUi()
        {
            if (MessageBox.Show(this,
                    "安全模式会暂时摘除**全部第三方插件**（每个 profile 都会先整份备份），\n" +
                    "让服务一定能启动。之后可在托盘菜单里还原。\n\n确定进入安全模式吗？",
                    AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            List<string> disabled = EnterSafeMode();
            MessageBox.Show(this, "已摘除 " + disabled.Count + " 个第三方插件。\n\n" +
                "请用托盘 →“停止服务并退出”，再重新双击启动器；服务将以“无第三方插件”方式启动。",
                AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshPlugins();
        }

        // ---------------- 日志 ----------------

        private TabPage BuildLogTab()
        {
            var page = new TabPage("日志");
            page.Padding = new Padding(10, 10, 10, 10);

            var t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            page.Controls.Add(t);

            logBox = new TextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.WordWrap = false;
            logBox.BackColor = SystemColors.Window;
            logBox.Font = new Font("Consolas", 9f);
            t.Controls.Add(logBox, 0, 0);

            var bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            t.Controls.Add(bar, 0, 1);

            logRefresh = NewButton("刷新", 60);
            logRefresh.Click += delegate { RefreshLog(); };
            logOpen = NewButton("打开日志文件", 100);
            logOpen.Click += delegate
            {
                try { Process.Start(new ProcessStartInfo(LogFile) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "打开失败：" + ex.Message, AppTitle); }
            };
            bar.Controls.Add(logRefresh);
            bar.Controls.Add(logOpen);
            return page;
        }

        private void RefreshLog()
        {
            try
            {
                if (!File.Exists(LogFile)) return;
                string[] lines = File.ReadAllLines(LogFile);
                int take = Math.Min(400, lines.Length);
                var sb = new StringBuilder();
                for (int i = lines.Length - take; i < lines.Length; i++) sb.AppendLine(lines[i]);
                string text = sb.ToString();
                if (text != logBox.Text)
                {
                    logBox.Text = text;
                    logBox.SelectionStart = logBox.TextLength;
                    logBox.ScrollToCaret();
                }
            }
            catch { }
        }

        // ---------------- 关于 ----------------

        private TabPage BuildAboutTab()
        {
            var page = new TabPage("关于");
            page.Padding = new Padding(16, 14, 16, 14);
            aboutLabel = new Label();
            aboutLabel.Dock = DockStyle.Fill;
            aboutLabel.Text =
                AppTitle + "  v" + LauncherVersion + "\r\n\r\n" +
                "作用：直接启动本机已装的 DeepSeek Harness（dsh），自动打开网页，" +
                "并在浏览器全部关闭或托盘操作时停止服务。\r\n\r\n" +
                "· 状态与日志目录：" + AppDataDir + "\r\n" +
                "· dsh profiles 目录：" + DshProfilesRoot() + "\r\n" +
                "· npm 缓存目录：" + Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache") + "\r\n\r\n" +
                "更新机制：dsh 本体走 npm 安装（失败自动回退到原版本）；" +
                "第三方插件为文件级安装（下载 → SHA1 校验 → 原子替换，改前整目录备份）。\r\n" +
                "安全兜底：任何 profile 改动前都会备份到 .backup-<时间戳>；" +
                "“安全模式”可一键摘除全部第三方插件以恢复启动。\r\n\r\n" +
                "本程序只监听 127.0.0.1，不对外提供服务；带 token 的访问地址不会离开本机。";
            page.Controls.Add(aboutLabel);
            return page;
        }

        // ---------------- 状态刷新 ----------------

        public void RefreshStatus()
        {
            try
            {
                int svcPid = NetstatListenPid(Port);
                bool running = svcPid > 0;
                bool own = serverProcess != null && !serverProcess.HasExited;

                dot.BackColor = running ? Color.SeaGreen : SystemColors.GrayText;
                if (running)
                {
                    state.Text = "服务运行中";
                    state.ForeColor = SystemColors.ControlText;
                }
                else
                {
                    state.Text = "服务已停止";
                    state.ForeColor = SystemColors.GrayText;
                }

                if (IsSafeMode()) state.Text += "（安全模式）";

                string v = string.IsNullOrEmpty(pinnedVersion) ? DetectInstalledVersion() : pinnedVersion;
                ver.Text = "使用版本：" + (string.IsNullOrEmpty(v) ? "未记录" : v)
                    + "　·　本启动器 v" + LauncherVersion;

                if (running)
                {
                    pid.Text = "服务进程：PID " + svcPid + (own ? "（本程序启动）" : "（外部/上次遗留）");
                }
                else
                {
                    pid.Text = "服务进程：无";
                }

                ovState.Text = running ? (own ? "运行中（本程序启动）" : "运行中（外部进程）") : "已停止";
                ovVersion.Text = string.IsNullOrEmpty(v) ? "未记录" : v;

                DateTime? start = running ? SafeStartTime(svcPid) : null;
                ovPid.Text = running ? ("PID " + svcPid) : "—";
                ovStart.Text = start.HasValue ? start.Value.ToString("yyyy-MM-dd HH:mm:ss") : "—";
                if (start.HasValue)
                {
                    TimeSpan up = DateTime.Now - start.Value;
                    ovUptime.Text = FormatDuration(up);
                }
                else ovUptime.Text = "—";

                string url = CurrentUrl();
                if (ovUrlBox.Text != url) ovUrlBox.Text = url;
                ovAddr.Text = running ? "已就绪（点“复制”可复制带 token 地址）" : "服务未运行";

                actOpen.Enabled = running;
                actStop.Enabled = true;
            }
            catch (Exception ex)
            {
                Log("刷新状态失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 构造冒烟测试（自检用）：创建句柄、跑一遍布局与状态刷新，回读关键控件是否建好。
        /// 只在 DSH_LAUNCHER_UI_TEST=1 时被调用，不会真的把窗口显示出来。
        /// </summary>
        public string SmokeTest()
        {
            CreateControl();
            PerformLayout();
            RefreshStatus();
            int tabCount = tabs == null ? -1 : tabs.TabPages.Count;
            var names = new StringBuilder();
            if (tabs != null)
            {
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    if (i > 0) names.Append("|");
                    names.Append(tabs.TabPages[i].Text);
                }
            }
            bool keys = actOpen != null && actCheck != null && actStop != null
                && updList != null && plugList != null && logBox != null && ovUrlBox != null
                && tabs != null;
            return "ui_form_created=True"
                + "\r\nui_tabs=" + tabCount
                + "\r\nui_tab_names=" + names
                + "\r\nui_key_controls=" + keys
                + "\r\nui_has_tray_icon=" + (Icon != null)
                + "\r\nui_refresh_status_ok=True";
        }

        /// <summary>
        /// 版式验收（自检用）：在屏幕外显示窗口，逐页签渲染成 PNG，便于核对界面。
        /// 只在 DSH_LAUNCHER_UI_TEST=1 时调用；会临时填几行演示数据以便看出进度条效果。
        /// </summary>
        public string RenderShots(string dir)
        {
            var report = new StringBuilder();
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-4000, -4000);
            Show();
            Application.DoEvents();
            try
            {
                for (int i = 0; i < tabs.TabPages.Count; i++)
                {
                    tabs.SelectedIndex = i;
                    Application.DoEvents();

                    // 给“更新/插件/日志”页填演示数据，好让版式看得出真实效果
                    if (i == 1 && updRows.Count == 0)
                    {
                        var a = new UpdateCandidate { Kind = "dsh", Label = "DeepSeek Harness（本体）", Current = "0.1.5-rc.3", Latest = "0.2.0-rc.2" };
                        var b = new UpdateCandidate { Kind = "plugin", Label = "@nanmicoder/dsh-agent-teams（web）", Current = "0.1.21", Latest = "0.1.22" };
                        var ra = new UpdateRow(a);
                        var rb = new UpdateRow(b);
                        updList.Controls.Add(ra);
                        updList.Controls.Add(rb);
                        updRows.Add(ra);
                        updRows.Add(rb);
                        FitUpdateRows();
                        updHint.Text = "发现 2 项可更新。勾选后点“开始更新选中项”。";
                        ra.ShowRunning();
                        var p1 = new ProgressInfo();
                        p1.Phase = "下载并安装依赖";
                        p1.Received = 18L * 1024 * 1024;
                        p1.Total = -1;
                        p1.BytesPerSec = 1.6 * 1024 * 1024;
                        p1.Elapsed = TimeSpan.FromSeconds(47);
                        ra.ShowProgress(p1);
                        rb.ShowRunning();
                        var p2 = new ProgressInfo();
                        p2.Phase = "下载中";
                        p2.Received = 900 * 1024;
                        p2.Total = 2048 * 1024;
                        p2.BytesPerSec = 288 * 1024;
                        p2.Elapsed = TimeSpan.FromSeconds(3);
                        rb.ShowProgress(p2);
                        Application.DoEvents();
                    }
                    if (i == 2 && plugList.Items.Count == 0)
                    {
                        var it = new ListViewItem("@nanmicoder/dsh-agent-teams");
                        it.SubItems.Add("web");
                        it.SubItems.Add("版本混杂（能跑）");
                        it.SubItems.Add("声明支持：0.1.7-rc.2,0.1.5-rc.3");
                        it.ToolTipText = "@nanmicoder/dsh-agent-teams（web）：版本混杂，但该插件不做加载期校验（能跑，行为可能异常）";
                        it.ForeColor = Color.DarkOrange;
                        plugList.Items.Add(it);
                        var it2 = new ListViewItem("dsh-perm-gate");
                        it2.SubItems.Add("web");
                        it2.SubItems.Add("兼容");
                        it2.SubItems.Add("");
                        plugList.Items.Add(it2);
                        FitPluginColumns();
                        Application.DoEvents();
                    }
                    if (i == 3) { logBox.Text = "17:30:00.001  === 启动器开始运行（v0.7.0）===\r\n17:30:00.120  服务就绪，耗时 9.1 秒\r\n17:30:00.400  接口自检：通过（client-api 通路正常）\r\n17:30:05.000  启动更新检查：发现 2 项可更新\r\n"; }

                    string f = Path.Combine(dir, "ui-tab-" + i + ".png");
                    using (var bmp = new Bitmap(Width, Height))
                    {
                        DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                        bmp.Save(f, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    report.AppendLine("shot[" + i + "][" + tabs.TabPages[i].Text + "]=" + f);
                }
            }
            finally { Hide(); }
            report.AppendLine("ui_buttons=" + DumpButtonWidths());
            return report.ToString();
        }

        private string DumpButtonWidths()
        {
            var sb = new StringBuilder();
            DumpButtonsRec(this, sb);
            return sb.ToString();
        }

        private static void DumpButtonsRec(Control root, StringBuilder sb)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && !string.IsNullOrEmpty(b.Text))
                    sb.Append(b.Text).Append(":w=").Append(b.Width)
                      .Append(",pref=").Append(b.PreferredSize.Width).Append(" | ");
                if (c.HasChildren) DumpButtonsRec(c, sb);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 关窗只是隐藏：本程序是托盘常驻，退出统一走托盘的“停止服务并退出”
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (fastTimer != null) fastTimer.Dispose(); } catch { }
                try { if (slowTimer != null) slowTimer.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>更新列表里的一行：勾选框 + 标题 + 进度条 + 明细（阶段/速度/耗时/剩余）。</summary>
    private sealed class UpdateRow : Panel
    {
        public UpdateCandidate Item;
        public ProgressInfo Info;
        public CheckBox Check;
        public bool Running;

        private Label title;
        private Label detail;
        private ProgressBar bar;
        private bool marquee;

        public UpdateRow(UpdateCandidate item)
        {
            Item = item;
            Width = 640;
            Height = 88;
            Margin = new Padding(4, 3, 4, 3);
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = SystemColors.Window;

            Check = new CheckBox();
            Check.SetBounds(10, 12, 18, 18);
            Check.Checked = true;
            Controls.Add(Check);

            title = new Label();
            title.SetBounds(34, 8, Width - 48, 20);
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            title.Font = new Font(Font, FontStyle.Bold);
            title.Text = item.Label + "   " + item.Current + "  →  " + item.Latest;
            Controls.Add(title);

            bar = new ProgressBar();
            bar.SetBounds(34, 30, Width - 48, 15);
            bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            bar.Style = ProgressBarStyle.Continuous;
            Controls.Add(bar);

            // 明细允许两行：速度/已用/剩余都齐时单行可能放不下，换行比截断好
            detail = new Label();
            detail.AutoSize = false;
            detail.SetBounds(34, 48, Width - 48, 34);
            detail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detail.ForeColor = SystemColors.GrayText;
            detail.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 8.25f);
            detail.Text = item.Kind == "dsh" ? "等待（后台下载，失败自动回退）" : "等待（下载 → 校验 → 原子替换）";
            Controls.Add(detail);
        }

        /// <summary>当前进度条数值（自检断言用）。</summary>
        public int BarValue { get { return bar == null ? -1 : bar.Value; } }

        public void ShowRunning()
        {
            Running = true;
            if (Check != null) Check.Enabled = false;
            detail.Text = "准备中…";
        }

        public void ShowProgress(ProgressInfo p)
        {
            if (!Running) return;
            try
            {
                int pct = p.Percent();
                if (pct < 0)
                {
                    if (!marquee) { bar.Style = ProgressBarStyle.Marquee; bar.MarqueeAnimationSpeed = 30; marquee = true; }
                }
                else
                {
                    if (marquee) { bar.Style = ProgressBarStyle.Continuous; marquee = false; }
                    if (pct < bar.Minimum) pct = bar.Minimum;
                    if (pct > bar.Maximum) pct = bar.Maximum;
                    bar.Value = pct;
                }
                detail.Text = p.DetailText();
            }
            catch { }
        }

        public void ShowDone(string error)
        {
            Running = false;
            if (Check != null) Check.Enabled = true;
            try
            {
                if (marquee) { bar.Style = ProgressBarStyle.Continuous; marquee = false; }
                if (string.IsNullOrEmpty(error))
                {
                    bar.Value = bar.Maximum;
                    detail.ForeColor = Color.SeaGreen;
                    detail.Text = Item.Kind == "dsh"
                        ? "已完成（重启启动器后生效）"
                        : "已完成（已备份原版本，可回滚）";
                }
                else
                {
                    detail.ForeColor = Color.Firebrick;
                    detail.Text = "失败：" + error;
                }
            }
            catch { }
        }
    }

    // ================= DPI =================

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSMICON = 49; // 小图标（托盘槽）宽度，物理像素

    private static void SetProcessDpiAware()
    {
        try { SetProcessDPIAware(); } catch { }
    }
}
