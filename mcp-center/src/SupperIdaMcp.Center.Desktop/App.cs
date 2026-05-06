using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using SupperIdaMcp.Center.Desktop.Shell;

namespace SupperIdaMcp.Center.Desktop;

public sealed class App : Application
{
    private DesktopTrayService? _trayService;

    public override void Initialize()
    {
        Name = "Supper IDA MCP Center";
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;

            var mainWindow = new MainWindow();
            var windowPresenter = new WindowPresenter(desktop, mainWindow);

            desktop.MainWindow = mainWindow;
            _trayService = new DesktopTrayService(this, windowPresenter);
            _trayService.Initialize();

            if (DesktopSettings.Current.StartMinimized)
            {
                windowPresenter.HideAfterFirstShow();
            }

            desktop.ShutdownRequested += (_, _) =>
            {
                _trayService?.Dispose();
                RuntimeHolder.DisposeAsync().AsTask().GetAwaiter().GetResult();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
