using System.Text.Json;

namespace SupperIdaMcp.Center.Desktop.Localization;

internal sealed record AppPreferences(AppLanguage Language);

internal static class AppPreferencesStore
{
    private const string DirectoryName = ".supper-ida-mcp-center";
    private const string FileName = "preferences.json";

    public static AppPreferences Load()
    {
        try
        {
            var path = PreferencesPath();
            if (!File.Exists(path))
            {
                return new AppPreferences(DefaultLanguage());
            }

            var file = JsonSerializer.Deserialize<PreferenceFile>(File.ReadAllText(path));
            return new AppPreferences(ParseLanguage(file?.Language) ?? DefaultLanguage());
        }
        catch
        {
            return new AppPreferences(DefaultLanguage());
        }
    }

    public static void Save(AppPreferences preferences)
    {
        var path = PreferencesPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var file = new PreferenceFile(LanguageCode(preferences.Language));
        File.WriteAllText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string PreferencesPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DirectoryName,
            FileName);
    }

    private static AppLanguage DefaultLanguage()
    {
        return Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Chinese
            : AppLanguage.English;
    }

    private static string LanguageCode(AppLanguage language)
    {
        return language == AppLanguage.Chinese ? "zh-CN" : "en-US";
    }

    private static AppLanguage? ParseLanguage(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "zh" or "zh-cn" or "chinese" => AppLanguage.Chinese,
            "en" or "en-us" or "english" => AppLanguage.English,
            _ => null
        };
    }

    private sealed record PreferenceFile(string Language);
}
