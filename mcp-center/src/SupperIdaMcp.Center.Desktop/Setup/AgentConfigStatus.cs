namespace SupperIdaMcp.Center.Desktop.Setup;

internal sealed record AgentConfigStatus(
    string AgentName,
    string ConfigPath,
    bool ConfigExists,
    bool IsConfigured,
    bool HasLegacyConfig,
    bool CanAutoConfigure,
    string Summary,
    string ManualSnippet);
