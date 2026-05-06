using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using SupperIdaMcp.Center.Core;
using SupperIdaMcp.Center.Desktop.Localization;
using SupperIdaMcp.Center.Desktop.Setup;
using SupperIdaMcp.Center.Desktop.UI;
using SupperIdaMcp.Center.Ida;

namespace SupperIdaMcp.Center.Desktop;

public sealed class MainWindow : Window
{
    private const string SurfacePrimary = "#FFFFFF";
    private const string SurfaceSecondary = "#F4F5F5";
    private const string SurfaceRaised = "#FAFBFA";
    private const string SurfaceTertiary = "#ECEFEC";
    private const string SurfaceInverse = "#1E3322";
    private const string ForegroundPrimary = "#1E3322";
    private const string ForegroundSecondary = "#5F6F61";
    private const string ForegroundMuted = "#808A80";
    private const string ForegroundInverse = "#FFFFFF";
    private const string BorderSubtle = "#DDE3DD";
    private const string AccentPrimary = "#2D6B3F";
    private const string AccentBlue = "#2563EB";
    private const string StatusOnline = "#238636";
    private const string StatusOnlineBg = "#E8F4EA";
    private const string StatusWarning = "#B7791F";
    private const string StatusWarningBg = "#FFF6DF";
    private const string StatusError = "#C93C37";
    private const string StatusErrorBg = "#FDECEB";
    private const string CodeBg = "#17231A";
    private const string CodeBorder = "#2E4233";

    private static readonly FontFamily HeadingFont = new("Inter");
    private static readonly FontFamily BodyFont = new("Inter, Geist, -apple-system, BlinkMacSystemFont, Segoe UI");
    private static readonly FontFamily CaptionFont = new("Newsreader, Georgia");
    private static readonly FontFamily DataFont = new("IBM Plex Mono, Menlo, Consolas, monospace");

    private readonly Localizer _text = new(AppPreferencesStore.Load().Language);
    private ContentControl _pageHost = new();
    private TextBlock _lastUpdated = new();
    private CenterPage _selectedPage = CenterPage.Overview;
    private LogFilter _logFilter = LogFilter.All;
    private bool _settingsDirty = true;
    private string _settingsMessage = string.Empty;

    public MainWindow()
    {
        Title = _text.T("app.title");
        Width = 1280;
        Height = 820;
        MinWidth = 1120;
        MinHeight = 720;
        Background = Brush(SurfacePrimary);

        RuntimeHolder.Start();
        Content = BuildLayout();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Refresh(force: true);
    }

    private Control BuildLayout()
    {
        Title = _text.T("app.title");

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("232,*"),
            Background = Brush(SurfacePrimary)
        };

        root.Children.Add(BuildSidebar());

        _pageHost = new ContentControl();
        Grid.SetColumn(_pageHost, 1);
        root.Children.Add(_pageHost);

