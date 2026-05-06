using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Desktop.Localization;
using SupperIdaMcp.Center.Desktop.Setup;

namespace SupperIdaMcp.Center.Desktop;

public sealed class MainWindow : Window
{
    private readonly Localizer _text = new(AppPreferencesStore.Load().Language);
    private StackPanel _targets = Panel(10);
    private StackPanel _activity = Panel(10);
    private StackPanel _processes = Panel(10);
    private StackPanel _installations = Panel(10);
    private StackPanel _logs = Panel(10);
    private StackPanel _settings = Panel(14);
    private TextBlock _status = new();
    private bool _settingsDirty = true;
    private int _selectedTabIndex;
    private string _settingsMessage = string.Empty;

    public MainWindow()
    {
        Title = _text.T("app.title");
        Width = 1120;
        Height = 760;
        MinWidth = 920;
        MinHeight = 620;
        Background = Brush("#F5F7FB");

        RuntimeHolder.Start();
        Content = BuildLayout();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh();
    }

    private Control BuildLayout()
    {
        Title = _text.T("app.title");

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = Brush("#F5F7FB")
        };

        root.Children.Add(BuildHeader());

        var tabs = new TabControl
        {
            Margin = new Thickness(22, 12, 22, 20),
            FontSize = 14,
            Items =
            {
                Tab(_text.T("tab.targets"), Wrap(_targets)),
                Tab(_text.T("tab.activity"), Wrap(_activity)),
                Tab(_text.T("tab.processes"), Wrap(_processes)),
                Tab(_text.T("tab.installations"), Wrap(_installations)),
                Tab(_text.T("tab.logs"), Wrap(_logs)),
                Tab(_text.T("tab.settings"), Wrap(_settings))
            }
        };
        tabs.SelectedIndex = Math.Clamp(_selectedTabIndex, 0, 5);
        tabs.SelectionChanged += (_, _) => _selectedTabIndex = tabs.SelectedIndex;
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        return root;
    }

    private Control BuildHeader()
    {
        var header = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E4E7EC"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 18)
        };

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var title = new TextBlock
        {
            Text = _text.T("app.title"),
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush("#101828")
        };
        var subtitle = new TextBlock
        {
            Text = _text.T("app.subtitle"),
            FontSize = 13,
            Foreground = Brush("#667085")
        };
        var endpoints = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                Chip($"MCP  {RuntimeHolder.McpEndpoint}", RowKind.Info),
                Chip($"TCP  {RuntimeHolder.TcpEndpoint}", RowKind.Neutral)
            }
        };

        layout.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children = { title, subtitle, endpoints }
        });

        _status = new TextBlock
        {
            Text = _text.F("updated", DateTime.Now.ToString("T", _text.Culture)),
            FontSize = 12,
            FontWeight = FontWeight.Medium
        };
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Chip(_status, RowKind.Neutral),
                LanguageSwitch()
            }
        };
        Grid.SetColumn(right, 1);
        layout.Children.Add(right);

        header.Child = layout;
        return header;
    }

    private Control LanguageSwitch()
    {
        var box = new Border
        {
            Background = Brush("#F2F4F7"),
            BorderBrush = Brush("#D0D5DD"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(3),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                Children =
                {
                    SegmentButton(_text.T("language.chinese"), AppLanguage.Chinese),
                    SegmentButton(_text.T("language.english"), AppLanguage.English)
                }
            }
        };
        return box;
    }

    private Button SegmentButton(string label, AppLanguage language)
    {
        var selected = _text.Language == language;
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium
            },
            Padding = new Thickness(10, 5),
            MinHeight = 28,
            Background = selected ? Brush("#101828") : Brushes.Transparent,
            Foreground = selected ? Brushes.White : Brush("#475467"),
            BorderBrush = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        button.Click += (_, _) => SetLanguage(language);
        AttachButtonHover(button, selected ? ButtonKind.Dark : ButtonKind.Ghost, selected);
        return button;
    }

    private void SetLanguage(AppLanguage language)
    {
        if (_text.Language == language)
        {
            return;
        }

        _text.SetLanguage(language);
        AppPreferencesStore.Save(new AppPreferences(language));
        _settingsDirty = true;
        _selectedTabIndex = 5;
        ResetPagePanels();
        Content = BuildLayout();
        Refresh();
    }

    private void ResetPagePanels()
    {
        _targets = Panel(10);
        _activity = Panel(10);
        _processes = Panel(10);
        _installations = Panel(10);
        _logs = Panel(10);
        _settings = Panel(14);
    }

    private void Refresh()
    {
        _status.Text = _text.F("updated", DateTime.Now.ToString("T", _text.Culture));
        RenderTargets();
        RenderActiveOperations();
        RenderProcesses();
        RenderInstallations();
        RenderLogs();
        if (_settingsDirty)
        {
            RenderSettings();
            _settingsDirty = false;
        }
    }

    private void RenderTargets()
    {
        _targets.Children.Clear();
        var targets = RuntimeHolder.TargetRegistry.ListTargets();
        if (targets.Count == 0)
        {
            _targets.Children.Add(Empty(_text.T("empty.targets")));
            return;
        }

        foreach (var target in targets)
        {
            var closeButton = ActionButton(_text.T("button.close"), () => RuntimeHolder.IdaLaunchService.CloseProcess(target.ProcessId, force: false));
            _targets.Children.Add(Row(
                target.Alias,
                _text.F(
                    "target.details",
                    target.BinaryName,
                    target.InputPath ?? _text.T("noInputPath"),
                    target.DatabasePath ?? _text.T("noDatabasePath"),
                    target.LastSeenUtc.LocalDateTime.ToString("T", _text.Culture)),
                closeButton,
                target.Health == TargetHealth.Healthy ? RowKind.Success : RowKind.Warning,
                $"{_text.T("status." + HealthKey(target.Health))} · PID {target.ProcessId}"));
        }
    }

    private void RenderActiveOperations()
    {
        _activity.Children.Clear();
        var operations = RuntimeHolder.ActiveOperations.List();
        if (operations.Count == 0)
        {
            _activity.Children.Add(Empty(_text.T("empty.activity")));
            return;
        }

        foreach (var operation in operations)
        {
            _activity.Children.Add(Row(
                $"{operation.TargetAlias} · {operation.ToolName}",
                _text.F(
                    "operation.details",
                    operation.StartedAtUtc.LocalDateTime.ToString("T", _text.Culture),
                    operation.TargetInstanceId),
                null,
                RowKind.Info));
        }
    }

    private void RenderProcesses()
    {
        _processes.Children.Clear();
        var processes = RuntimeHolder.IdaLaunchService.ListLaunchedProcesses();
        if (processes.Count == 0)
        {
            _processes.Children.Add(Empty(_text.T("empty.processes")));
            return;
        }

        foreach (var process in processes)
        {
            _processes.Children.Add(Row(
                _text.F("process.title", process.ProcessId),
                _text.F(
                    "process.details",
                    process.InputPath,
                    process.ExecutablePath,
                    process.LaunchedAtUtc.LocalDateTime.ToString("T", _text.Culture)),
                null,
                process.HasExited ? RowKind.Neutral : RowKind.Success,
                process.HasExited ? _text.T("status.closing") : _text.T("status.healthy")));
        }
    }

    private void RenderInstallations()
    {
        _installations.Children.Clear();
        var installations = RuntimeHolder.IdaLocator.FindInstallations();
        if (installations.Count == 0)
        {
            _installations.Children.Add(Empty(_text.T("empty.installations")));
            return;
        }

        foreach (var install in installations)
        {
            _installations.Children.Add(Row(
                install.DisplayName,
                _text.F("install.details", install.Path, install.Source),
                null,
                install.Exists ? RowKind.Success : RowKind.Warning,
                install.Exists ? _text.T("status.exists") : _text.T("status.missing")));
        }
    }

    private void RenderLogs()
    {
        _logs.Children.Clear();
        var logs = RuntimeHolder.OperationLog.List(200);
        if (logs.Count == 0)
        {
            _logs.Children.Add(Empty(_text.T("empty.logs")));
            return;
        }

        foreach (var log in logs)
        {
            _logs.Children.Add(Row(
                _text.F("log.title", log.TargetAlias, log.ToolName),
                _text.F(
                    "log.details",
                    log.TimestampUtc.LocalDateTime.ToString("T", _text.Culture),
                    log.Elapsed,
                    log.Error ?? _text.T("noError")),
                null,
                log.Success ? RowKind.Success : RowKind.Danger,
                log.Success ? _text.T("status.success") : _text.T("status.failed")));
        }
    }

    private void RenderSettings()
    {
        _settings.Children.Clear();
        _settings.Children.Add(SectionTitle(_text.T("section.center")));
        _settings.Children.Add(Row(
            _text.T("center.runtime"),
            _text.F(
                "center.details",
                RuntimeHolder.McpEndpoint,
                RuntimeHolder.TcpEndpoint,
                RuntimeHolder.RepositoryPaths.Root ?? _text.T("notDiscovered")),
            null,
            RowKind.Info));

        _settings.Children.Add(SectionTitle(_text.T("section.language")));
        _settings.Children.Add(Row(
            _text.T("language"),
            _text.T("language.details"),
            LanguageSwitch(),
            RowKind.Neutral,
            _text.Language == AppLanguage.Chinese ? "中文" : "English"));

        if (!string.IsNullOrWhiteSpace(_settingsMessage))
        {
            _settings.Children.Add(Row(_text.T("lastAction"), _settingsMessage, null, RowKind.Info));
        }

        RenderPluginSettings();
        RenderAgentSettings();
    }

    private void RenderPluginSettings()
    {
        var status = RuntimeHolder.PluginInstallService.GetStatus();
        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        actionButtons.Children.Add(ActionButton(
            status.IsCompatible ? _text.T("button.reinstall") : _text.T("button.install"),
            () => RunSettingsAction(() =>
            {
                var next = RuntimeHolder.PluginInstallService.InstallOrRepair();
                _settingsMessage = LocalizePluginMessage(next);
            }),
            ButtonKind.Primary));

        actionButtons.Children.Add(ActionButton(
            _text.T("button.uninstall"),
            () => RunSettingsAction(() =>
            {
                var next = RuntimeHolder.PluginInstallService.Uninstall();
                _settingsMessage = LocalizePluginMessage(next);
            })));

        if (status.Warnings.Count > 0)
        {
            actionButtons.Children.Add(ActionButton(
                _text.T("button.archiveLegacy"),
                () => RunSettingsAction(() =>
                {
                    var next = RuntimeHolder.PluginInstallService.ArchiveLegacyPlugins();
                    _settingsMessage = next.Warnings.Count == 0
                        ? _text.T("plugin.archiveDone")
                        : LocalizePluginMessage(next);
                }),
                ButtonKind.Warning));
        }

        var kind = status.IsCompatible && status.Warnings.Count == 0
            ? RowKind.Success
            : status.Warnings.Count > 0
                ? RowKind.Warning
                : RowKind.Danger;

        _settings.Children.Add(SectionTitle(_text.T("section.plugin")));
        var warnings = status.Warnings.Count == 0
            ? _text.T("plugin.noWarnings")
            : string.Join("\n", status.Warnings.Select(warning => "• " + warning));
        _settings.Children.Add(Row(
            PluginTitle(status),
            _text.F(
                "plugin.details",
                status.ExpectedVersion,
                status.InstalledVersion ?? _text.T("none"),
                YesNo(status.IsOurs),
                status.LoaderPath,
                status.PackagePath,
                status.SourceRoot ?? _text.T("notDiscovered"),
                LocalizePluginMessage(status),
                warnings),
            actionButtons,
            kind));
    }

    private void RenderAgentSettings()
    {
        _settings.Children.Add(SectionTitle(_text.T("section.agents")));
        foreach (var agent in RuntimeHolder.AgentConfigService.Detect())
        {
            var configureButton = ActionButton(
                agent.IsConfigured ? _text.T("button.reconfigure") : _text.T("button.configure"),
                () => RunSettingsAction(() =>
                {
                    var next = RuntimeHolder.AgentConfigService.Configure(agent.AgentName, agent.ConfigPath);
                    _settingsMessage = $"{next.AgentName}: {AgentSummary(next)}";
                }),
                ButtonKind.Primary,
                agent.CanAutoConfigure);

            _settings.Children.Add(Row(
                agent.AgentName,
                _text.F(
                    "agent.details",
                    agent.ConfigPath,
                    YesNo(agent.ConfigExists),
                    YesNo(agent.HasLegacyConfig),
                    AgentSummary(agent)),
                configureButton,
                agent.IsConfigured ? RowKind.Success : agent.HasLegacyConfig ? RowKind.Warning : RowKind.Neutral,
                agent.IsConfigured ? _text.T("status.configured") : _text.T("status.notConfigured")));
        }

        _settings.Children.Add(SectionTitle(_text.T("section.manual")));
        _settings.Children.Add(CodeBlock(_text.F("manual.http", ProductInfo.AgentServerName, RuntimeHolder.McpEndpoint)));
        _settings.Children.Add(CodeBlock(ManualStdioSnippet()));
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

        _settingsDirty = true;
        Refresh();
    }

    private string ManualStdioSnippet()
    {
        var bridge = RuntimeHolder.AgentConfigService.GetBridgeProjectPath();
        return bridge is null
            ? _text.T("manual.stdioUnavailable")
            : _text.F("manual.stdio", bridge, RuntimeHolder.McpEndpoint);
    }

    private string PluginTitle(PluginInstallStatus status)
    {
        return status.IsCompatible
            ? _text.T("plugin.compatible")
            : status.IsInstalled ? _text.T("plugin.attention") : _text.T("plugin.notInstalled");
    }

    private string LocalizePluginMessage(PluginInstallStatus status)
    {
        if (!status.IsInstalled)
        {
            return _text.T("plugin.message.notInstalled");
        }

        if (!status.IsOurs)
        {
            return _text.T("plugin.message.notOurs");
        }

        if (!Directory.Exists(status.PackagePath))
        {
            return _text.T("plugin.message.missingPackage");
        }

        return status.IsCompatible
            ? _text.T("plugin.message.compatible")
            : _text.F("plugin.message.versionMismatch", status.InstalledVersion ?? _text.T("status.unknown"), status.ExpectedVersion);
    }

    private string AgentSummary(AgentConfigStatus agent)
    {
        var isCodex = agent.AgentName.StartsWith("Codex", StringComparison.OrdinalIgnoreCase);
        if (agent.IsConfigured)
        {
            return _text.T(isCodex ? "agent.summary.configuredHttp" : "agent.summary.configuredBridge");
        }

        if (agent.HasLegacyConfig)
        {
            return _text.T(isCodex ? "agent.summary.legacyCodex" : "agent.summary.legacyClaude");
        }

        if (agent.ConfigExists)
        {
            return _text.T(isCodex ? "agent.summary.codexFound" : "agent.summary.claudeFound");
        }

        return _text.T(isCodex ? "agent.summary.codexCreate" : "agent.summary.claudeCreate");
    }

    private string YesNo(bool value)
    {
        return _text.T(value ? "yes" : "no");
    }

    private static string HealthKey(TargetHealth health)
    {
        return health switch
        {
            TargetHealth.Healthy => "healthy",
            TargetHealth.Unreachable => "unreachable",
            TargetHealth.Closing => "closing",
            _ => "unknown"
        };
    }

    private static TabItem Tab(string title, Control content)
    {
        return new TabItem
        {
            Header = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(6, 0)
            },
            Content = content
        };
    }

    private static StackPanel Panel(double spacing)
    {
        return new StackPanel { Spacing = spacing };
    }

    private static Control Wrap(Control content)
    {
        content.Margin = new Thickness(0, 0, 24, 36);
        return new ScrollViewer
        {
            Content = content,
            Padding = new Thickness(4, 0, 24, 32),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private static Control Empty(string text)
    {
        return Row(text, string.Empty, null, RowKind.Neutral);
    }

    private static Control SectionTitle(string text)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(0, 12, 0, 2),
            Children =
            {
                new TextBlock
                {
                    Text = text,
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Brush("#101828"),
                    VerticalAlignment = VerticalAlignment.Center
                },
                SectionRule()
            }
        };
    }

    private static Control SectionRule()
    {
        var rule = new Border
        {
            Height = 1,
            Background = Brush("#E4E7EC"),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(rule, 1);
        return rule;
    }

    private static Control CodeBlock(string text)
    {
        return new Border
        {
            Background = Brush("#111827"),
            BorderBrush = Brush("#1F2937"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new SelectableTextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Menlo, Consolas, monospace"),
                FontSize = 12,
                Foreground = Brush("#F9FAFB"),
                LineHeight = 18
            }
        };
    }

    private static Control Row(string title, string details, Control? action = null, RowKind kind = RowKind.Neutral, string? badge = null)
    {
        var style = RowPalette(kind);
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush("#101828"),
                    VerticalAlignment = VerticalAlignment.Center
                }
            }
        };
        if (!string.IsNullOrWhiteSpace(badge))
        {
            header.Children.Add(Chip(badge, kind));
        }

        var content = new StackPanel
        {
            Spacing = string.IsNullOrWhiteSpace(details) ? 0 : 8,
            Children = { header }
        };
        if (!string.IsNullOrWhiteSpace(details))
        {
            content.Children.Add(new TextBlock
            {
                Text = details,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brush("#475467"),
                LineHeight = 19
            });
        }

        grid.Children.Add(content);

        if (action is not null)
        {
            Grid.SetColumn(action, 1);
            action.Margin = new Thickness(16, 0, 0, 0);
            action.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(action);
        }

        var card = new Border
        {
            Padding = new Thickness(14),
            Background = style.Background,
            BorderBrush = style.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Transitions =
            [
                new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(140) },
                new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(140) }
            ],
            Child = grid
        };
        AttachCardHover(card, style);
        return card;
    }

    private Button ActionButton(string text, Action action, ButtonKind kind = ButtonKind.Secondary, bool enabled = true)
    {
        var palette = ButtonPalette(kind);
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeight.Medium
            },
            Padding = new Thickness(12, 7),
            MinHeight = 32,
            Background = palette.Background,
            Foreground = palette.Foreground,
            BorderBrush = palette.Border,
            BorderThickness = new Thickness(1),
            Transitions =
            [
                new BrushTransition { Property = Button.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(120) },
                new BrushTransition { Property = Button.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(120) },
                new BrushTransition { Property = Button.ForegroundProperty, Duration = TimeSpan.FromMilliseconds(120) }
            ],
            Cursor = new Cursor(StandardCursorType.Hand),
            IsEnabled = enabled,
            Opacity = enabled ? 1 : 0.45
        };
        button.Click += (_, _) => action();
        AttachButtonHover(button, kind, selected: false);
        return button;
    }

    private static Border Chip(string text, RowKind kind)
    {
        var style = RowPalette(kind);
        return Chip(new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = style.ChipForeground
        }, kind);
    }

    private static Border Chip(Control content, RowKind kind)
    {
        var style = RowPalette(kind);
        if (content is TextBlock textBlock)
        {
            textBlock.Foreground = style.ChipForeground;
        }

        return new Border
        {
            Background = style.ChipBackground,
            BorderBrush = style.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(9, 4),
            Child = content
        };
    }

    private static void AttachCardHover(Border border, RowStyle style)
    {
        border.PointerEntered += (_, _) =>
        {
            border.Background = style.HoverBackground;
            border.BorderBrush = style.Accent;
        };
        border.PointerExited += (_, _) =>
        {
            border.Background = style.Background;
            border.BorderBrush = style.Border;
        };
    }

    private static void AttachButtonHover(Button button, ButtonKind kind, bool selected)
    {
        if (selected || !button.IsEnabled)
        {
            return;
        }

        var normal = ButtonPalette(kind);
        var hover = ButtonHoverPalette(kind);
        button.PointerEntered += (_, _) =>
        {
            button.Background = hover.Background;
            button.BorderBrush = hover.Border;
            button.Foreground = hover.Foreground;
        };
        button.PointerExited += (_, _) =>
        {
            button.Background = normal.Background;
            button.BorderBrush = normal.Border;
            button.Foreground = normal.Foreground;
        };
    }

    private static RowStyle RowPalette(RowKind kind)
    {
        return kind switch
        {
            RowKind.Success => new RowStyle(Brush("#12B76A"), Brush("#D1FADF"), Brush("#F6FEF9"), Brush("#ECFDF3"), Brush("#D1FADF"), Brush("#039855")),
            RowKind.Warning => new RowStyle(Brush("#F79009"), Brush("#FEDF89"), Brush("#FFFCF5"), Brush("#FFFAEB"), Brush("#FEF0C7"), Brush("#DC6803")),
            RowKind.Danger => new RowStyle(Brush("#F04438"), Brush("#FECDCA"), Brush("#FFFBFA"), Brush("#FEF3F2"), Brush("#FEE4E2"), Brush("#D92D20")),
            RowKind.Info => new RowStyle(Brush("#2E90FA"), Brush("#B2DDFF"), Brush("#F5FAFF"), Brush("#EFF8FF"), Brush("#D1E9FF"), Brush("#1570EF")),
            _ => new RowStyle(Brush("#98A2B3"), Brush("#E4E7EC"), Brushes.White, Brush("#F9FAFB"), Brush("#F2F4F7"), Brush("#667085"))
        };
    }

    private static ButtonStyle ButtonPalette(ButtonKind kind)
    {
        return kind switch
        {
            ButtonKind.Primary => new ButtonStyle(Brush("#1570EF"), Brush("#1570EF"), Brushes.White),
            ButtonKind.Warning => new ButtonStyle(Brush("#DC6803"), Brush("#DC6803"), Brushes.White),
            ButtonKind.Dark => new ButtonStyle(Brush("#101828"), Brush("#101828"), Brushes.White),
            ButtonKind.Ghost => new ButtonStyle(Brush("#00000000"), Brush("#00000000"), Brush("#475467")),
            _ => new ButtonStyle(Brush("#FFFFFF"), Brush("#D0D5DD"), Brush("#344054"))
        };
    }

    private static ButtonStyle ButtonHoverPalette(ButtonKind kind)
    {
        return kind switch
        {
            ButtonKind.Primary => new ButtonStyle(Brush("#175CD3"), Brush("#175CD3"), Brushes.White),
            ButtonKind.Warning => new ButtonStyle(Brush("#B54708"), Brush("#B54708"), Brushes.White),
            ButtonKind.Dark => new ButtonStyle(Brush("#101828"), Brush("#101828"), Brushes.White),
            ButtonKind.Ghost => new ButtonStyle(Brush("#EAECF0"), Brush("#EAECF0"), Brush("#101828")),
            _ => new ButtonStyle(Brush("#F9FAFB"), Brush("#98A2B3"), Brush("#101828"))
        };
    }

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    private sealed record RowStyle(
        IBrush Accent,
        IBrush Border,
        IBrush Background,
        IBrush HoverBackground,
        IBrush ChipBackground,
        IBrush ChipForeground);

    private sealed record ButtonStyle(IBrush Background, IBrush Border, IBrush Foreground);

    private enum RowKind
    {
        Neutral,
        Info,
        Success,
        Warning,
        Danger
    }

    private enum ButtonKind
    {
        Secondary,
        Primary,
        Warning,
        Dark,
        Ghost
    }
}
