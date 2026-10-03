using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using xHotspot.App.Views;
using xHotspot.Core.Models;
using xHotspot.App.Services;

namespace xHotspot.App;

public partial class App : Application
{
    private TaskbarIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private System.Windows.Controls.MenuItem? _pauseItem;
    private readonly IpcClient _ipcClient = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _notifyIcon = new TaskbarIcon
        {
            ToolTipText = "xHotspot — Always-On Mobile Hotspot",
            Icon = System.Drawing.SystemIcons.Application
        };

        var contextMenu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (s, args) => ShowMainWindow();
        contextMenu.Items.Add(openItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var enableItem = new System.Windows.Controls.MenuItem { Header = "Enable Hotspot" };
        enableItem.Click += async (s, args) => {
            await _ipcClient.SendCommandAsync(IpcCommandType.EnableHotspot);
        };
        contextMenu.Items.Add(enableItem);

        _pauseItem = new System.Windows.Controls.MenuItem { Header = "Pause Automation" };
        _pauseItem.Click += async (s, args) => {
            var resp = await _ipcClient.SendCommandAsync(IpcCommandType.GetStatus);
            bool isPaused = false;
            if (resp.Success && !string.IsNullOrEmpty(resp.DataJson))
            {
                var dto = System.Text.Json.JsonSerializer.Deserialize<StatusDto>(resp.DataJson);
                if (dto != null) isPaused = dto.AutomationPaused;
            }

            if (isPaused)
            {
                await _ipcClient.SendCommandAsync(IpcCommandType.ResumeAutomation);
                if (_pauseItem != null) _pauseItem.Header = "Pause Automation";
            }
            else
            {
                await _ipcClient.SendCommandAsync(IpcCommandType.PauseAutomation);
                if (_pauseItem != null) _pauseItem.Header = "Resume Automation";
            }
        };
        contextMenu.Items.Add(_pauseItem);

        contextMenu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (s, args) => {
            _notifyIcon?.Dispose();
            Current.Shutdown();
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenu = contextMenu;
        _notifyIcon.TrayMouseDoubleClick += (s, args) => ShowMainWindow();

        // Starts hidden in system tray
    }

    private void ShowMainWindow()
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
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
