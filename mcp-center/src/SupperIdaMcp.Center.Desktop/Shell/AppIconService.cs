using Avalonia.Controls;
using Avalonia.Platform;

namespace SupperIdaMcp.Center.Desktop.Shell;

internal static class AppIconService
{
    public static WindowIcon LoadWindowIcon()
    {
        return LoadIcon(AppIconAssets.WindowIconUri);
    }

    public static WindowIcon LoadTrayIcon()
    {
        return LoadIcon(AppIconAssets.TrayIconUri);
    }

    private static WindowIcon LoadIcon(string assetUri)
    {
        using var asset = AssetLoader.Open(new Uri(assetUri), null);
        using var memory = new MemoryStream();
        asset.CopyTo(memory);
        memory.Position = 0;
        return new WindowIcon(memory);
    }
}
