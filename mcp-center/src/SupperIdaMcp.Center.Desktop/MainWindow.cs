using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Desktop.Setup;

namespace SupperIdaMcp.Center.Desktop;

public sealed class MainWindow : Window
{
    private readonly StackPanel _targets = new() { Spacing = 8 };
    private readonly StackPanel _activity = new() { Spacing = 8 };
    private readonly StackPanel _processes = new() { Spacing = 8 };
    private readonly StackPanel _installations = new() { Spacing = 8 };
    private readonly StackPanel _logs = new() { Spacing = 8 };
    private readonly StackPanel _settings = new() { Spacing = 12 };
    private readonly TextBlock _status = new();
    private string _settingsMessage = string.Empty;

    public MainWindow()
    {
        Title = "Supper IDA MCP Center";
        Width = 1120;
        Height = 760;
        MinWidth = 920;
        MinHeight = 620;
        RuntimeHolder.Start();
        Content = BuildLayout();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
    }

    private Control BuildLayout()
    {
        var root = new DockPanel();
        Background = new SolidColorBrush(Color.Parse("#F4F6FA"));
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(22, 18, 22, 12)
        };
        header.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Supper IDA MCP Center", FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Brushes.Black },
                new TextBlock { Text = $"MCP: {RuntimeHolder.McpEndpoint}    IDA TCP: {RuntimeHolder.TcpEndpoint}", FontSize = 13, Foreground = new SolidColorBrush(Color.Parse("#475467")) }
            }
        });
        Grid.SetColumn(_status, 1);
        _status.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(_status);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var tabs = new TabControl
        {
            Margin = new Thickness(18, 0, 18, 18),
            Items =
            {
                Tab("IDA Windows", Wrap(_targets)),
                Tab("Active Calls", Wrap(_activity)),
                Tab("Processes", Wrap(_processes)),
                Tab("Installations", Wrap(_installations)),
                Tab("Operation Log", Wrap(_logs)),
                Tab("Settings", Wrap(_settings))
            }
        };
        root.Children.Add(tabs);
        return root;
    }

    private void Refresh()
    {
        _status.Text = $"Updated {DateTime.Now:T}";
        RenderTargets();
        RenderActiveOperations();
        RenderProcesses();
        RenderInstallations();
        RenderLogs();
        RenderSettings();
    }

    private void RenderTargets()
    {
        _targets.Children.Clear();
        var targets = RuntimeHolder.TargetRegistry.ListTargets();
        if (targets.Count == 0)
        {
            _targets.Children.Add(Empty("No IDA windows registered."));
            return;
        }

        foreach (var target in targets)
        {
            var closeButton = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Left };
            closeButton.Click += (_, _) => RuntimeHolder.IdaLaunchService.CloseProcess(target.ProcessId, force: false);
            _targets.Children.Add(Row(
                $"{target.Alias}  PID {target.ProcessId}  {target.Health}",
                $"{target.BinaryName}\n{target.InputPath ?? "<no input path>"}\nLast seen: {target.LastSeenUtc.LocalDateTime:T}",
                closeButton));
        }
    }

    private void RenderActiveOperations()
    {
        _activity.Children.Clear();
        var operations = RuntimeHolder.ActiveOperations.List();
        if (operations.Count == 0)
        {
            _activity.Children.Add(Empty("No active agent calls."));
            return;
        }

        foreach (var operation in operations)
        {
            _activity.Children.Add(Row(
                $"{operation.TargetAlias}  {operation.ToolName}",
                $"Started: {operation.StartedAtUtc.LocalDateTime:T}\nTarget: {operation.TargetInstanceId}"));
        }
    }

    private void RenderProcesses()
    {
        _processes.Children.Clear();
        var processes = RuntimeHolder.IdaLaunchService.ListLaunchedProcesses();
        if (processes.Count == 0)
        {
            _processes.Children.Add(Empty("No IDA processes launched by the center."));
            return;
        }

        foreach (var process in processes)
        {
            _processes.Children.Add(Row(
                $"PID {process.ProcessId}  Exited: {process.HasExited}",
                $"{process.InputPath}\n{process.ExecutablePath}\nLaunched: {process.LaunchedAtUtc.LocalDateTime:T}"));
        }
    }

    private void RenderInstallations()
    {
        _installations.Children.Clear();
        var installations = RuntimeHolder.IdaLocator.FindInstallations();
        if (installations.Count == 0)
        {
            _installations.Children.Add(Empty("No IDA installations discovered. Set SUPPER_IDA_PATH or pass idaPath in launch calls."));
            return;
        }

        foreach (var install in installations)
        {
            _installations.Children.Add(Row(
                $"{install.DisplayName}  Exists: {install.Exists}",
                $"{install.Path}\nSource: {install.Source}"));
        }
    }

    private void RenderLogs()
    {
        _logs.Children.Clear();
        var logs = RuntimeHolder.OperationLog.List(200);
        if (logs.Count == 0)
        {
            _logs.Children.Add(Empty("No operations logged."));
            return;
        }

        foreach (var log in logs)
        {
            _logs.Children.Add(Row(
                $"{log.TargetAlias}  {log.ToolName}  Success: {log.Success}",
                $"{log.TimestampUtc.LocalDateTime:T}  Elapsed: {log.Elapsed}\n{log.Error ?? "No error"}"));
        }
    }

    private void RenderSettings()
    {
        _settings.Children.Clear();
        _settings.Children.Add(SectionTitle("Center"));
        _settings.Children.Add(Row(
            "Runtime endpoints",
            $"MCP HTTP: {RuntimeHolder.McpEndpoint}\nIDA plugin TCP: {RuntimeHolder.TcpEndpoint}\nRepository: {RuntimeHolder.RepositoryPaths.Root ?? "<not discovered>"}"));

        if (!string.IsNullOrWhiteSpace(_settingsMessage))
        {
            _settings.Children.Add(Row("Last action", _settingsMessage));
        }

        RenderPluginSettings();
        RenderAgentSettings();
    }

    private void RenderPluginSettings()
    {
        var status = RuntimeHolder.PluginInstallService.GetStatus();
        var installButton = new Button
        {
            Content = status.IsCompatible ? "Reinstall" : "Install / Repair",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        installButton.Click += (_, _) => RunSettingsAction(() =>
        {
            var next = RuntimeHolder.PluginInstallService.InstallOrRepair();
            _settingsMessage = next.Message;
        });

        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        actionButtons.Children.Add(installButton);

        var uninstallButton = new Button
        {
            Content = "Uninstall",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        uninstallButton.Click += (_, _) => RunSettingsAction(() =>
        {
            var next = RuntimeHolder.PluginInstallService.Uninstall();
            _settingsMessage = next.Message;
        });
        actionButtons.Children.Add(uninstallButton);

        if (status.Warnings.Count > 0)
        {
            var archiveButton = new Button
            {
                Content = "Archive legacy",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            archiveButton.Click += (_, _) => RunSettingsAction(() =>
            {
                var next = RuntimeHolder.PluginInstallService.ArchiveLegacyPlugins();
                _settingsMessage = next.Warnings.Count == 0
                    ? "Legacy IDA MCP plugin files were archived. Restart IDA Pro to reload plugins."
                    : next.Message;
            });
            actionButtons.Children.Add(archiveButton);
        }

        var kind = status.IsCompatible && status.Warnings.Count == 0
            ? RowKind.Success
            : status.Warnings.Count > 0
                ? RowKind.Warning
                : RowKind.Danger;

        _settings.Children.Add(SectionTitle("IDA Plugin"));
        var warnings = status.Warnings.Count == 0
            ? "No legacy or misplaced loaders detected."
            : string.Join("\n", status.Warnings);
        _settings.Children.Add(Row(
            status.IsCompatible ? "Installed and compatible" : status.IsInstalled ? "Installed but needs attention" : "Not installed",
            $"Expected version: {status.ExpectedVersion}\nInstalled version: {status.InstalledVersion ?? "<none>"}\nOurs: {status.IsOurs}\nLoader: {status.LoaderPath}\nPackage: {status.PackagePath}\nSource: {status.SourceRoot ?? "<not discovered>"}\n{status.Message}\n{warnings}",
            actionButtons,
            kind));
    }

    private void RenderAgentSettings()
    {
        _settings.Children.Add(SectionTitle("Agent MCP Configuration"));
        foreach (var agent in RuntimeHolder.AgentConfigService.Detect())
        {
            var configureButton = new Button
            {
                Content = agent.IsConfigured ? "Reconfigure" : "Configure",
                IsEnabled = agent.CanAutoConfigure,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            configureButton.Click += (_, _) => RunSettingsAction(() =>
            {
                var next = RuntimeHolder.AgentConfigService.Configure(agent.AgentName, agent.ConfigPath);
                _settingsMessage = $"{next.AgentName}: {next.Summary}";
            });

            _settings.Children.Add(Row(
                $"{agent.AgentName}  {(agent.IsConfigured ? "Configured" : "Not configured")}",
                $"Config: {agent.ConfigPath}\nExists: {agent.ConfigExists}\nLegacy config detected: {agent.HasLegacyConfig}\n{agent.Summary}",
                configureButton));
        }

        _settings.Children.Add(SectionTitle("Manual Configuration"));
        _settings.Children.Add(CodeBlock(RuntimeHolder.AgentConfigService.ManualHttpSnippet()));
        _settings.Children.Add(CodeBlock(RuntimeHolder.AgentConfigService.ManualStdioSnippet()));
    }

    private void RunSettingsAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exc)
        {
            _settingsMessage = exc.Message;
        }

        Refresh();
    }

    private static TabItem Tab(string title, Control content)
    {
        return new TabItem { Header = title, Content = content };
    }

    private static Control Wrap(Control content)
    {
        return new ScrollViewer { Content = content, Padding = new Thickness(4) };
    }

    private static Control Empty(string text)
    {
        return new Border
        {
            Padding = new Thickness(14),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D0D5DD")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(Color.Parse("#475467")),
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control SectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#101828")),
            Margin = new Thickness(0, 8, 0, 0)
        };
    }

    private static Control CodeBlock(string text)
    {
        return new TextBox
        {
            Text = text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 120,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#D0D5DD")),
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            FontSize = 12,
            Padding = new Thickness(10)
        };
    }

    private static Control Row(string title, string details, Control? action = null, RowKind kind = RowKind.Neutral)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var (border, background) = RowPalette(kind);
        grid.Children.Add(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#101828"))
                },
                new TextBlock
                {
                    Text = details,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#475467")),
                    LineHeight = 19
                }
            }
        });

        if (action is not null)
        {
            Grid.SetColumn(action, 1);
            action.Margin = new Thickness(16, 0, 0, 0);
            action.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(action);
        }

        return new Border
        {
            Padding = new Thickness(14),
            Background = background,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid
        };
    }

    private static (IBrush Border, IBrush Background) RowPalette(RowKind kind)
    {
        return kind switch
        {
            RowKind.Success => (new SolidColorBrush(Color.Parse("#12B76A")), new SolidColorBrush(Color.Parse("#F6FEF9"))),
            RowKind.Warning => (new SolidColorBrush(Color.Parse("#F79009")), new SolidColorBrush(Color.Parse("#FFFCF5"))),
            RowKind.Danger => (new SolidColorBrush(Color.Parse("#F04438")), new SolidColorBrush(Color.Parse("#FFFBFA"))),
            RowKind.Info => (new SolidColorBrush(Color.Parse("#2E90FA")), new SolidColorBrush(Color.Parse("#F5FAFF"))),
            _ => (new SolidColorBrush(Color.Parse("#D0D5DD")), Brushes.White)
        };
    }

    private enum RowKind
    {
        Neutral,
        Info,
        Success,
        Warning,
        Danger
    }
}
