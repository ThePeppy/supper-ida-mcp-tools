using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace SupperIdaMcp.Center.Desktop.Shell;

internal sealed class WindowPresenter
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly MainWindow _window;
    private bool _exitRequested;
    private bool _hideOnFirstShow;

    public WindowPresenter(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        _desktop = desktop;
        _window = window;
        _window.Opened += OnWindowOpened;
        _window.Closing += OnWindowClosing;
    }

    public void Show()
    {
        MacDockVisibilityService.ShowDockIcon(activate: false);

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
        MacDockVisibilityService.ActivateApplication();
    }

    public void Hide()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
        }

        MacDockVisibilityService.HideDockIcon();
    }

    public void HideAfterFirstShow()
    {
        _hideOnFirstShow = true;
    }

    public void Exit()
    {
        _exitRequested = true;
        _desktop.Shutdown(0);
    }

    private void OnWindowOpened(object? sender, EventArgs args)
    {
        if (_hideOnFirstShow)
        {
            _hideOnFirstShow = false;
            Hide();
            return;
        }

        MacDockVisibilityService.ShowDockIcon(activate: false);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs args)
    {
        if (_exitRequested || args.IsProgrammatic)
        {
            return;
        }

        args.Cancel = true;
        Hide();
    }
}
