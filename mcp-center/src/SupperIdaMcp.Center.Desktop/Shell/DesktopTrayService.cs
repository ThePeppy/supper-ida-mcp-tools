using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using SupperIdaMcp.Center.Desktop.Localization;

namespace SupperIdaMcp.Center.Desktop.Shell;

internal sealed class DesktopTrayService : IDisposable
{
    private readonly Application _application;
    private readonly WindowPresenter _windowPresenter;
    private readonly Localizer _text = new(AppPreferencesStore.Load().Language);
    private TrayIcons? _trayIcons;
    private TrayIcon? _trayIcon;
    private NativeMenuItem? _showItem;
    private NativeMenuItem? _hideItem;
    private NativeMenuItem? _quitItem;

    public DesktopTrayService(Application application, WindowPresenter windowPresenter)
    {
        _application = application;
        _windowPresenter = windowPresenter;
        AppPreferencesStore.Saved += OnPreferencesSaved;
    }

    public void Initialize()
    {
        try
        {
            _showItem = MenuItem("tray.show", (_, _) => _windowPresenter.Show());
            _hideItem = MenuItem("tray.hide", (_, _) => _windowPresenter.Hide());
            _quitItem = MenuItem("tray.quit", (_, _) => _windowPresenter.Exit());

            var menu = new NativeMenu();
            menu.Items.Add(_showItem);
            menu.Items.Add(_hideItem);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(_quitItem);

            _trayIcon = new TrayIcon
            {
                Icon = AppIconService.LoadTrayIcon(),
                IsVisible = true,
                Menu = menu,
                ToolTipText = _text.T("tray.tooltip")
            };
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
            _trayIcon.Clicked += OnTrayClicked;

            _trayIcons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(_application, _trayIcons);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"Tray initialization failed: {exception}");
        }
    }

    public void Dispose()
    {
        AppPreferencesStore.Saved -= OnPreferencesSaved;

        if (_trayIcon is not null)
        {
            _trayIcon.Clicked -= OnTrayClicked;
            _trayIcon.IsVisible = false;
            _trayIcon.Dispose();
        }

        _trayIcons?.Clear();
        TrayIcon.SetIcons(_application, new TrayIcons());
    }

    private NativeMenuItem MenuItem(string key, EventHandler handler)
    {
        var item = new NativeMenuItem(_text.T(key));
        item.Click += handler;
        return item;
    }

    private void OnTrayClicked(object? sender, EventArgs args)
    {
        _windowPresenter.Show();
    }

    private void OnPreferencesSaved(AppPreferences preferences)
    {
        _text.SetLanguage(preferences.Language);

        if (_showItem is not null)
        {
            _showItem.Header = _text.T("tray.show");
        }

        if (_hideItem is not null)
        {
            _hideItem.Header = _text.T("tray.hide");
        }

        if (_quitItem is not null)
        {
            _quitItem.Header = _text.T("tray.quit");
        }

        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = _text.T("tray.tooltip");
        }
    }
}
