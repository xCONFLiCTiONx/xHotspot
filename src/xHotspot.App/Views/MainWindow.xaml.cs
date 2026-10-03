using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace xHotspot.App.Views;

public partial class MainWindow : Window
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;

        try
        {
            Icon = BitmapFrame.Create(new Uri("pack://application:,,,/icon.ico"));
        }
        catch
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = BitmapFrame.Create(new Uri(iconPath));
                }
            }
            catch { }
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var icon = App.Current.GetAppIcon();
                if (icon != null)
                {
                    SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), icon.Handle);
                    SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), icon.Handle);
                }
            }
        }
        catch { }
    }
}
