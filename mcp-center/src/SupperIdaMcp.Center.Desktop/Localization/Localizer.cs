using System.Globalization;

namespace SupperIdaMcp.Center.Desktop.Localization;

internal sealed class Localizer
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["app.title"] = "Supper IDA MCP Center",
        ["app.subtitle"] = "Multi-window IDA analysis hub",
        ["updated"] = "Updated {0}",
        ["language"] = "Language",
        ["language.english"] = "EN",
        ["language.chinese"] = "中文",
        ["tab.targets"] = "IDA Windows",
        ["tab.activity"] = "Active Calls",
        ["tab.processes"] = "Processes",
        ["tab.installations"] = "Installations",
        ["tab.logs"] = "Operation Log",
        ["tab.settings"] = "Settings",
        ["empty.targets"] = "No IDA windows registered.",
        ["empty.activity"] = "No active agent calls.",
        ["empty.processes"] = "No IDA processes launched by the center.",
        ["empty.installations"] = "No IDA installations discovered. Set SUPPER_IDA_PATH or pass idaPath in launch calls.",
        ["empty.logs"] = "No operations logged.",
        ["button.close"] = "Close",
        ["button.configure"] = "Configure",
        ["button.reconfigure"] = "Reconfigure",
        ["button.install"] = "Install / Repair",
        ["button.reinstall"] = "Reinstall",
        ["button.uninstall"] = "Uninstall",
        ["button.archiveLegacy"] = "Archive legacy",
        ["section.center"] = "Center",
        ["section.language"] = "Language",
        ["section.plugin"] = "IDA Plugin",
        ["section.agents"] = "Agent MCP Configuration",
        ["section.manual"] = "Manual Configuration",
        ["center.runtime"] = "Runtime endpoints",
        ["center.details"] = "MCP HTTP: {0}\nIDA plugin TCP: {1}\nRepository: {2}",
        ["lastAction"] = "Last action",
        ["language.details"] = "Choose the desktop UI language. Changes apply immediately and are saved locally.",
        ["target.details"] = "Alias: {0}\nPath: {1}\nDatabase: {2}\nLast seen: {3}",
        ["operation.details"] = "Started: {0}\nTarget: {1}",
        ["process.title"] = "PID {0}",
        ["process.details"] = "Input: {0}\nExecutable: {1}\nLaunched: {2}",
        ["install.details"] = "{0}\nSource: {1}",
        ["log.title"] = "{0}  {1}",
        ["log.details"] = "{0}  Elapsed: {1}\n{2}",
        ["status.configured"] = "Configured",
        ["status.notConfigured"] = "Not configured",
        ["status.exists"] = "Exists",
        ["status.missing"] = "Missing",
        ["status.healthy"] = "Healthy",
        ["status.unknown"] = "Unknown",
        ["status.unreachable"] = "Unreachable",
        ["status.closing"] = "Closing",
        ["status.success"] = "Success",
        ["status.failed"] = "Failed",
        ["yes"] = "Yes",
        ["no"] = "No",
        ["none"] = "<none>",
        ["notDiscovered"] = "<not discovered>",
        ["noInputPath"] = "<no input path>",
        ["noDatabasePath"] = "<no database path>",
        ["noError"] = "No error",
        ["plugin.compatible"] = "Installed and compatible",
        ["plugin.attention"] = "Installed but needs attention",
        ["plugin.notInstalled"] = "Not installed",
        ["plugin.message.notInstalled"] = "Plugin loader is not installed in the IDA user plugins directory.",
        ["plugin.message.notOurs"] = "A loader with the expected filename exists, but it is not a Supper IDA MCP loader.",
        ["plugin.message.missingPackage"] = "Loader exists, but the plugin package folder is missing. Repair installation.",
        ["plugin.message.compatible"] = "Installed plugin loader matches this center version.",
        ["plugin.message.versionMismatch"] = "Installed loader version is {0}, expected {1}.",
        ["plugin.details"] = "Expected version: {0}\nInstalled version: {1}\nOurs: {2}\nLoader: {3}\nPackage: {4}\nSource: {5}\n{6}\n{7}",
        ["plugin.noWarnings"] = "No legacy or misplaced loaders detected.",
        ["plugin.archiveDone"] = "Legacy IDA MCP plugin files were archived. Restart IDA Pro to reload plugins.",
        ["agent.details"] = "Config: {0}\nExists: {1}\nLegacy config detected: {2}\n{3}",
        ["agent.summary.configuredHttp"] = "Configured for this desktop center.",
        ["agent.summary.configuredBridge"] = "Configured through the local stdio bridge.",
        ["agent.summary.legacyCodex"] = "Legacy ida-pro-mcp config detected. Add the new center config and remove old entries when ready.",
        ["agent.summary.legacyClaude"] = "Legacy IDA MCP config detected. Add the new bridge config and remove old entries when ready.",
        ["agent.summary.codexFound"] = "Config file found, center not configured.",
        ["agent.summary.codexCreate"] = "Codex config file will be created.",
        ["agent.summary.claudeFound"] = "Config file found, center bridge not configured.",
        ["agent.summary.claudeCreate"] = "Config file will be created.",
        ["manual.http"] = "For MCP clients that support Streamable HTTP, add:\n\nName: {0}\nURL:  {1}",
        ["manual.stdio"] = "For MCP clients that only support stdio, configure:\n\ncommand: dotnet\nargs:\n  - run\n  - --project\n  - {0}\n  - --\n  - --endpoint\n  - {1}",
        ["manual.stdioUnavailable"] = "Stdio bridge is unavailable because the repository path could not be discovered."
    };

    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>
    {
        ["app.title"] = "Supper IDA MCP Center",
        ["app.subtitle"] = "多窗口 IDA 逆向分析中枢",
        ["updated"] = "已更新 {0}",
        ["language"] = "语言",
        ["language.english"] = "EN",
        ["language.chinese"] = "中文",
        ["tab.targets"] = "IDA 窗口",
        ["tab.activity"] = "活动调用",
        ["tab.processes"] = "进程",
        ["tab.installations"] = "安装位置",
        ["tab.logs"] = "操作日志",
        ["tab.settings"] = "设置",
        ["empty.targets"] = "当前没有已注册的 IDA 窗口。",
        ["empty.activity"] = "当前没有正在执行的 Agent 调用。",
        ["empty.processes"] = "当前没有由中心启动的 IDA 进程。",
        ["empty.installations"] = "没有发现 IDA 安装。可设置 SUPPER_IDA_PATH，或在启动调用里传入 idaPath。",
        ["empty.logs"] = "暂无操作日志。",
        ["button.close"] = "关闭",
        ["button.configure"] = "配置",
        ["button.reconfigure"] = "重新配置",
        ["button.install"] = "安装 / 修复",
        ["button.reinstall"] = "重新安装",
        ["button.uninstall"] = "卸载",
        ["button.archiveLegacy"] = "归档旧插件",
        ["section.center"] = "中心服务",
        ["section.language"] = "语言",
        ["section.plugin"] = "IDA 插件",
        ["section.agents"] = "Agent MCP 配置",
        ["section.manual"] = "手动配置",
        ["center.runtime"] = "运行端点",
        ["center.details"] = "MCP HTTP: {0}\nIDA 插件 TCP: {1}\n仓库: {2}",
        ["lastAction"] = "上次操作",
        ["language.details"] = "选择桌面端界面语言。修改会立即生效，并保存到本机。",
        ["target.details"] = "别名: {0}\n路径: {1}\n数据库: {2}\n最后在线: {3}",
        ["operation.details"] = "开始时间: {0}\n目标: {1}",
        ["process.title"] = "PID {0}",
        ["process.details"] = "输入: {0}\n可执行文件: {1}\n启动时间: {2}",
        ["install.details"] = "{0}\n来源: {1}",
        ["log.title"] = "{0}  {1}",
        ["log.details"] = "{0}  耗时: {1}\n{2}",
        ["status.configured"] = "已配置",
        ["status.notConfigured"] = "未配置",
        ["status.exists"] = "存在",
        ["status.missing"] = "缺失",
        ["status.healthy"] = "健康",
        ["status.unknown"] = "未知",
        ["status.unreachable"] = "不可达",
        ["status.closing"] = "正在关闭",
        ["status.success"] = "成功",
        ["status.failed"] = "失败",
        ["yes"] = "是",
        ["no"] = "否",
        ["none"] = "<无>",
        ["notDiscovered"] = "<未发现>",
        ["noInputPath"] = "<无输入路径>",
        ["noDatabasePath"] = "<无数据库路径>",
        ["noError"] = "无错误",
        ["plugin.compatible"] = "已安装且兼容",
        ["plugin.attention"] = "已安装但需要处理",
        ["plugin.notInstalled"] = "未安装",
        ["plugin.message.notInstalled"] = "IDA 用户插件目录中没有安装插件 loader。",
        ["plugin.message.notOurs"] = "目标文件名的 loader 已存在，但不是 Supper IDA MCP loader。",
        ["plugin.message.missingPackage"] = "loader 已存在，但插件包目录缺失。请修复安装。",
        ["plugin.message.compatible"] = "已安装的插件 loader 与当前中心版本匹配。",
        ["plugin.message.versionMismatch"] = "已安装 loader 版本为 {0}，期望版本为 {1}。",
        ["plugin.details"] = "期望版本: {0}\n已安装版本: {1}\n是否为本工具插件: {2}\nLoader: {3}\n插件包: {4}\n源码: {5}\n{6}\n{7}",
        ["plugin.noWarnings"] = "未检测到旧插件或错位 loader。",
        ["plugin.archiveDone"] = "旧版 IDA MCP 插件文件已归档。请重启 IDA Pro 重新加载插件。",
        ["agent.details"] = "配置文件: {0}\n存在: {1}\n检测到旧配置: {2}\n{3}",
        ["agent.summary.configuredHttp"] = "已配置到当前桌面中心。",
        ["agent.summary.configuredBridge"] = "已通过本地 stdio bridge 配置。",
        ["agent.summary.legacyCodex"] = "检测到旧版 ida-pro-mcp 配置。建议添加新的中心配置，并在确认后移除旧配置。",
        ["agent.summary.legacyClaude"] = "检测到旧版 IDA MCP 配置。建议添加新的 bridge 配置，并在确认后移除旧配置。",
        ["agent.summary.codexFound"] = "已找到配置文件，但尚未配置中心服务。",
        ["agent.summary.codexCreate"] = "将创建 Codex 配置文件。",
        ["agent.summary.claudeFound"] = "已找到配置文件，但尚未配置中心 bridge。",
        ["agent.summary.claudeCreate"] = "将创建配置文件。",
        ["manual.http"] = "支持 Streamable HTTP 的 MCP 客户端可添加:\n\n名称: {0}\nURL:  {1}",
        ["manual.stdio"] = "仅支持 stdio 的 MCP 客户端可配置:\n\ncommand: dotnet\nargs:\n  - run\n  - --project\n  - {0}\n  - --\n  - --endpoint\n  - {1}",
        ["manual.stdioUnavailable"] = "无法发现仓库路径，因此 stdio bridge 暂不可用。"
    };

    public Localizer(AppLanguage language)
    {
        Language = language;
    }

    public AppLanguage Language { get; private set; }

    public CultureInfo Culture => Language == AppLanguage.Chinese
        ? CultureInfo.GetCultureInfo("zh-CN")
        : CultureInfo.GetCultureInfo("en-US");

    public void SetLanguage(AppLanguage language)
    {
        Language = language;
    }

    public string T(string key)
    {
        var table = Language == AppLanguage.Chinese ? Chinese : English;
        return table.TryGetValue(key, out var value)
            ? value
            : English.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string F(string key, params object?[] args)
    {
        return string.Format(Culture, T(key), args);
    }
}
