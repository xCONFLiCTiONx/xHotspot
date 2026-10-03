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
    public bool IsAutoReenableEnabled { get; set; } = false;
    public bool IsBusy { get; private set; }

    public static new App Current => (App)Application.Current;

    public App()
    {
        HotspotManager = new WindowsHotspotManager(_logger);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        string fullCmdLine = Environment.CommandLine;
        bool isStartup = e.Args.Any(a => a.Contains("startup", StringComparison.OrdinalIgnoreCase)) ||
                         fullCmdLine.Contains("startup", StringComparison.OrdinalIgnoreCase);

        // Single Instance Check
        _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            _logger.LogInformation("Another instance of xHotspot is already running.");

            if (!isStartup)
            {
                // If started manually without --startup while already running in tray, bring up the existing GUI
                SignalPrimaryInstanceToShowGui();
            }

            Current.Shutdown();
            return;
        }

        // Initialize tray icon & IPC signal listener FIRST
        InitializeTrayIcon();
        StartGuiSignalListener();

        // Run startup task creation asynchronously off the UI thread
        Task.Run(() => EnsureStartupTask());

        // Register power mode change events (e.g., system sleep/resume)
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        // Register network status change events
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;

        // Start background auto-reenable monitoring loop
        StartMonitoringLoop();

        if (isStartup)
        {
            // Started via --startup: STAY IN SYSTEM TRAY ONLY, ENABLE HOTSPOT IMMEDIATELY, DO NOT SHOW GUI WINDOW
            IsAutoReenableEnabled = true;
            _ = EnableHotspotAsync();
        }
        else
        {
            // Started manually without --startup: show GUI window immediately
            IsAutoReenableEnabled = false;
            ShowMainWindow();
        }
    }

    private System.Drawing.Icon GetAppIcon()
    {
        try
        {
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/logo.ico"));
            if (streamInfo != null)
            {
                using var stream = streamInfo.Stream;
                return new System.Drawing.Icon(stream);
            }
        }
        catch { }

        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
            }
        }
        catch { }

        return System.Drawing.SystemIcons.Application;
    }

    private void InitializeTrayIcon()
    {
        _notifyIcon = new TaskbarIcon
        {
            Icon = GetAppIcon()
        };

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (s, args) => ShowMainWindow();
        contextMenu.Items.Add(openItem);

        var hotspotInfoItem = new System.Windows.Controls.MenuItem { Header = "Hotspot Info" };
        hotspotInfoItem.Click += (s, args) => OpenHotspotSettings();
        contextMenu.Items.Add(hotspotInfoItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        _toggleMenuItem = new System.Windows.Controls.MenuItem { Header = "Enable Hotspot" };
        _toggleMenuItem.Click += async (s, args) => await ToggleHotspotAsync();
        contextMenu.Items.Add(_toggleMenuItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += async (s, args) => {
            await ExitApplicationAsync();
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;
        _notifyIcon.TrayMouseDoubleClick += (s, args) => ShowMainWindow();
    }

    private void OpenHotspotSettings()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:network-mobilehotspot")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to open Windows Mobile Hotspot settings", ex);
        }
    }

    private async Task ExitApplicationAsync()
    {
        _logger.LogInformation("Exit requested. Disabling Mobile Hotspot before exiting...");
        IsAutoReenableEnabled = false;
        try
        {
            await HotspotManager.DisableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Error disabling hotspot on exit", ex);
        }

        _notifyIcon?.Dispose();
        Current.Shutdown();
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
        IsBusy = true;
        IsAutoReenableEnabled = true;
        _logger.LogInformation("Enabling Hotspot...");
        try
        {
            await HotspotManager.EnableAsync();
        }
        finally
        {
            IsBusy = false;
            await UpdateTrayMenuAsync();
        }
    }

    public async Task DisableHotspotAsync()
    {
        IsBusy = true;
        IsAutoReenableEnabled = false;
        _logger.LogInformation("Disabling Hotspot...");
        try
        {
            await HotspotManager.DisableAsync();
        }
        finally
        {
            IsBusy = false;
            await UpdateTrayMenuAsync();
        }
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
            Console.WriteLine(">>> [APP] Power resume detected. Checking hotspot state...");
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