        RenderSelectedPage(force: true);
        return root;
    }

    private Control BuildSidebar()
    {
        var sidebar = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BuildIdentity(),
                BuildNavigation(),
                BuildRuntimeCard(),
                new Border { Height = double.NaN, VerticalAlignment = VerticalAlignment.Stretch },
                BuildBridgeStatusCard()
            }
        };

        return new Border
        {
            Width = 232,
            Padding = new Thickness(18, 14),
            Background = Brush(SurfaceSecondary),
            Child = sidebar
        };
    }

    private Control BuildIdentity()
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("34,*"),
            Height = 42,
            ColumnSpacing = 10,
            Children =
            {
                new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(8),
                    Background = Brush(SurfaceInverse),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = CenteredText("IDA", 10, FontWeight.SemiBold, ForegroundInverse, DataFont)
                },
                Stack(Orientation.Vertical, 1,
                    Text(_text.T("brand.title"), 15, FontWeight.Bold, ForegroundPrimary, HeadingFont),
                    Text(_text.T("brand.subtitle"), 12, FontWeight.Normal, ForegroundSecondary, CaptionFont))
            }
        }.WithColumn(1, childIndex: 1);
    }

    private Control BuildNavigation()
    {
        var panel = Stack(Orientation.Vertical, 3);
        panel.Children.Add(Text(_text.T("sidebar.navigation"), 12, FontWeight.Normal, ForegroundMuted, CaptionFont));
        foreach (var page in Enum.GetValues<CenterPage>())
        {
            panel.Children.Add(NavButton(page));
        }

        return panel;
    }

    private Control NavButton(CenterPage page)
    {
        var selected = _selectedPage == page;
        var foreground = selected ? ForegroundInverse : ForegroundSecondary;
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("15,*"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                LineIcon(NavGlyph(page), foreground).WithColumn(0),
                Text(PageTitle(page), 13, selected ? FontWeight.SemiBold : FontWeight.Normal, foreground)
                    .WithColumn(1)
            }
        };

        var normal = selected
            ? new ButtonPalette(Brush(SurfaceInverse), Brushes.Transparent, Brush(ForegroundInverse), ForegroundInverse, 8, 34, 13, new Thickness(9, 0))
            : new ButtonPalette(Brush(SurfacePrimary), Brushes.Transparent, Brush(ForegroundSecondary), ForegroundSecondary, 8, 34, 13, new Thickness(9, 0));
        var hover = selected
            ? normal
            : new ButtonPalette(Brush(SurfaceTertiary), Brushes.Transparent, Brush(ForegroundPrimary), ForegroundPrimary, 8, 34, 13, new Thickness(9, 0));

        return ClickableSurface(content, () =>
        {
            SelectPage(page);
            return Task.CompletedTask;
        }, normal, hover, hoverEnabled: !selected);
    }

    private Control BuildRuntimeCard()
    {
        var targets = RuntimeHolder.TargetRegistry.ListTargets();
        var healthy = targets.Count(target => target.Health == TargetHealth.Healthy);
        var status = RuntimeHolder.IsRunning
            ? _text.T("sidebar.localServiceOnline")
            : _text.T("sidebar.localServiceOffline");
        var meta = RuntimeHolder.IsRunning
            ? CompactEndpoint(RuntimeHolder.McpEndpoint)
            : _text.F("sidebar.windowHealth", healthy, targets.Count);

        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8),
            Background = Brush(SurfacePrimary),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 8,
                Children =
                {
                    Dot(RuntimeHolder.IsRunning ? StatusOnline : StatusError).WithColumn(0),
                    Stack(Orientation.Vertical, 1,
                        Text(status, 12, FontWeight.SemiBold, ForegroundPrimary),
                        Text(meta, 11, FontWeight.Normal, ForegroundMuted, DataFont))
                        .WithColumn(1)
                }
            }
        };
    }

    private Control BuildBridgeStatusCard()
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 8),
            Background = Brush(SurfacePrimary),
            Child = Stack(Orientation.Vertical, 6,
                Text(_text.T("sidebar.workspace"), 12, FontWeight.Normal, ForegroundMuted, CaptionFont),
                Text(ProductInfo.AgentServerName, 11, FontWeight.Normal, ForegroundPrimary, DataFont))
        };
    }

    private void SelectPage(CenterPage page)
    {
        if (_selectedPage == page)
        {
            return;
        }

        _selectedPage = page;
        Content = BuildLayout();
        Refresh(force: true);
    }

    private void Refresh(bool force = false)
    {
        _lastUpdated.Text = _text.F("updated", DateTime.Now.ToString("T", _text.Culture));
        if (_selectedPage == CenterPage.Settings)
        {
            if (_settingsDirty || force)
            {
                RenderSelectedPage(force: true);
                _settingsDirty = false;
            }

            return;
        }

        RenderSelectedPage(force);
    }

    private void RenderSelectedPage(bool force = false)
    {
        _pageHost.Content = _selectedPage switch
        {
            CenterPage.Overview => BuildOverviewPage(),
            CenterPage.Targets => BuildTargetsPage(),
            CenterPage.Activity => BuildActivityPage(),
            CenterPage.Processes => BuildProcessesPage(),
            CenterPage.Installations => BuildInstallationsPage(),
            CenterPage.Logs => BuildLogsPage(),
            CenterPage.Settings => BuildSettingsPage(),
            _ => BuildOverviewPage()
        };
    }

    private Control BuildOverviewPage()
    {
        var targets = RuntimeHolder.TargetRegistry.ListTargets()
            .OrderBy(target => target.BinaryName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var healthy = targets.Count(target => target.Health == TargetHealth.Healthy);
        var activeOps = RuntimeHolder.ActiveOperations.List().Count;
        var logs = RuntimeHolder.OperationLog.List(8).ToArray();
        var errors = logs.Count(log => !log.Success);

        var primaryColumn = Stack(Orientation.Vertical, 14,
            BuildServiceSummary(targets.Length, healthy, activeOps),
            BuildOverviewMetricGrid(targets.Length, healthy, RuntimeHolder.ToolCatalog.ListTools().Count, activeOps),
            BuildOverviewWarning(errors));

        var secondaryColumn = Stack(Orientation.Vertical, 14,
            BuildConnectedSummary(targets.Take(3).ToArray()),
            BuildRecentActivity(logs),
            BuildSmallEmptyState(_text.T("overview.emptyTitle"), _text.T("overview.emptyBody")));

        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("634,*"),
            ColumnSpacing = 14,
            Children =
            {
                primaryColumn.WithColumn(0),
                secondaryColumn.WithColumn(1)
            }
        };

        return PageScaffold(
            BuildToolbar(
                _text.T("page.overview.title"),
                _text.T("page.overview.subtitle"),
                ToolbarEndpointControls(includeSettings: false)),
            new Border
            {
                Padding = new Thickness(18, 14, 18, 18),
                Background = Brush(SurfacePrimary),
                Child = contentGrid
            });
    }

    private Control BuildServiceSummary(int targetCount, int healthy, int activeOps)
    {
        var statusText = RuntimeHolder.IsRunning ? _text.T("overview.serverOnline") : _text.T("overview.serverOffline");
        var badgeText = RuntimeHolder.IsRunning ? _text.T("status.normal") : _text.T("status.error");
        var restart = ActionButton(_text.T("button.restartBridge"), () =>
        {
            _settingsMessage = _text.T("action.restartBridgeHint");
            _settingsDirty = true;
            return Task.CompletedTask;
        }, ButtonKind.Green);

        return new Border
        {
            Height = 252,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18),
            Background = Brush(SurfaceInverse),
            Child = Stack(Orientation.Vertical, 14,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        Stack(Orientation.Vertical, 4,
                            Text(_text.T("overview.serviceStatus"), 12, FontWeight.Normal, "#FFFFFFB8", CaptionFont),
                            Text(statusText, 28, FontWeight.Bold, ForegroundInverse, HeadingFont),
                            Text(_text.T("overview.serviceBody"), 13, FontWeight.Normal, "#DDE3DD").Wrap())
                            .WithColumn(0),
                        StatusPill(badgeText, RuntimeHolder.IsRunning).WithColumn(1)
                    }
                },
                SummaryMetricRow(activeOps),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        Stack(Orientation.Vertical, 2,
                            Text(_text.T("overview.activeEndpoint"), 12, FontWeight.Normal, "#FFFFFFA8", CaptionFont),
                            Text(RuntimeHolder.McpEndpoint, 12, FontWeight.Normal, ForegroundInverse, DataFont))
                            .WithColumn(0),
                        restart.WithColumn(1)
                    }
                })
        };
    }

    private Control BuildOverviewMetricGrid(int targets, int healthy, int tools, int activeOps)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowSpacing = 10,
            ColumnSpacing = 10
        };

        grid.Children.Add(OverviewMetric(_text.T("overview.metricMcp"), RuntimeHolder.IsRunning ? _text.T("status.online") : _text.T("status.offline"), _text.T("overview.metricMcpBody"), RowKind.Success).At(0, 0));
        grid.Children.Add(OverviewMetric(_text.T("overview.metricBridge"), _text.F("overview.windowsCount", healthy), _text.F("overview.lastHeartbeat", BestHeartbeat()), RowKind.Success).At(0, 1));
        grid.Children.Add(OverviewMetric(_text.T("overview.metricTools"), tools.ToString(), _text.T("overview.metricToolsBody"), RowKind.Warning).At(1, 0));
        grid.Children.Add(OverviewMetric(_text.T("overview.metricCalls"), activeOps == 0 ? _text.T("status.idle") : activeOps.ToString(), _text.T("overview.metricCallsBody"), RowKind.Neutral).At(1, 1));
        return grid;
    }

    private Control StatusPill(string text, bool healthy)
    {
        return new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            Background = Brush(healthy ? "#345237" : StatusErrorBg),
            VerticalAlignment = VerticalAlignment.Top,
            Child = Stack(Orientation.Horizontal, 7,
                Dot(healthy ? StatusOnline : StatusError),
                Text(text.ToUpperInvariant(), 11, FontWeight.SemiBold, healthy ? ForegroundInverse : StatusError, DataFont))
        };
    }

    private Control SummaryMetricRow(int activeOps)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            ColumnSpacing = 10,
            Height = 78,
            Children =
            {
                SummaryPill(_text.T("overview.port"), DesktopSettings.Current.McpPort.ToString()).WithColumn(0),
                SummaryPill(_text.T("overview.latency"), _text.T("overview.localLatency")).WithColumn(1),
                SummaryPill(_text.T("overview.queue"), activeOps.ToString()).WithColumn(2)
            }
        };

        return grid;
    }

    private Control BuildOverviewWarning(int errorCount)
    {
        var hasErrors = errorCount > 0;
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Horizontal, 12,
                BadgeIcon(hasErrors ? "!" : "i", hasErrors ? RowKind.Warning : RowKind.Info),
                Stack(Orientation.Vertical, 6,
                    Text(hasErrors ? _text.T("overview.errorTitle") : _text.T("overview.readyTitle"), 14, FontWeight.Bold, ForegroundPrimary, HeadingFont),
                    Text(hasErrors ? _text.F("overview.errorBody", errorCount) : _text.T("overview.readyBody"), 12, FontWeight.Normal, ForegroundSecondary)
                        .Wrap(),
                    ActionButton(_text.T("button.openTrace"), () =>
                    {
                        SelectPage(CenterPage.Logs);
                        return Task.CompletedTask;
                    }, ButtonKind.DarkSmall)))
        };
    }

    private Control BuildConnectedSummary(IReadOnlyCollection<TargetInfo> targets)
    {
        var panel = Stack(Orientation.Vertical, 12,
            HeaderRow(_text.T("overview.connectedTitle"), _text.T("status.active")));

        if (targets.Count == 0)
        {
            panel.Children.Add(Text(_text.T("empty.targets"), 12, FontWeight.Normal, ForegroundSecondary).Wrap());
        }
        else
        {
            foreach (var target in targets)
            {
                panel.Children.Add(TargetSummaryLine(target));
            }
        }

        return Card(panel, height: 220);
    }

    private Control TargetSummaryLine(TargetInfo target)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                Stack(Orientation.Vertical, 3,
                    Text(TargetTitle(target), 12, FontWeight.SemiBold, ForegroundPrimary, DataFont).Ellipsis(),
                    Text(CompactPath(target.InputPath ?? target.DatabasePath), 11, FontWeight.Normal, ForegroundMuted).Ellipsis())
                    .WithColumn(0),
                HealthBadge(target.Health).WithColumn(1)
            }
        };
    }

    private Control BuildRecentActivity(IReadOnlyCollection<OperationLogEntry> logs)
    {
        var panel = Stack(Orientation.Vertical, 10,
            HeaderRow(_text.T("overview.recentTitle"), _text.T("status.recent")));

        if (logs.Count == 0)
        {
            panel.Children.Add(Text(_text.T("empty.logs"), 12, FontWeight.Normal, ForegroundSecondary));
        }
        else
        {
            foreach (var log in logs.Take(5))
            {
                panel.Children.Add(LogMiniRow(log));
            }
        }

        return Card(panel);
    }

    private Control LogMiniRow(OperationLogEntry log)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("58,42,*"),
            ColumnSpacing = 8,
            Children =
            {
                Text(log.TimestampUtc.LocalDateTime.ToString("HH:mm:ss", _text.Culture), 11, FontWeight.Normal, ForegroundMuted, DataFont).WithColumn(0),
                Text(log.Success ? "OK" : "ERR", 11, FontWeight.SemiBold, log.Success ? StatusOnline : StatusError, DataFont).WithColumn(1),
                Text($"{log.TargetAlias} / {log.ToolName}", 11, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis().WithColumn(2)
            }
        };
    }

    private Control BuildSmallEmptyState(string title, string body)
    {
        return Card(
            Stack(Orientation.Horizontal, 12,
                BadgeIcon("-", RowKind.Neutral),
                Stack(Orientation.Vertical, 4,
                    Text(title, 13, FontWeight.SemiBold, ForegroundPrimary),
                    Text(body, 11, FontWeight.Normal, ForegroundSecondary).Wrap())),
            height: 96);
    }

    private Control BuildTargetsPage()
    {
        var targets = RuntimeHolder.TargetRegistry.ListTargets()
            .OrderBy(target => target.Health == TargetHealth.Healthy ? 0 : 1)
            .ThenBy(target => TargetTitle(target), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var activeOps = RuntimeHolder.ActiveOperations.List().ToArray();
        var healthy = targets.Count(target => target.Health == TargetHealth.Healthy);
        var warning = targets.Count(target => target.Health is TargetHealth.Unreachable or TargetHealth.Unknown);

        var content = Stack(Orientation.Vertical, 14,
            BuildTargetsToolbar(),
            BuildTargetsMetrics(targets.Length, healthy, warning, activeOps.FirstOrDefault()?.TargetAlias),
            BuildTargetsTable(targets, activeOps));

        return new Border
        {
            Padding = new Thickness(20),
            Background = Brush(SurfacePrimary),
            Child = content
        };
    }

    private Control BuildTargetsToolbar()
    {
        return new Border
        {
            Height = 74,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 16),
            Background = Brush(SurfaceSecondary),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    Stack(Orientation.Vertical, 3,
                        Text(_text.T("page.targets.title"), 26, FontWeight.Bold, ForegroundPrimary, HeadingFont),
                        Text(_text.T("page.targets.subtitle"), 13, FontWeight.Normal, ForegroundSecondary))
                        .WithColumn(0),
                    Stack(Orientation.Horizontal, 8,
                        SearchPlaceholder(_text.T("search.targets"), 260),
                        SegmentedStatusFilters(),
                        ActionButton(_text.T("button.refreshMetadata"), RefreshAllMetadataAsync, ButtonKind.Dark))
                        .WithColumn(1)
                }
            }
        };
    }

    private Control BuildTargetsMetrics(int total, int healthy, int warning, string? activeAlias)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,1.15*"),
            ColumnSpacing = 12,
            Height = 92,
            Children =
            {
                MetricCard(_text.T("targets.metric.connected"), total.ToString(), ForegroundPrimary).WithColumn(0),
                MetricCard(_text.T("targets.metric.online"), healthy.ToString(), StatusOnline).WithColumn(1),
                MetricCard(_text.T("targets.metric.timeouts"), warning.ToString(), StatusWarning).WithColumn(2),
                MetricCard(_text.T("targets.metric.activeAgent"), string.IsNullOrWhiteSpace(activeAlias) ? _text.T("status.idle") : activeAlias, ForegroundInverse, dark: true).WithColumn(3)
            }
        };
        return grid;
    }

    private Control BuildTargetsTable(IReadOnlyCollection<TargetInfo> targets, IReadOnlyCollection<ActiveOperation> activeOps)
    {
        var body = Stack(Orientation.Vertical, 6);
        if (targets.Count == 0)
        {
            body.Children.Add(EmptyState(_text.T("empty.targets"), _text.T("empty.targetsBody")));
        }
        else
        {
            foreach (var target in targets)
            {
                body.Children.Add(TargetTableRow(target, activeOps.Any(op => op.TargetInstanceId == target.InstanceId)));
            }
        }

        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Vertical, 8,
                HeaderRow(_text.T("targets.table.title"), _text.F("targets.table.summary", targets.Count)),
                TargetTableHeader(),
                body)
        };
    }

    private Control TargetTableHeader()
    {
        var grid = TargetGrid();
        AddTargetCell(grid, _text.T("table.file"), 0, isHeader: true);
        AddTargetCell(grid, _text.T("table.alias"), 1, isHeader: true);
        AddTargetCell(grid, "PID", 2, isHeader: true);
        AddTargetCell(grid, _text.T("table.platform"), 3, isHeader: true);
        AddTargetCell(grid, _text.T("table.path"), 4, isHeader: true);
        AddTargetCell(grid, _text.T("table.idb"), 5, isHeader: true);
        AddTargetCell(grid, _text.T("table.heartbeat"), 6, isHeader: true);
        AddTargetCell(grid, _text.T("table.health"), 7, isHeader: true);
        AddTargetCell(grid, _text.T("table.actions"), 8, isHeader: true);

        return new Border
        {
            Height = 28,
            Padding = new Thickness(8, 0),
            Child = grid
        };
    }

    private Control TargetTableRow(TargetInfo target, bool active)
    {
        var grid = TargetGrid();
        AddTargetCell(grid, TargetTitle(target), 0, strong: true, font: BodyFont, size: 12);
        AddTargetCell(grid, target.Alias, 1, font: DataFont, color: ForegroundSecondary, size: 10.5);
        AddTargetCell(grid, target.ProcessId.ToString(), 2, font: DataFont, size: 10.5);
        AddTargetCell(grid, CompactPlatform(target), 3, font: DataFont, size: 10.5);
        AddTargetCell(grid, CompactPath(target.InputPath), 4, font: DataFont, color: ForegroundSecondary, tooltip: target.InputPath, size: 9.3);
        AddTargetCell(grid, CompactPath(target.DatabasePath), 5, font: DataFont, color: ForegroundSecondary, tooltip: target.DatabasePath, size: 9.3);
        AddTargetCell(grid, LastSeen(target.LastSeenUtc), 6, font: DataFont, color: target.Health == TargetHealth.Healthy ? ForegroundPrimary : StatusWarning, size: 10.5);
        grid.Children.Add(HealthBadge(target.Health).WithColumn(7));
        grid.Children.Add(TargetActions(target).WithColumn(8));

        return new Border
        {
            MinHeight = 62,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 10),
            Background = Brush(active ? SurfaceRaised : SurfacePrimary),
            BorderBrush = Brush(active ? AccentPrimary : SurfacePrimary),
            BorderThickness = active ? new Thickness(1) : new Thickness(0),
            Child = grid
        }.WithHover(Brush(SurfaceRaised), Brush(active ? AccentPrimary : BorderSubtle));
    }

    private Grid TargetGrid()
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("118,82,50,62,*,*,68,72,192"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void AddTargetCell(
        Grid grid,
        string text,
        int column,
        bool isHeader = false,
        bool strong = false,
        FontFamily? font = null,
        string? color = null,
        string? tooltip = null,
        double? size = null)
    {
        var block = Text(
            string.IsNullOrWhiteSpace(text) ? "-" : text,
            size ?? (isHeader ? 12 : 11),
            isHeader || strong ? FontWeight.SemiBold : FontWeight.Normal,
            color ?? (isHeader ? ForegroundMuted : ForegroundPrimary),
            font ?? (isHeader ? CaptionFont : BodyFont))
            .Ellipsis();

        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            ToolTip.SetTip(block, tooltip);
        }

        grid.Children.Add(block.WithColumn(column));
    }

    private Control TargetActions(TargetInfo target)
    {
        var actions = Stack(Orientation.Horizontal, 4,
            TargetActionButton(_text.T("button.ping"), 34, () => CallTargetToolAsync("ida_ping_target", target.InstanceId, _text.T("action.pingSent"))),
            TargetActionButton(_text.T("button.refreshShort"), 58, () => CallTargetToolAsync("ida_get_metadata", target.InstanceId, _text.T("action.metadataRefreshed"))),
            TargetActionButton(_text.T("button.openShort"), 44, () => OpenTargetInIdaAsync(target), enabled: !string.IsNullOrWhiteSpace(target.InputPath)),
            TargetActionButton(_text.T("button.close"), 44, () => CallTargetCloseAsync(target), kind: ButtonKind.DangerMini));
        actions.VerticalAlignment = VerticalAlignment.Center;
        actions.HorizontalAlignment = HorizontalAlignment.Left;
        return actions;
    }

    private Control BuildActivityPage()
    {
        var operations = RuntimeHolder.ActiveOperations.List()
            .OrderBy(operation => operation.StartedAtUtc)
            .ToArray();

        var content = operations.Length == 0
            ? EmptyState(_text.T("empty.activity"), _text.T("empty.activityBody"))
            : ActiveOperationsTable(operations);

        return PageScaffold(
            BuildToolbar(
                _text.T("page.activity.title"),
                _text.T("page.activity.subtitle"),
                ToolbarEndpointControls(includeSettings: false)),
            PagePadding(content));
    }

    private Control ActiveOperationsTable(IReadOnlyCollection<ActiveOperation> operations)
    {
        var rows = Stack(Orientation.Vertical, 6);
        foreach (var operation in operations)
        {
            rows.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Brush(SurfacePrimary),
                Padding = new Thickness(12),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.2*,1.4*,1*,0.8*"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        Text(operation.TargetAlias, 12, FontWeight.SemiBold, ForegroundPrimary, DataFont).Ellipsis().WithColumn(0),
                        Text(operation.ToolName, 12, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis().WithColumn(1),
                        Text(operation.StartedAtUtc.LocalDateTime.ToString("HH:mm:ss", _text.Culture), 12, FontWeight.Normal, ForegroundSecondary, DataFont).WithColumn(2),
                        Chip(_text.T("status.running"), RowKind.Info).WithColumn(3)
                    }
                }
            });
        }

        return PanelCard(_text.T("activity.table.title"), _text.F("activity.table.summary", operations.Count), rows);
    }

    private Control BuildProcessesPage()
    {
        var processes = RuntimeHolder.IdaLaunchService.ListLaunchedProcesses()
            .OrderByDescending(process => process.LaunchedAtUtc)
            .ToArray();

        var body = Stack(Orientation.Vertical, 6);
        if (processes.Length == 0)
        {
            body.Children.Add(EmptyState(_text.T("empty.processes"), _text.T("empty.processesBody")));
        }
        else
        {
            foreach (var process in processes)
            {
                body.Children.Add(ProcessRow(process));
            }
        }

        return PageScaffold(
            BuildToolbar(
                _text.T("page.processes.title"),
                _text.T("page.processes.subtitle"),
                ToolbarEndpointControls(includeSettings: false)),
            PagePadding(PanelCard(_text.T("processes.table.title"), _text.F("processes.table.summary", processes.Length), body)));
    }

    private Control ProcessRow(LaunchedIdaProcess process)
    {
        var hasExited = process.HasExited;
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brush(SurfacePrimary),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("80,1.2*,1.2*,110,110"),
                ColumnSpacing = 12,
                Children =
                {
                    Text($"PID {process.ProcessId}", 12, FontWeight.SemiBold, ForegroundPrimary, DataFont).WithColumn(0),
                    Text(CompactPath(process.InputPath), 12, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis().WithColumn(1),
                    Text(CompactPath(process.ExecutablePath), 12, FontWeight.Normal, ForegroundSecondary, DataFont).Ellipsis().WithColumn(2),
                    Text(process.LaunchedAtUtc.LocalDateTime.ToString("HH:mm:ss", _text.Culture), 12, FontWeight.Normal, ForegroundMuted, DataFont).WithColumn(3),
                    Chip(hasExited ? _text.T("status.exited") : _text.T("status.running"), hasExited ? RowKind.Neutral : RowKind.Success).WithColumn(4)
                }
            }
        };
    }

    private Control BuildInstallationsPage()
    {
        var installations = RuntimeHolder.IdaLocator.FindInstallations().ToArray();
        var body = Stack(Orientation.Vertical, 6);
        if (installations.Length == 0)
        {
            body.Children.Add(EmptyState(_text.T("empty.installations"), _text.T("empty.installationsBody")));
        }
        else
        {
            foreach (var install in installations)
            {
                body.Children.Add(InstallRow(install));
            }
        }

        return PageScaffold(
            BuildToolbar(
                _text.T("page.installations.title"),
                _text.T("page.installations.subtitle"),
                ToolbarEndpointControls(includeSettings: false)),
            PagePadding(PanelCard(_text.T("installations.table.title"), _text.F("installations.table.summary", installations.Count(i => i.Exists), installations.Length), body)));
    }

    private Control InstallRow(IdaInstall install)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brush(SurfacePrimary),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1*,1.6*,1*,110"),
                ColumnSpacing = 12,
                Children =
                {
                    Text(install.DisplayName, 12, FontWeight.SemiBold, ForegroundPrimary, DataFont).Ellipsis().WithColumn(0),
                    Text(CompactPath(install.Path), 12, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis().WithColumn(1),
                    Text(install.Source, 12, FontWeight.Normal, ForegroundSecondary).Ellipsis().WithColumn(2),
                    Chip(install.Exists ? _text.T("status.exists") : _text.T("status.missing"), install.Exists ? RowKind.Success : RowKind.Warning).WithColumn(3)
                }
            }
        };
    }

    private Control BuildLogsPage()
    {
        var logs = RuntimeHolder.OperationLog.List(200)
            .Where(MatchesLogFilter)
            .ToArray();
        var latestError = RuntimeHolder.OperationLog.List(200).FirstOrDefault(log => !log.Success);
        var body = logs.Length == 0
            ? EmptyState(_text.T("empty.logs"), _text.T("empty.logsBody"))
            : BuildLogRows(logs);

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,300"),
            ColumnSpacing = 14,
            Children =
            {
                PanelCard(_text.T("logs.stream.title"), _text.T("logs.stream.summary"), body).WithColumn(0),
                BuildLogSidePanel(latestError).WithColumn(1)
            }
        };

        return new Border
        {
            Padding = new Thickness(16),
            Background = Brush(SurfacePrimary),
            Child = Stack(Orientation.Vertical, 14, BuildLogToolbar(), content)
        };
    }

    private Control BuildLogToolbar()
    {
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Vertical, 12,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        Stack(Orientation.Vertical, 2,
                            Text(_text.T("page.logs.title"), 24, FontWeight.Bold, ForegroundPrimary, HeadingFont),
                            Text(_text.T("page.logs.subtitle"), 13, FontWeight.Normal, ForegroundSecondary))
                            .WithColumn(0),
                        Stack(Orientation.Horizontal, 8,
                            SearchPlaceholder(_text.T("search.logs"), 276, height: 36, radius: 12, fontSize: 13, showIcon: true),
                            ActionButton(_text.T("button.copy"), CopyLogsAsync, ButtonKind.ActionDark),
                            ActionButton(_text.T("button.clear"), () =>
                            {
                                _settingsMessage = _text.T("action.clearLogUnavailable");
                                return Task.CompletedTask;
                            }, ButtonKind.Clear))
                            .WithColumn(1)
                    }
                },
                BuildLogFilters())
        };
    }

    private Control BuildLogFilters()
    {
        return Stack(Orientation.Horizontal, 8,
            Text(_text.T("logs.filters"), 13, FontWeight.Normal, ForegroundMuted, CaptionFont),
            LogFilterButton(LogFilter.All, "All"),
            LogFilterButton(LogFilter.Mcp, "MCP"),
            LogFilterButton(LogFilter.IdaTcp, "IDA TCP"),
            LogFilterButton(LogFilter.Plugin, "Plugin"),
            LogFilterButton(LogFilter.Agent, "Agent"),
            LogFilterButton(LogFilter.Error, "Error"));
    }

    private Control LogFilterButton(LogFilter filter, string label)
    {
        var selected = _logFilter == filter;
        var kind = filter == LogFilter.Error ? ButtonKind.DangerMini : selected ? ButtonKind.FilterSelected : ButtonKind.Filter;
        return ActionButton(label, () =>
        {
            _logFilter = filter;
            RenderSelectedPage(force: true);
            return Task.CompletedTask;
        }, kind);
    }

    private Control BuildLogRows(IReadOnlyCollection<OperationLogEntry> logs)
    {
        var rows = Stack(Orientation.Vertical, 6);
        rows.Children.Add(LogHeaderRow());
        foreach (var log in logs.Take(120))
        {
            rows.Children.Add(LogRow(log));
        }

        return rows;
    }

    private Control LogHeaderRow()
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = Brush(SurfaceTertiary),
            Padding = new Thickness(10, 8),
            Child = LogGrid(
                Text("Time", 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont),
                Text("Level", 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont),
                Text("Source", 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont),
                Text("Target", 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont),
                Text("Message", 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont))
        };
    }

    private Control LogRow(OperationLogEntry log)
    {
        var success = log.Success;
        var row = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = Brush(success ? SurfacePrimary : StatusErrorBg),
            BorderBrush = Brush(success ? SurfacePrimary : StatusError),
            BorderThickness = success ? new Thickness(0) : new Thickness(1),
            Padding = new Thickness(10, 8),
            Child = LogGrid(
                Text(log.TimestampUtc.LocalDateTime.ToString("HH:mm:ss", _text.Culture), 11, FontWeight.Normal, ForegroundMuted, DataFont),
                Text(success ? "INFO" : "ERROR", 11, FontWeight.SemiBold, success ? ForegroundSecondary : StatusError, DataFont),
                Text(LogSource(log), 11, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis(),
                Text(log.TargetAlias, 11, FontWeight.Normal, ForegroundPrimary, DataFont).Ellipsis(),
                Text(success ? $"{log.ToolName} completed in {log.Elapsed.TotalMilliseconds:0} ms" : log.Error ?? log.ToolName, 11, FontWeight.Normal, success ? ForegroundPrimary : StatusError, DataFont).Ellipsis())
        };

        return row;
    }

    private Grid LogGrid(params Control[] children)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("72,58,86,130,*"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        for (var i = 0; i < children.Length; i++)
        {
            grid.Children.Add(children[i].WithColumn(i));
        }

        return grid;
    }

    private Control BuildLogSidePanel(OperationLogEntry? latestError)
    {
        var errorTile = latestError is null
            ? StateTile(_text.T("logs.noErrorTitle"), _text.T("logs.noErrorBody"), RowKind.Success)
            : StateTile(_text.T("logs.latestError"), latestError.Error ?? latestError.ToolName, RowKind.Danger);

        var logs = RuntimeHolder.OperationLog.List(200).ToArray();
        return Stack(Orientation.Vertical, 14,
            errorTile,
            StateTile(_text.T("logs.emptyStateTitle"), _text.T("logs.emptyStateBody"), RowKind.Neutral),
            new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12),
                Background = Brush(SurfaceInverse),
                Child = Stack(Orientation.Vertical, 8,
                    Text(_text.T("logs.sessionSummary"), 12, FontWeight.Normal, "#B6C4B8", CaptionFont),
                    Text(logs.Length.ToString(), 28, FontWeight.Bold, ForegroundInverse, DataFont),
                    SummaryBar(_text.T("logs.errors"), logs.Count(log => !log.Success), logs.Length, StatusError),
                    SummaryBar(_text.T("logs.warnings"), 0, Math.Max(logs.Length, 1), StatusWarning))
            });
    }

    private Control BuildSettingsPage()
    {
        var plugin = RuntimeHolder.PluginInstallService.GetStatus();
        var agents = RuntimeHolder.AgentConfigService.Detect().ToArray();
        var warning = plugin.IsCompatible && plugin.Warnings.Count == 0
            ? null
            : PluginWarningBanner(plugin);

        var body = Stack(Orientation.Vertical, 10);
        if (warning is not null)
        {
            body.Children.Add(warning);
        }

        body.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.85*,1.25*"),
            ColumnSpacing = 14,
            Children =
            {
                LanguageSettingsCard().WithColumn(0),
                PluginSettingsCard(plugin).WithColumn(1)
            }
        });

        body.Children.Add(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.05*,0.95*"),
            ColumnSpacing = 14,
            Children =
            {
                AgentSettingsCard(agents).WithColumn(0),
                AdvancedSettingsCard().WithColumn(1)
            }
        });

        body.Children.Add(ManualConfigurationCard());

        if (!string.IsNullOrWhiteSpace(_settingsMessage))
        {
            body.Children.Add(StateTile(_text.T("lastAction"), _settingsMessage, RowKind.Info));
        }

        return PageScaffold(
            BuildToolbar(
                _text.T("page.settings.title"),
                _text.F("page.settings.subtitle", PluginSummary(plugin), agents.Count(agent => agent.IsConfigured), DateTime.Now.ToString("T", _text.Culture)),
                ToolbarEndpointControls(includeSettings: true)),
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,12"),
                Background = Brush(SurfacePrimary),
                Children =
                {
                    new ScrollViewer
                    {
                        Content = body,
                        Padding = new Thickness(14, 18, 18, 28),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    }.WithColumn(0),
                    new Border
                    {
                        Background = Brush(SurfacePrimary),
                        Padding = new Thickness(3, 0),
                        Child = new Border
                        {
                            Width = 6,
                            Height = 116,
                            CornerRadius = new CornerRadius(8),
                            Background = Brush(BorderSubtle),
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, 72, 0, 0)
                        }
                    }.WithColumn(1)
                }
            });
    }

    private Control PluginWarningBanner(PluginInstallStatus plugin)
    {
        return new Border
        {
            Height = 46,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Background = Brush(StatusWarningBg),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10,
                Children =
                {
                    BadgeIcon("!", RowKind.Warning).WithColumn(0),
                    Stack(Orientation.Vertical, 2,
                        Text(_text.T("settings.pluginWarningTitle"), 12, FontWeight.SemiBold, ForegroundPrimary),
                        Text(LocalizePluginMessage(plugin), 10, FontWeight.Normal, ForegroundSecondary).Ellipsis())
                        .WithColumn(1),
                    ActionButton(_text.T("button.verify"), () =>
                    {
                        _settingsDirty = true;
                        return Task.CompletedTask;
                    }, ButtonKind.Light).WithColumn(2)
                }
            }
        };
    }

    private Control LanguageSettingsCard()
    {
        return SectionCard(Stack(Orientation.Vertical, 12,
            SettingsSectionHeader(_text.T("settings.language"), _text.T("settings.languageMeta")),
            new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = Brush(SurfaceTertiary),
                Padding = new Thickness(4),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    ColumnSpacing = 4,
                    Children =
                    {
                        LanguageButton(AppLanguage.English).WithColumn(0),
                        LanguageButton(AppLanguage.Chinese).WithColumn(1)
                    }
                }
            }));
    }

    private Control PluginSettingsCard(PluginInstallStatus plugin)
    {
        var actions = Stack(Orientation.Horizontal, 6,
            ActionButton(plugin.IsCompatible ? _text.T("button.reinstall") : _text.T("button.install"), () => RunSettingsActionAsync(() => RuntimeHolder.PluginInstallService.InstallOrRepair()), ButtonKind.Primary),
            ActionButton(_text.T("button.uninstall"), () => RunSettingsActionAsync(() => RuntimeHolder.PluginInstallService.Uninstall()), ButtonKind.Secondary),
            ActionButton(_text.T("button.verify"), () =>
            {
                _settingsDirty = true;
                RenderSelectedPage(force: true);
                return Task.CompletedTask;
            }, ButtonKind.Secondary));

        var panel = Stack(Orientation.Vertical, 10,
            SettingsSectionHeader(_text.T("settings.idaPlugin"), _text.T("settings.pluginMeta")),
            SettingLine(_text.T("settings.installStatus"), PluginTitle(plugin), plugin.IsCompatible ? RowKind.Success : RowKind.Warning),
            SettingLine(_text.T("settings.pluginVersion"), _text.F("settings.pluginVersionValue", plugin.InstalledVersion ?? _text.T("none"), plugin.ExpectedVersion), plugin.IsCompatible ? RowKind.Success : RowKind.Warning),
            SettingPath(_text.T("settings.installPath"), plugin.LoaderPath),
            actions);

        if (plugin.Warnings.Count > 0)
        {
            panel.Children.Add(ActionButton(_text.T("button.archiveLegacy"), () => RunSettingsActionAsync(() => RuntimeHolder.PluginInstallService.ArchiveLegacyPlugins()), ButtonKind.Warning));
        }

        return SectionCard(panel);
    }

    private Control AgentSettingsCard(IReadOnlyCollection<AgentConfigStatus> agents)
    {
        var panel = Stack(Orientation.Vertical, 10,
            SettingsSectionHeader(_text.T("settings.mcpClients"), _text.F("settings.clientsMeta", agents.Count(agent => agent.IsConfigured))));

        foreach (var agent in agents)
        {
            var configure = ActionButton(
                agent.IsConfigured ? _text.T("button.edit") : _text.T("button.configure"),
                () => RunAgentConfigureAsync(agent),
                agent.IsConfigured ? ButtonKind.Secondary : ButtonKind.Primary,
                agent.CanAutoConfigure);

            panel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8),
                Background = Brush(SurfacePrimary),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    ColumnSpacing = 10,
                    Children =
                    {
                        Stack(Orientation.Vertical, 4,
                            Text(agent.AgentName, 12, FontWeight.SemiBold, ForegroundPrimary),
                            Text(AgentSummary(agent), 10, FontWeight.Normal, agent.IsConfigured ? StatusOnline : agent.HasLegacyConfig ? StatusWarning : ForegroundMuted).Wrap())
                            .WithColumn(0),
                        configure.WithColumn(1)
                    }
                }
            });
        }

        return SectionCard(panel);
    }

    private Control AdvancedSettingsCard()
    {
        return SectionCard(Stack(Orientation.Vertical, 8,
            SettingsSectionHeader(_text.T("settings.advanced"), _text.T("settings.runtimeSettings")),
            SettingLine(_text.T("settings.mcpPort"), DesktopSettings.Current.McpPort.ToString(), RowKind.Neutral),
            SettingLine(_text.T("settings.tcpPort"), DesktopSettings.Current.TcpPort.ToString(), RowKind.Neutral),
            SettingLine(_text.T("settings.logLevel"), "Info", RowKind.Neutral),
            SettingLine(_text.T("settings.launchLogin"), _text.T("status.enabled"), RowKind.Success),
            SettingLine(_text.T("settings.startMinimized"), _text.T("status.disabled"), RowKind.Neutral)));
    }

    private Control ManualConfigurationCard()
    {
        var httpText = _text.F("manual.http", ProductInfo.AgentServerName, RuntimeHolder.McpEndpoint);
        var stdioText = ManualStdioSnippet();

        return SectionCard(Stack(Orientation.Vertical, 10,
            SettingsSectionHeader(_text.T("section.manual"), _text.T("settings.manualMeta")),
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                ColumnSpacing = 8,
                Children =
                {
                    CodeBlock("HTTP", httpText).WithColumn(0),
                    CodeBlock("stdio", stdioText).WithColumn(1)
                }
            }));
    }

    private Control LanguageButton(AppLanguage language)
    {
        var selected = _text.Language == language;
        var normal = new ButtonPalette(
            selected ? Brush(SurfacePrimary) : Brushes.Transparent,
            Brushes.Transparent,
            Brush(selected ? ForegroundPrimary : ForegroundSecondary),
            selected ? ForegroundPrimary : ForegroundSecondary,
            6,
            28,
            11,
            new Thickness(0));
        var hover = selected
            ? normal
            : normal with { Background = Brush(SurfacePrimary), ForegroundColor = ForegroundPrimary, Foreground = Brush(ForegroundPrimary) };
        return ClickableSurface(
            CenteredText(language == AppLanguage.Chinese ? "中文" : "English", 11, selected ? FontWeight.SemiBold : FontWeight.Normal, normal.ForegroundColor),
            () =>
            {
                SetLanguage(language);
                return Task.CompletedTask;
            },
            normal,
            hover,
            hoverEnabled: !selected);
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
        Content = BuildLayout();
        Refresh(force: true);
    }

    private Control BuildToolbar(string title, string subtitle, Control controls)
    {
        return new Border
        {
            Height = 58,
            Padding = new Thickness(14, 18),
            Background = Brush(SurfacePrimary),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    Stack(Orientation.Vertical, 2,
                        Text(title, 18, FontWeight.SemiBold, ForegroundPrimary, HeadingFont),
                        Text(subtitle, 12, FontWeight.Normal, ForegroundSecondary))
                        .WithColumn(0),
                    controls.WithColumn(1)
                }
            }
        };
    }

    private Control ToolbarEndpointControls(bool includeSettings)
    {
        if (!includeSettings)
        {
            return Stack(Orientation.Horizontal, 8,
                EnvironmentChip(),
                CompactEndpointChip(),
                RefreshButton(dark: true));
        }

        var controls = Stack(Orientation.Horizontal, 8,
            EndpointChip("MCP", RuntimeHolder.McpEndpoint),
            EndpointChip("IDA TCP", RuntimeHolder.TcpEndpoint),
            LanguageMiniSwitch(),
            RefreshButton(dark: false));

        if (includeSettings)
        {
            controls.Children.Add(IconButton("sliders-horizontal", () =>
            {
                _settingsDirty = true;
                RenderSelectedPage(force: true);
                return Task.CompletedTask;
            }));
        }

        return controls;
    }

    private Control EnvironmentChip()
    {
        return new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Horizontal, 7,
                Dot(StatusOnline),
                Text("dev", 11, FontWeight.SemiBold, ForegroundPrimary, DataFont))
        };
    }

    private Control CompactEndpointChip()
    {
        return new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            Background = Brush(SurfaceSecondary),
            Child = Text(CompactEndpoint(RuntimeHolder.McpEndpoint), 11, FontWeight.Normal, ForegroundSecondary, DataFont)
        };
    }

    private Control LanguageMiniSwitch()
    {
        return new Border
        {
            Width = 54,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Horizontal, 3,
                MiniLangButton("EN", AppLanguage.English),
                MiniLangButton("中", AppLanguage.Chinese))
        };
    }

    private Control MiniLangButton(string label, AppLanguage language)
    {
        var selected = _text.Language == language;
        var normal = new ButtonPalette(
            selected ? Brush(SurfacePrimary) : Brushes.Transparent,
            Brushes.Transparent,
            Brush(selected ? ForegroundPrimary : ForegroundMuted),
            selected ? ForegroundPrimary : ForegroundMuted,
            6,
            26,
            10,
            new Thickness(0));
        var button = ClickableSurface(
            CenteredText(label, 10, selected ? FontWeight.SemiBold : FontWeight.Normal, normal.ForegroundColor, DataFont),
            () =>
            {
                SetLanguage(language);
                return Task.CompletedTask;
            },
            normal,
            normal,
            hoverEnabled: false);
        button.Width = 25;
        button.Margin = new Thickness(2, 2, 0, 2);
        return button;
    }

    private Control RefreshButton(bool dark)
    {
        return IconButton("refresh-cw", () =>
        {
            _settingsDirty = true;
            Refresh(force: true);
            return Task.CompletedTask;
        }, dark ? ButtonKind.DarkSquare : ButtonKind.Square);
    }

    private Control PageScaffold(Control toolbar, Control content)
    {
        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = Brush(SurfacePrimary),
            Children =
            {
                toolbar.WithRow(0),
                content.WithRow(1)
            }
        };
    }

    private Control PagePadding(Control content)
    {
        return new Border
        {
            Padding = new Thickness(20),
            Background = Brush(SurfacePrimary),
            Child = content
        };
    }

    private Control PanelCard(string title, string summary, Control body)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Vertical, 8,
                HeaderRow(title, summary),
                body)
        };
    }

    private static Control Card(Control content, double? height = null)
    {
        return new Border
        {
            Height = height ?? double.NaN,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Background = Brush(SurfaceSecondary),
            Child = content
        };
    }

    private static Border SectionCard(Control content, Thickness? padding = null)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = padding ?? new Thickness(12),
            Background = Brush(SurfaceSecondary),
            Child = content
        };
    }

    private Control HeaderRow(string title, string summary)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                Text(title, 15, FontWeight.Bold, ForegroundPrimary, HeadingFont).WithColumn(0),
                Text(summary, 11, FontWeight.Normal, ForegroundMuted, DataFont).WithColumn(1)
            }
        };
    }

    private Control SettingsSectionHeader(string title, string meta)
    {
        return Stack(Orientation.Horizontal, 7,
            Text(title, 12, FontWeight.SemiBold, ForegroundPrimary),
            Text(meta, 10, FontWeight.Normal, ForegroundMuted, CaptionFont));
    }

    private Control SettingLine(string label, string value, RowKind kind)
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Height = 25,
            Children =
            {
                Text(label, 11, FontWeight.Normal, ForegroundSecondary).WithColumn(0),
                Text(value, 11, FontWeight.Medium, KindForeground(kind), DataFont).Ellipsis().WithColumn(1)
            }
        };
    }

    private Control SettingPath(string label, string? path)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            Background = Brush(SurfacePrimary),
            Child = Stack(Orientation.Vertical, 3,
                Text(label, 10, FontWeight.Normal, ForegroundMuted, CaptionFont),
                Text(path ?? _text.T("none"), 10, FontWeight.Normal, ForegroundSecondary, DataFont).Ellipsis())
        };
    }

    private Control CodeBlock(string title, string code)
    {
        var copy = ActionButton(_text.T("button.copy"), () => CopyTextAsync(code), ButtonKind.Code);
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderBrush = Brush(CodeBorder),
            BorderThickness = new Thickness(1),
            Background = Brush(CodeBg),
            Padding = new Thickness(10),
            Child = Stack(Orientation.Vertical, 8,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        Text(title, 10, FontWeight.SemiBold, "#B6C4B8", DataFont).WithColumn(0),
                        copy.WithColumn(1)
                    }
                },
                new SelectableTextBlock
                {
                    Text = code,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = DataFont,
                    FontSize = 10,
                    Foreground = Brush("#DDE9DF"),
                    LineHeight = 15
                })
        };
    }

    private Control EmptyState(string title, string body)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Background = Brush(SurfacePrimary),
            Child = Stack(Orientation.Horizontal, 12,
                BadgeIcon("-", RowKind.Neutral),
                Stack(Orientation.Vertical, 5,
                    Text(title, 13, FontWeight.SemiBold, ForegroundPrimary),
                    Text(body, 12, FontWeight.Normal, ForegroundSecondary).Wrap()))
        };
    }

    private Control StateTile(string title, string body, RowKind kind)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Background = Brush(kind == RowKind.Danger ? StatusErrorBg : SurfaceSecondary),
            BorderBrush = Brush(kind == RowKind.Danger ? StatusError : SurfaceSecondary),
            BorderThickness = kind == RowKind.Danger ? new Thickness(1) : new Thickness(0),
            Child = Stack(Orientation.Vertical, 8,
                BadgeIcon(kind == RowKind.Danger ? "!" : "-", kind),
                Text(title, 14, FontWeight.Bold, ForegroundPrimary, HeadingFont),
                Text(body, 12, FontWeight.Normal, kind == RowKind.Danger ? StatusError : ForegroundSecondary).Wrap())
        };
    }

    private Control OverviewMetric(string label, string value, string body, RowKind kind)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Vertical, 5,
                Stack(Orientation.Horizontal, 6, Dot(KindForeground(kind)), Text(label, 11, FontWeight.Normal, ForegroundMuted, CaptionFont)),
                Text(value, 17, FontWeight.Bold, ForegroundPrimary, DataFont),
                Text(body, 11, FontWeight.Normal, ForegroundSecondary).Wrap())
        };
    }

    private Control MetricCard(string label, string value, string valueColor, bool dark = false)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14),
            Background = Brush(dark ? SurfaceInverse : SurfaceSecondary),
            Child = Stack(Orientation.Vertical, 4,
                Text(label, 12, FontWeight.Normal, dark ? ForegroundInverse : ForegroundMuted, CaptionFont),
                Text(value, dark ? 17 : 30, FontWeight.SemiBold, valueColor, DataFont).Ellipsis())
        };
    }

    private Control SummaryPill(string label, string value)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(11),
            Background = Brush("#304934"),
            Child = Stack(Orientation.Vertical, 4,
                Text(label.ToUpperInvariant(), 11, FontWeight.Normal, "#FFFFFFA8", CaptionFont),
                Text(value, 23, FontWeight.SemiBold, ForegroundInverse, DataFont))
        };
    }

    private Control SummaryBar(string label, int count, int total, string color)
    {
        var width = Math.Max(2, Math.Min(180, total == 0 ? 2 : count * 180.0 / total));
        return Stack(Orientation.Vertical, 4,
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    Text(label, 10, FontWeight.Normal, "#B6C4B8").WithColumn(0),
                    Text(count.ToString(), 10, FontWeight.SemiBold, ForegroundInverse, DataFont).WithColumn(1)
                }
            },
            new Border
            {
                Height = 8,
                CornerRadius = new CornerRadius(999),
                Background = Brush("#DDE9DF"),
                Child = new Border
                {
                    Width = width,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(999),
                    Background = Brush(color)
                }
            });
    }

    private Control SearchPlaceholder(string text, double width, double height = 34, double radius = 8, double fontSize = 12, bool showIcon = false)
    {
        Control content = showIcon
            ? Stack(Orientation.Horizontal, 8,
                LineIcon("search", ForegroundMuted),
                Text(text, fontSize, FontWeight.Normal, ForegroundMuted))
            : Text(text, fontSize, FontWeight.Normal, ForegroundMuted);

        return new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(radius),
            Background = Brush(SurfacePrimary),
            BorderBrush = Brush(BorderSubtle),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Child = content
        };
    }

    private Control SegmentedStatusFilters()
    {
        return new Border
        {
            Height = 34,
            CornerRadius = new CornerRadius(8),
            Background = Brush(SurfaceTertiary),
            Padding = new Thickness(4),
            Child = Stack(Orientation.Horizontal, 4,
                FilterSegment(_text.T("filter.all"), selected: true),
                FilterSegment(_text.T("filter.online")),
                FilterSegment(_text.T("filter.timeout")))
        };
    }

    private Control FilterSegment(string label, bool selected = false)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 5),
            Background = Brush(selected ? SurfacePrimary : SurfaceTertiary),
            Child = Text(label, 10, selected ? FontWeight.SemiBold : FontWeight.Normal, selected ? ForegroundPrimary : ForegroundSecondary)
        };
    }

    private Control EndpointChip(string label, string value)
    {
        return new Border
        {
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7),
            Background = Brush(SurfaceSecondary),
            Child = Stack(Orientation.Horizontal, 6,
                Text(label, 10, FontWeight.SemiBold, ForegroundMuted, CaptionFont),
                Text(value, 10, FontWeight.Normal, ForegroundPrimary, DataFont))
        };
    }

    private Border ActionButton(string text, Func<Task> action, ButtonKind kind = ButtonKind.Secondary, bool enabled = true)
    {
        var palette = GetButtonPalette(kind);
        return ClickableSurface(
            CenteredText(text, palette.FontSize, FontWeight.SemiBold, palette.ForegroundColor),
            action,
            palette,
            ButtonHoverPalette(kind),
            enabled);
    }

    private Border MiniButton(string text, Func<Task> action, ButtonKind kind = ButtonKind.Mini, bool enabled = true)
    {
        return ActionButton(text, action, kind, enabled);
    }

    private Border TargetActionButton(string text, double width, Func<Task> action, ButtonKind kind = ButtonKind.Mini, bool enabled = true)
    {
        var button = ActionButton(text, action, kind, enabled);
        button.Width = width;
        button.MinWidth = width;
        button.Padding = new Thickness(0);
        return button;
    }

    private Border IconButton(string iconName, Func<Task> action, ButtonKind kind = ButtonKind.Square, bool enabled = true)
    {
        var palette = GetButtonPalette(kind);
        var button = ClickableSurface(
            LineIcon(iconName, palette.ForegroundColor),
            action,
            palette,
            ButtonHoverPalette(kind),
            enabled);
        button.Width = palette.Height;
        button.MinWidth = palette.Height;
        button.Padding = new Thickness(0);
        return button;
    }

    private Border SmallSquareButton(string text, Func<Task> action)
    {
        var button = ActionButton(text, action, ButtonKind.Square);
        button.Width = 30;
        button.MinWidth = 30;
        button.Padding = new Thickness(0);
        return button;
    }

    private Border ClickableSurface(
        Control content,
        Func<Task> action,
        ButtonPalette normal,
        ButtonPalette hover,
        bool enabled = true,
        bool hoverEnabled = true)
    {
        var button = new Border
        {
            MinHeight = normal.Height,
            Padding = normal.Padding,
            Background = normal.Background,
            BorderBrush = normal.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(normal.Radius),
            Cursor = new Cursor(enabled ? StandardCursorType.Hand : StandardCursorType.Arrow),
            Opacity = enabled ? 1 : 0.45,
            Child = content
        };

        var over = false;
        var pressed = false;
        var busy = false;

        void Apply(ButtonPalette palette)
        {
            button.Background = palette.Background;
            button.BorderBrush = palette.Border;
        }

        button.PointerEntered += (_, _) =>
        {
            over = true;
            if (enabled && hoverEnabled && !busy)
            {
                Apply(hover);
            }
        };
        button.PointerExited += (_, _) =>
        {
            over = false;
            pressed = false;
            if (enabled && !busy)
            {
                Apply(normal);
            }
        };
        button.PointerPressed += (_, _) =>
        {
            if (enabled && !busy)
            {
                pressed = true;
                button.Opacity = 0.78;
            }
        };
        button.PointerReleased += async (_, _) =>
        {
            if (!enabled || busy || !over || !pressed)
            {
                pressed = false;
                return;
            }

            pressed = false;
            busy = true;
            button.Opacity = 0.62;
            try
            {
                await action().ConfigureAwait(true);
            }
            finally
            {
                busy = false;
                button.Opacity = enabled ? 1 : 0.45;
                Apply(over && hoverEnabled ? hover : normal);
            }
        };

        return button;
    }

    private Border Chip(string text, RowKind kind)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 4),
            Background = Brush(KindBackground(kind)),
            Child = Text(text, 10, FontWeight.SemiBold, KindForeground(kind), DataFont)
        };
    }

    private Control HealthBadge(TargetHealth health)
    {
        var kind = health switch
        {
            TargetHealth.Healthy => RowKind.Success,
            TargetHealth.Unreachable => RowKind.Warning,
            TargetHealth.Closing => RowKind.Neutral,
            _ => RowKind.Neutral
        };

        return Stack(Orientation.Horizontal, 6,
            Dot(KindForeground(kind)),
            Text(_text.T("status." + HealthKey(health)), 11, FontWeight.SemiBold, KindForeground(kind)));
    }

    private static Control BadgeIcon(string value, RowKind kind)
    {
        return new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Background = Brush(KindBackground(kind)),
            Child = CenteredText(value, 12, FontWeight.Bold, KindForeground(kind), DataFont)
        };
    }

    private static Control Dot(string color)
    {
        return new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(999),
            Background = Brush(color),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock Text(string text, double size, FontWeight weight, string color, FontFamily? font = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = font ?? BodyFont,
            FontSize = size,
            FontWeight = weight,
            Foreground = Brush(color),
            TextTrimming = TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CenteredText(string text, double size, FontWeight weight, string color, FontFamily? font = null)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = font ?? BodyFont,
            FontSize = size,
            FontWeight = weight,
            Foreground = Brush(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
    }

    private static Control LineIcon(string name, string color)
    {
        return new Viewbox
        {
            Width = 15,
            Height = 15,
            Stretch = Stretch.Uniform,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(IconGeometry(name)),
                Stroke = Brush(color),
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            }
        };
    }

    private static string IconGeometry(string name)
    {
        return name switch
        {
            "layout-dashboard" => "M3 3H10V10H3V3ZM14 3H21V8H14V3ZM14 12H21V21H14V12ZM3 14H10V21H3V14Z",
            "app-window" => "M4 5H20V18H4V5ZM4 9H20",
            "activity" => "M3 12H7L10 5L14 19L17 12H21",
            "cpu" => "M9 9H15V15H9V9ZM4 10H7M4 14H7M17 10H20M17 14H20M10 4V7M14 4V7M10 17V20M14 17V20",
            "package" => "M5 6H19L21 16H3L5 6ZM7 19H17",
            "scroll-text" => "M8 6H21M8 12H21M8 18H21M4 6H4.01M4 12H4.01M4 18H4.01",
            "settings" => "M12 8A4 4 0 1 0 12 16A4 4 0 1 0 12 8ZM12 2V5M12 19V22M2 12H5M19 12H22M4.9 4.9L7 7M17 17L19.1 19.1M19.1 4.9L17 7M7 17L4.9 19.1",
            "search" => "M11 4A7 7 0 1 0 11 18A7 7 0 1 0 11 4ZM16 16L21 21",
            "refresh-cw" => "M21 12A9 9 0 0 1 6 18.7M3 12A9 9 0 0 1 18 5.3M18 2V6H22M2 18H6V22",
            "sliders-horizontal" => "M4 7H10M14 7H20M10 7A2 2 0 1 0 14 7A2 2 0 1 0 10 7ZM4 17H6M10 17H20M6 17A2 2 0 1 0 10 17A2 2 0 1 0 6 17Z",
            _ => "M4 12H20"
        };
    }

    private static StackPanel Stack(Orientation orientation, double spacing, params Control[] children)
    {
        var panel = new StackPanel
        {
            Orientation = orientation,
            Spacing = spacing
        };

        foreach (var child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    private async Task RefreshAllMetadataAsync()
    {
        var targets = RuntimeHolder.TargetRegistry.ListTargets();
        foreach (var target in targets)
        {
            await CallTargetToolAsync("ida_get_metadata", target.InstanceId, _text.T("action.metadataRefreshed")).ConfigureAwait(true);
        }
    }

    private async Task CallTargetToolAsync(string toolName, string instanceId, string successMessage)
    {
        var args = JsonSerializer.SerializeToElement(new { instanceId });
        var result = await RuntimeHolder.ToolHandler.CallAsync(toolName, args, CancellationToken.None).ConfigureAwait(true);
        _settingsMessage = result.IsError ? result.Text : successMessage;
        _settingsDirty = true;
        RenderSelectedPage(force: true);
    }

    private async Task CallTargetCloseAsync(TargetInfo target)
    {
        var args = JsonSerializer.SerializeToElement(new { target.InstanceId, force = false });
        var result = await RuntimeHolder.ToolHandler.CallAsync("ida_close_target", args, CancellationToken.None).ConfigureAwait(true);
        _settingsMessage = result.IsError ? result.Text : _text.T("action.closeSent");
        _settingsDirty = true;
        RenderSelectedPage(force: true);
    }

    private Task OpenTargetInIdaAsync(TargetInfo target)
    {
        if (string.IsNullOrWhiteSpace(target.InputPath))
        {
            _settingsMessage = _text.T("action.noInputPath");
            _settingsDirty = true;
            return Task.CompletedTask;
        }

        try
        {
            RuntimeHolder.IdaLaunchService.Launch(new IdaLaunchRequest(target.InputPath, null, []));
            _settingsMessage = _text.T("action.launchSent");
        }
        catch (Exception exc)
        {
            _settingsMessage = exc.Message;
        }

        _settingsDirty = true;
        RenderSelectedPage(force: true);
        return Task.CompletedTask;
    }

    private Task RunSettingsActionAsync(Func<PluginInstallStatus> action)
    {
        try
        {
            var next = action();
            _settingsMessage = LocalizePluginMessage(next);
        }
        catch (Exception exc)
        {
            _settingsMessage = exc.Message;
        }

        _settingsDirty = true;
        RenderSelectedPage(force: true);
        return Task.CompletedTask;
    }

    private Task RunAgentConfigureAsync(AgentConfigStatus agent)
    {
        try
        {
            var next = RuntimeHolder.AgentConfigService.Configure(agent.AgentName, agent.ConfigPath);
            _settingsMessage = $"{next.AgentName}: {AgentSummary(next)}";
        }
        catch (Exception exc)
        {
            _settingsMessage = exc.Message;
        }

        _settingsDirty = true;
        RenderSelectedPage(force: true);
        return Task.CompletedTask;
    }

    private async Task CopyTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
            _settingsMessage = _text.T("action.copied");
            _settingsDirty = true;
            RenderSelectedPage(force: true);
        }
    }

    private Task CopyLogsAsync()
    {
        var text = string.Join(
            Environment.NewLine,
            RuntimeHolder.OperationLog.List(200).Select(log =>
                $"{log.TimestampUtc.LocalDateTime:HH:mm:ss} {(log.Success ? "INFO" : "ERROR")} {log.TargetAlias} {log.ToolName} {log.Error}"));
        return CopyTextAsync(text);
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

    private string PluginSummary(PluginInstallStatus status)
    {
        if (!status.IsInstalled)
        {
            return _text.T("plugin.notInstalled");
        }

        return status.IsCompatible ? _text.T("plugin.compatible") : _text.T("plugin.attention");
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

    private bool MatchesLogFilter(OperationLogEntry log)
    {
        return _logFilter switch
        {
            LogFilter.Error => !log.Success,
            LogFilter.Plugin => log.ToolName.StartsWith("target.", StringComparison.OrdinalIgnoreCase)
                || log.ToolName.StartsWith("analysis.", StringComparison.OrdinalIgnoreCase),
            LogFilter.Agent => log.ToolName.StartsWith("analysis.", StringComparison.OrdinalIgnoreCase),
            LogFilter.Mcp => log.ToolName.StartsWith("ida_", StringComparison.OrdinalIgnoreCase),
            LogFilter.IdaTcp => log.ToolName.StartsWith("target.", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static string TargetTitle(TargetInfo target)
    {
        return IsFallbackName(target.BinaryName, target.ProcessId)
            ? target.InputPath ?? target.DatabasePath ?? target.Alias
            : target.BinaryName;
    }

    private static bool IsFallbackName(string? value, int processId)
    {
        return string.IsNullOrWhiteSpace(value)
            || processId > 0 && value.Equals($"ida-{processId}", StringComparison.OrdinalIgnoreCase);
    }

    private static string CompactPath(string? path, int maxLength = 34)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "-";
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        var file = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized;
        var prefixLength = Math.Max(8, maxLength - file.Length - 5);
        if (file.Length + 5 >= maxLength)
        {
            return "..." + file[^Math.Min(file.Length, maxLength - 3)..];
        }

        return normalized[..Math.Min(prefixLength, normalized.Length)] + "/.../" + file;
    }

    private static string CompactEndpoint(string endpoint)
    {
        return endpoint
            .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("/mcp", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string CompactPlatform(TargetInfo target)
    {
        var platform = target.Platform;
        if (string.IsNullOrWhiteSpace(platform))
        {
            return target.IdaVersion ?? "-";
        }

        platform = platform.Replace("Microsoft Windows", "Windows", StringComparison.OrdinalIgnoreCase)
            .Replace("macOS", "macOS", StringComparison.OrdinalIgnoreCase);
        return platform.Length <= 14 ? platform : platform[..14] + "...";
    }

    private string LastSeen(DateTimeOffset lastSeenUtc)
    {
        var elapsed = DateTimeOffset.UtcNow - lastSeenUtc;
        if (elapsed.TotalSeconds < 60)
        {
            return _text.F("time.secondsAgo", Math.Max(0, (int)elapsed.TotalSeconds));
        }

        if (elapsed.TotalMinutes < 60)
        {
            return _text.F("time.minutesAgo", (int)elapsed.TotalMinutes);
        }

        return lastSeenUtc.LocalDateTime.ToString("HH:mm", _text.Culture);
    }

    private string BestHeartbeat()
    {
        var latest = RuntimeHolder.TargetRegistry.ListTargets()
            .OrderByDescending(target => target.LastSeenUtc)
            .FirstOrDefault();
        return latest is null ? _text.T("status.none") : LastSeen(latest.LastSeenUtc);
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

    private static string LogSource(OperationLogEntry log)
    {
        if (log.ToolName.StartsWith("analysis.", StringComparison.OrdinalIgnoreCase))
        {
            return "Agent";
        }

        if (log.ToolName.StartsWith("target.", StringComparison.OrdinalIgnoreCase))
        {
            return "IDA TCP";
        }

        return "MCP";
    }

    private string PageTitle(CenterPage page)
    {
        return page switch
        {
            CenterPage.Overview => _text.T("tab.overview"),
            CenterPage.Targets => _text.T("tab.targets"),
            CenterPage.Activity => _text.T("tab.activity"),
            CenterPage.Processes => _text.T("tab.processes"),
            CenterPage.Installations => _text.T("tab.installations"),
            CenterPage.Logs => _text.T("tab.logs"),
            CenterPage.Settings => _text.T("tab.settings"),
            _ => page.ToString()
        };
    }

    private static string NavGlyph(CenterPage page)
    {
        return page switch
        {
            CenterPage.Overview => "layout-dashboard",
            CenterPage.Targets => "app-window",
            CenterPage.Activity => "activity",
            CenterPage.Processes => "cpu",
            CenterPage.Installations => "package",
            CenterPage.Logs => "scroll-text",
            CenterPage.Settings => "settings",
            _ => "line"
        };
    }

    private static string KindForeground(RowKind kind)
    {
        return kind switch
        {
            RowKind.Success => StatusOnline,
            RowKind.Warning => StatusWarning,
            RowKind.Danger => StatusError,
            RowKind.Info => AccentBlue,
            _ => ForegroundMuted
        };
    }

    private static string KindBackground(RowKind kind)
    {
        return kind switch
        {
            RowKind.Success => StatusOnlineBg,
            RowKind.Warning => StatusWarningBg,
            RowKind.Danger => StatusErrorBg,
            RowKind.Info => "#EAF1FF",
            _ => SurfaceTertiary
        };
    }

    private static ButtonPalette GetButtonPalette(ButtonKind kind)
    {
        return kind switch
        {
            ButtonKind.Primary => new ButtonPalette(Brush(AccentPrimary), Brush(AccentPrimary), Brush(ForegroundInverse), ForegroundInverse, 8, 30, 11, new Thickness(12, 6)),
            ButtonKind.Dark => new ButtonPalette(Brush(SurfaceInverse), Brush(SurfaceInverse), Brush(ForegroundInverse), ForegroundInverse, 8, 34, 11, new Thickness(12, 8)),
            ButtonKind.Green => new ButtonPalette(Brush(AccentPrimary), Brush(AccentPrimary), Brush(ForegroundInverse), ForegroundInverse, 8, 34, 12, new Thickness(12, 9)),
            ButtonKind.ActionDark => new ButtonPalette(Brush(SurfaceInverse), Brush(SurfaceInverse), Brush(ForegroundInverse), ForegroundInverse, 12, 36, 13, new Thickness(12, 8)),
            ButtonKind.Clear => new ButtonPalette(Brush(SurfacePrimary), Brush(BorderSubtle), Brush(StatusError), StatusError, 12, 36, 13, new Thickness(12, 8)),
            ButtonKind.Light => new ButtonPalette(Brush(SurfacePrimary), Brush(SurfacePrimary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 28, 11, new Thickness(10, 5)),
            ButtonKind.DarkSmall => new ButtonPalette(Brush(SurfaceInverse), Brush(SurfaceInverse), Brush(ForegroundInverse), ForegroundInverse, 8, 28, 10, new Thickness(10, 5)),
            ButtonKind.Warning => new ButtonPalette(Brush(StatusWarning), Brush(StatusWarning), Brush(ForegroundInverse), ForegroundInverse, 8, 28, 10, new Thickness(10, 5)),
            ButtonKind.DangerMini => new ButtonPalette(Brush(SurfaceTertiary), Brush(SurfaceTertiary), Brush(StatusError), StatusError, 8, 26, 10, new Thickness(7, 3)),
            ButtonKind.Mini => new ButtonPalette(Brush(SurfaceSecondary), Brush(SurfaceSecondary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 26, 10, new Thickness(7, 3)),
            ButtonKind.Square => new ButtonPalette(Brush(SurfaceSecondary), Brush(SurfaceSecondary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 30, 10, new Thickness(0)),
            ButtonKind.DarkSquare => new ButtonPalette(Brush(SurfaceInverse), Brush(SurfaceInverse), Brush(ForegroundInverse), ForegroundInverse, 8, 30, 10, new Thickness(0)),
            ButtonKind.Code => new ButtonPalette(Brush("#253D29"), Brush("#253D29"), Brush(ForegroundInverse), ForegroundInverse, 999, 20, 9, new Thickness(8, 2)),
            ButtonKind.FilterSelected => new ButtonPalette(Brush(AccentPrimary), Brush(AccentPrimary), Brush(ForegroundInverse), ForegroundInverse, 999, 30, 10, new Thickness(12, 6)),
            ButtonKind.Filter => new ButtonPalette(Brush(SurfacePrimary), Brush(BorderSubtle), Brush(ForegroundSecondary), ForegroundSecondary, 999, 30, 10, new Thickness(12, 6)),
            ButtonKind.Selected => new ButtonPalette(Brush(SurfacePrimary), Brush(SurfacePrimary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 32, 13, new Thickness(9, 0)),
            ButtonKind.Ghost => new ButtonPalette(Brush("#00000000"), Brush("#00000000"), Brush(ForegroundSecondary), ForegroundSecondary, 8, 32, 13, new Thickness(9, 0)),
            _ => new ButtonPalette(Brush(SurfacePrimary), Brush(BorderSubtle), Brush(ForegroundPrimary), ForegroundPrimary, 8, 30, 11, new Thickness(12, 6))
        };
    }

    private static ButtonPalette ButtonHoverPalette(ButtonKind kind)
    {
        return kind switch
        {
            ButtonKind.Primary => new ButtonPalette(Brush("#255C35"), Brush("#255C35"), Brush(ForegroundInverse), ForegroundInverse, 8, 30, 11, new Thickness(12, 6)),
            ButtonKind.Green => new ButtonPalette(Brush("#255C35"), Brush("#255C35"), Brush(ForegroundInverse), ForegroundInverse, 8, 34, 12, new Thickness(12, 9)),
            ButtonKind.Dark or ButtonKind.DarkSmall or ButtonKind.ActionDark => new ButtonPalette(Brush("#142318"), Brush("#142318"), Brush(ForegroundInverse), ForegroundInverse, 12, 36, 13, new Thickness(12, 8)),
            ButtonKind.Clear => new ButtonPalette(Brush(StatusErrorBg), Brush(StatusError), Brush(StatusError), StatusError, 12, 36, 13, new Thickness(12, 8)),
            ButtonKind.DangerMini => new ButtonPalette(Brush("#DDE3DD"), Brush("#DDE3DD"), Brush(StatusError), StatusError, 8, 26, 10, new Thickness(7, 3)),
            ButtonKind.DarkSquare => new ButtonPalette(Brush("#142318"), Brush("#142318"), Brush(ForegroundInverse), ForegroundInverse, 8, 30, 10, new Thickness(0)),
            ButtonKind.Mini or ButtonKind.Square or ButtonKind.Secondary => new ButtonPalette(Brush(SurfaceTertiary), Brush(SurfaceTertiary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 30, 11, new Thickness(12, 6)),
            ButtonKind.Filter => new ButtonPalette(Brush(SurfaceSecondary), Brush(BorderSubtle), Brush(ForegroundPrimary), ForegroundPrimary, 999, 30, 10, new Thickness(12, 6)),
            ButtonKind.Ghost => new ButtonPalette(Brush(SurfaceTertiary), Brush(SurfaceTertiary), Brush(ForegroundPrimary), ForegroundPrimary, 8, 32, 13, new Thickness(9, 0)),
            _ => GetButtonPalette(kind)
        };
    }

    private sealed record ButtonPalette(
        IBrush Background,
        IBrush Border,
        IBrush Foreground,
        string ForegroundColor,
        double Radius,
        double Height,
        double FontSize,
        Thickness Padding);

    private enum CenterPage
    {
        Overview,
        Targets,
        Activity,
        Processes,
        Installations,
        Logs,
        Settings
    }

    private enum LogFilter
    {
        All,
        Mcp,
        IdaTcp,
        Plugin,
        Agent,
        Error
    }

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
        Dark,
        Green,
        ActionDark,
        Clear,
        Light,
        DarkSmall,
        Warning,
        DangerMini,
        Mini,
        Square,
        DarkSquare,
        Code,
        Filter,
        FilterSelected,
        Selected,
        Ghost
    }
}
