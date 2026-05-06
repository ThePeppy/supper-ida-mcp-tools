using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace SupperIdaMcp.Center.Desktop.Shell;

internal sealed class WindowPresenter
{
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly MainWindow _window;
    private bool _exitRequested;

    public WindowPresenter(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        _desktop = desktop;
        _window = window;
        _window.Closing += OnWindowClosing;
    }

    public void Show()
    {
        if (!_window.IsVisible)
        {
            _window.Show();
        }

        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    public void Hide()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
        }
    }

    public void HideAfterFirstShow()
    {
        void HideWhenOpened(object? sender, EventArgs args)
        {
            _window.Opened -= HideWhenOpened;
            Hide();
        }

        _window.Opened += HideWhenOpened;
    }

    public void Exit()
    {
        _exitRequested = true;
        _desktop.Shutdown(0);
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
