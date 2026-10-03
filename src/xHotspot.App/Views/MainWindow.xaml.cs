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
            var streamInfo = Application.GetResourceStream(new Uri("pack://application:,,,/logo.ico"));
            if (streamInfo != null)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = streamInfo.Stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                Icon = bitmap;
            }
            else
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new BitmapImage(new Uri(iconPath));
                }
            }
        }
        catch
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new BitmapImage(new Uri(iconPath));
                }
            }
            catch { }
        }
    }
}
