using System.IO;
using System.Linq;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Windows.Networking.Connectivity;
using xHotspot.App.Services;
using xHotspot.App.Views;
using xHotspot.Core.Hotspot;
using xHotspot.Core.Models;
using xHotspot.Core.Services;

namespace xHotspot.App;

public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private System.Windows.Controls.MenuItem? _toggleMenuItem;
    private readonly IpcClient _ipcClient = new();
    private readonly LoggerService _logger = new();

    public WindowsHotspotManager HotspotManager { get; }
    public bool IsAutoReenableEnabled { get; set; } = true;

    public static new App Current => (App)Application.Current;

    public App()
    {
        HotspotManager = new WindowsHotspotManager(_logger);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        bool isStartup = e.Args.Any(a => string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(a, "-startup", StringComparison.OrdinalIgnoreCase));

        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        System.Drawing.Icon trayIcon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "xHotspot — Always-On Mobile Hotspot",
            Icon = trayIcon
        };

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (s, args) => ShowMainWindow();
        contextMenu.Items.Add(openItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        _toggleMenuItem = new System.Windows.Controls.MenuItem { Header = "Enable Hotspot" };
        _toggleMenuItem.Click += async (s, args) => await ToggleHotspotAsync();
        contextMenu.Items.Add(_toggleMenuItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, args) => {
            _notifyIcon?.Dispose();
            Current.Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;
        _notifyIcon.TrayMouseDoubleClick += (s, args) => ShowMainWindow();

        // Register power mode change events (e.g., system sleep/resume)
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        // Register network status change events
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;

        // Start background auto-reenable monitoring loop
        StartMonitoringLoop();

        if (isStartup)
        {
            // Started via --startup: run in system tray and immediately enable hotspot
            IsAutoReenableEnabled = true;
            _ = EnableHotspotAsync();
        }
        else
        {
            // Started manually: open GUI window directly
            ShowMainWindow();
        }
    }

    public async Task EnableHotspotAsync()
    {
        IsAutoReenableEnabled = true;
        _logger.LogInformation("Enabling Hotspot and activating Auto-Reenable...");
        await HotspotManager.EnableAsync();
        await UpdateTrayMenuAsync();
    }

    public async Task DisableHotspotAsync()
    {
        IsAutoReenableEnabled = false;
        _logger.LogInformation("Disabling Hotspot and pausing Auto-Reenable...");
        await HotspotManager.DisableAsync();
        await UpdateTrayMenuAsync();
    }

    public async Task ToggleHotspotAsync()
    {
        var status = await HotspotManager.GetStatusAsync();
        if (status == HotspotStatus.On || status == HotspotStatus.TurningOn)
        {
            await DisableHotspotAsync();
        }
        else
        {
            await EnableHotspotAsync();
        }
    }

    private async Task UpdateTrayMenuAsync()
    {
        if (_toggleMenuItem != null)
        {
            var status = await HotspotManager.GetStatusAsync();
            _toggleMenuItem.Header = (status == HotspotStatus.On || status == HotspotStatus.TurningOn)
                ? "Disable Hotspot"
                : "Enable Hotspot";
        }
    }

    private void StartMonitoringLoop()
    {
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += async (s, e) =>
        {
            await UpdateTrayMenuAsync();

            if (IsAutoReenableEnabled)
            {
                var status = await HotspotManager.GetStatusAsync();
                if (status == HotspotStatus.Off)
                {
                    _logger.LogInformation("Always-On check: Hotspot is OFF. Auto-reenabling Mobile Hotspot...");
                    await HotspotManager.EnableAsync();
                    await UpdateTrayMenuAsync();
                }
            }
        };
        timer.Start();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _logger.LogInformation("System resumed from sleep. Checking Mobile Hotspot status...");
            Console.WriteLine(">>> [APP] Power resume detected. Waiting for network interfaces and re-enabling hotspot...");
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500); // Allow Wi-Fi / network interfaces to re-initialize
                if (IsAutoReenableEnabled)
                {
                    var status = await HotspotManager.GetStatusAsync();
                    if (status == HotspotStatus.Off)
                    {
                        _logger.LogInformation("Post-resume check: Hotspot is OFF. Re-enabling Mobile Hotspot immediately...");
                        await HotspotManager.EnableAsync();
                        await UpdateTrayMenuAsync();
                    }
                }
            });
        }
    }

    private void OnNetworkStatusChanged(object? sender)
    {
        if (IsAutoReenableEnabled)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                var status = await HotspotManager.GetStatusAsync();
                if (status == HotspotStatus.Off && IsAutoReenableEnabled)
                {
                    _logger.LogInformation("Network status change check: Hotspot is OFF. Re-enabling Mobile Hotspot...");
                    await HotspotManager.EnableAsync();
                    await UpdateTrayMenuAsync();
                }
            });
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (s, args) => { _mainWindow = null; };
        }
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
