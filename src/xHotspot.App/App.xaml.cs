using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
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
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "Global\\xHotspot_SingleInstance_Mutex";
    private const string PipeName = "xHotspot_GuiSignal_Pipe";

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

        // Single Instance Check
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _logger.LogInformation("Another instance of xHotspot is already running.");

            if (!isStartup)
            {
                // If started without --startup while already running in tray, bring up the existing GUI
                SignalPrimaryInstanceToShowGui();
            }

            Current.Shutdown();
            return;
        }

        // Create Scheduled Task / Registry Startup Entry
        EnsureStartupTask();

        // Listen for IPC signal from secondary instances to show GUI
        StartGuiSignalListener();

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

    private static void SignalPrimaryInstanceToShowGui()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000);
        }
        catch { }
    }

    private void StartGuiSignalListener()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    Dispatcher.Invoke(() => ShowMainWindow());
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
        });
    }

    private void EnsureStartupTask()
    {
        try
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xHotspot.App.exe");
            }

            // 1. HKCU Registry Run Key
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.SetValue("xHotspot", $"\"{exePath}\" --startup");
            }

            // 2. Task Scheduler Task via schtasks.exe
            string trArgument = $"\"\"{exePath}\" --startup\"";
            var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", $"/Create /TN \"xHotspot\" /TR {trArgument} /SC ONLOGON /F")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error setting up startup scheduled task/registry", ex);
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
        if (_singleInstanceMutex != null)
        {
            try
            {
                _singleInstanceMutex.ReleaseMutex();
            }
            catch { }
        }
        base.OnExit(e);
    }
}
