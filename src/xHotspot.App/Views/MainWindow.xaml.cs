using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace xHotspot.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
            if (File.Exists(iconPath))
            {
                Icon = new BitmapImage(new Uri(iconPath));
            }
        }
        catch { }
    }
}
