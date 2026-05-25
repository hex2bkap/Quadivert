using Microsoft.UI.Xaml;
using PixShift.Services;

namespace PixShift;

public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }
    public static SettingsService Settings { get; } = new();
    public static LogService Log { get; } = new();

    public App()
    {
        InitializeComponent();
        Settings.Load();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
