using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PixShift.Helpers;
using PixShift.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PixShift;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private static readonly string AppVersion = "1.0.0";
    private static readonly string AppName = "PixShift";

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel(DispatcherQueue);
        WindowHelper.RestorePosition(this, App.Settings.Current);
        WindowHelper.SetMinSize(this, minWidthLogical: 560, minHeightLogical: 500);
        SetWindowIcon();
        Closed += MainWindow_Closed;
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PixShift.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        WindowHelper.SavePosition(this, App.Settings.Current);
        ViewModel.SaveToSettings();
    }

    // ── ドラッグ&ドロップ ─────────────────────────────────────────
    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride != null)
            e.DragUIOverride.Caption = "ファイルを追加";
    }

    private void DropArea_DragEnter(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    private void DropArea_DragLeave(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
    }

    private async void DropArea_Drop(object sender, DragEventArgs e)
    {
        DropAreaBorder.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        try
        {
            if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(i => i.Path);
            await ViewModel.AddDroppedPathsAsync(paths);
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "ドロップ");
        }
    }

    private void ApplyResizeButton_Click(object sender, RoutedEventArgs e)
    {
        // フォーカスを受け取ることでNumberBoxのスピンボタンを閉じる
        ((Button)sender).Focus(FocusState.Pointer);
    }

    private void SkippedWarning_Closed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        ViewModel.IsSkippedFilesWarningOpen = false;
    }

    // ── メニュー ──────────────────────────────────────────────────
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PixShift", "logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo("explorer.exe", logDir) { UseShellExecute = true });
    }

    private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "バージョン情報",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = AppName, Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] },
                    new TextBlock { Text = $"バージョン {AppVersion}" },
                    new TextBlock
                    {
                        Text = "画像ファイルの形式変換ツールです。PNG / JPEG / WebP / BMP / TIFF / GIF / ICO に対応しています。",
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            },
            CloseButtonText = "閉じる",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

