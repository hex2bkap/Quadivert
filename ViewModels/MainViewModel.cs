using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Quadivert.Models;
using Quadivert.Services;
using ResizeMode = Quadivert.Models.ResizeMode;
using RenameMode = Quadivert.Models.RenameMode;

namespace Quadivert.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _cts;

    public string[] OutputFormats => ConversionService.OutputFormats;

    public MainViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        LoadFromSettings();
    }

    // ── ファイルリスト ────────────────────────────────────────────
    public ObservableCollection<ImageItem> Files { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartConversionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearAllCommand))]
    [NotifyPropertyChangedFor(nameof(FileCountText))]
    private int _fileCount;

    public string FileCountText => FileCount > 0
        ? $"追加済みファイル ({FileCount} 件)"
        : "追加済みファイル";

    // ── 読み込み・通知 ────────────────────────────────────────────
    [ObservableProperty] private bool _isLoadingFiles;
    [ObservableProperty] private bool _isSkippedFilesWarningOpen;
    [ObservableProperty] private string _skippedFilesMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingText))]
    [NotifyPropertyChangedFor(nameof(LoadingProgressRatio))]
    [NotifyPropertyChangedFor(nameof(IsLoadingIndeterminate))]
    private int _loadingProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadingText))]
    [NotifyPropertyChangedFor(nameof(LoadingProgressRatio))]
    [NotifyPropertyChangedFor(nameof(IsLoadingIndeterminate))]
    private int _loadingTotal;

    public string LoadingText => LoadingTotal > 0
        ? $"読み込み中: {LoadingProgress} / {LoadingTotal} 件"
        : "ファイルを読み込んでいます...";

    public double LoadingProgressRatio => LoadingTotal > 0
        ? (double)LoadingProgress / LoadingTotal * 100.0
        : 0;

    public bool IsLoadingIndeterminate => LoadingTotal == 0;

    // ── 変換設定 ──────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTransparencyWarning))]
    private string _selectedOutputFormat = "PNG";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResizePanelVisibility))]
    private bool _resizeEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPixelMode))]
    [NotifyPropertyChangedFor(nameof(IsPercentMode))]
    [NotifyPropertyChangedFor(nameof(PixelInputVisibility))]
    [NotifyPropertyChangedFor(nameof(PercentInputVisibility))]
    private ResizeMode _resizeMode = ResizeMode.Pixel;

    public bool IsPixelMode
    {
        get => ResizeMode == ResizeMode.Pixel;
        set { if (value) ResizeMode = ResizeMode.Pixel; }
    }
    public bool IsPercentMode
    {
        get => ResizeMode == ResizeMode.Percent;
        set { if (value) ResizeMode = ResizeMode.Percent; }
    }

    public Visibility PixelInputVisibility =>
        ResizeMode == ResizeMode.Pixel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PercentInputVisibility =>
        ResizeMode == ResizeMode.Percent ? Visibility.Visible : Visibility.Collapsed;

    // アスペクト比ロック
    private double _lockedAspectRatio = 1920.0 / 1080.0;
    private bool _updatingAspect = false;

    [ObservableProperty] private int _resizeWidth = 1920;
    [ObservableProperty] private int _resizeHeight = 1080;
    [ObservableProperty] private bool _lockAspectRatio = true;
    [ObservableProperty] private double _resizePercent = 100.0;

    partial void OnLockAspectRatioChanged(bool value)
    {
        if (value && ResizeHeight > 0)
            _lockedAspectRatio = (double)ResizeWidth / ResizeHeight;
    }

    partial void OnResizeWidthChanged(int value)
    {
        if (LockAspectRatio && !_updatingAspect && _lockedAspectRatio > 0)
        {
            _updatingAspect = true;
            ResizeHeight = Math.Max(1, (int)Math.Round(value / _lockedAspectRatio));
            _updatingAspect = false;
        }
    }

    partial void OnResizeHeightChanged(int value)
    {
        if (LockAspectRatio && !_updatingAspect && _lockedAspectRatio > 0)
        {
            _updatingAspect = true;
            ResizeWidth = Math.Max(1, (int)Math.Round(value * _lockedAspectRatio));
            _updatingAspect = false;
        }
    }

    public Visibility ResizePanelVisibility =>
        ResizeEnabled ? Visibility.Visible : Visibility.Collapsed;

    // JPEG選択時かつ透明ありファイルが存在する場合に警告
    public bool ShowTransparencyWarning =>
        SelectedOutputFormat == "JPEG" && Files.Any(f => f.HasTransparency);

    // ── リネーム設定 ──────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoRename))]
    [NotifyPropertyChangedFor(nameof(IsSuffixRename))]
    [NotifyPropertyChangedFor(nameof(IsReplaceRename))]
    [NotifyPropertyChangedFor(nameof(RenameSuffixPanelVisibility))]
    [NotifyPropertyChangedFor(nameof(RenameReplacePanelVisibility))]
    private RenameMode _renameMode = RenameMode.None;

    public bool IsNoRename
    {
        get => RenameMode == RenameMode.None;
        set { if (value) RenameMode = RenameMode.None; }
    }
    public bool IsSuffixRename
    {
        get => RenameMode == RenameMode.Suffix;
        set { if (value) RenameMode = RenameMode.Suffix; }
    }
    public bool IsReplaceRename
    {
        get => RenameMode == RenameMode.Replace;
        set { if (value) RenameMode = RenameMode.Replace; }
    }

    public Visibility RenameSuffixPanelVisibility =>
        RenameMode == RenameMode.Suffix ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RenameReplacePanelVisibility =>
        RenameMode == RenameMode.Replace ? Visibility.Visible : Visibility.Collapsed;

    public string[] SuffixPresets { get; } = ["_R", "_converted", "_new", "カスタム"];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenameSuffixCustomVisibility))]
    [NotifyPropertyChangedFor(nameof(RenameSuffixExample))]
    private string _renameSuffixPreset = "_R";

    public Visibility RenameSuffixCustomVisibility =>
        RenameSuffixPreset == "カスタム" ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenameSuffixExample))]
    private string _renameSuffixCustom = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RenameReplaceExample))]
    private string _renameReplaceString = "";

    private string ActualRenameSuffix =>
        RenameSuffixPreset == "カスタム" ? RenameSuffixCustom : RenameSuffixPreset;

    public string RenameSuffixExample
    {
        get
        {
            var suffix = ActualRenameSuffix;
            return string.IsNullOrEmpty(suffix)
                ? "例: photo.jpg（変更なし）"
                : $"例: photo{suffix}.jpg";
        }
    }

    public string RenameReplaceExample
    {
        get
        {
            var name = string.IsNullOrEmpty(RenameReplaceString) ? "ベース名" : RenameReplaceString;
            return $"例: {name}_001.jpg、{name}_002.jpg ...";
        }
    }

    // ── 出力先 ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UseCustomOutputFolder))]
    [NotifyPropertyChangedFor(nameof(CustomOutputRowVisibility))]
    private bool _useSubfolder = true;

    public bool UseCustomOutputFolder
    {
        get => !UseSubfolder;
        set => UseSubfolder = !value;
    }

    public Visibility CustomOutputRowVisibility =>
        UseCustomOutputFolder ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty] private string _customOutputFolder = "";

    // ── 処理状態 ──────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConvertButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(CancelButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(ShowProgressVisibility))]
    [NotifyCanExecuteChangedFor(nameof(StartConversionCommand))]
    private bool _isConverting;

    public Visibility ConvertButtonVisibility => IsConverting ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CancelButtonVisibility  => IsConverting ? Visibility.Visible   : Visibility.Collapsed;
    public Visibility ShowProgressVisibility  => IsConverting ? Visibility.Visible   : Visibility.Collapsed;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "ファイルを追加してください";

    // ── コマンド ──────────────────────────────────────────────────
    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        foreach (var ext in ConversionService.SupportedInputExtensions)
            picker.FileTypeFilter.Add(ext);
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));

        var files = await picker.PickMultipleFilesAsync();
        if (files == null) return;
        await AddDroppedPathsAsync(files.Select(f => f.Path));
    }

    [RelayCommand(CanExecute = nameof(CanClearAll))]
    private void ClearAll()
    {
        Files.Clear();
        FileCount = 0;
        OnPropertyChanged(nameof(ShowTransparencyWarning));
        StatusMessage = "ファイルを追加してください";
    }
    private bool CanClearAll() => FileCount > 0 && !IsConverting;

    [RelayCommand]
    private void RemoveFile(ImageItem item)
    {
        Files.Remove(item);
        FileCount = Files.Count;
        OnPropertyChanged(nameof(ShowTransparencyWarning));
    }

    [RelayCommand(CanExecute = nameof(CanStartConversion))]
    private async Task StartConversionAsync()
    {
        string? outputRoot = ResolveOutputRoot();
        if (outputRoot == null)
        {
            StatusMessage = "出力先フォルダを指定してください";
            return;
        }

        _cts = new CancellationTokenSource();
        IsConverting = true;
        ProgressValue = 0;
        StatusMessage = "変換中...";

        int done = 0, errors = 0;
        int total = Files.Count;
        int fileIndex = 0;

        // リネーム設定をスナップショット（変換中に変わらないよう）
        var renameMode     = RenameMode;
        var renameSuffix   = ActualRenameSuffix;
        var renameReplace  = RenameReplaceString;

        try
        {
            foreach (var item in Files)
            {
                _cts.Token.ThrowIfCancellationRequested();
                fileIndex++;

                string outDir = UseSubfolder
                    ? Path.Combine(Path.GetDirectoryName(item.FilePath)!, "output")
                    : outputRoot;
                Directory.CreateDirectory(outDir);

                item.Status = ConversionStatus.Converting;
                try
                {
                    await ConversionService.ConvertAsync(
                        item, SelectedOutputFormat,
                        ResizeEnabled, ResizeMode, ResizeWidth, ResizeHeight, LockAspectRatio, ResizePercent,
                        outDir,
                        renameMode, renameSuffix, renameReplace, fileIndex, total,
                        _cts.Token);
                    item.Status = ConversionStatus.Done;
                    done++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    item.Status = ConversionStatus.Error;
                    item.ErrorMessage = ex.Message;
                    errors++;
                    App.Log.Error(ex, item.FileName);
                }

                int processed = done + errors;
                _dispatcher.TryEnqueue(() =>
                {
                    ProgressValue = (double)processed / total * 100;
                    StatusMessage = $"変換中: {processed} / {total}";
                });
            }

            ProgressValue = 100;
            var errMsg = errors > 0 ? $"（エラー {errors} 件）" : "";
            StatusMessage = $"完了。{done} 件変換しました {errMsg}";
            App.Log.Info($"変換完了: {done}件成功, {errors}件エラー");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "キャンセルしました";
            foreach (var item in Files.Where(f => f.Status == ConversionStatus.Converting))
                item.Status = ConversionStatus.Pending;
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "変換処理");
            StatusMessage = $"エラー: {ex.Message}";
        }
        finally
        {
            IsConverting = false;
        }
    }
    private bool CanStartConversion() => FileCount > 0 && !IsConverting;

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private async Task BrowseOutputFolderAsync()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!));

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) CustomOutputFolder = folder.Path;
    }

    // ── ドロップ / ファイル追加 ───────────────────────────────────
    public async Task AddDroppedPathsAsync(IEnumerable<string> paths)
    {
        IsLoadingFiles = true;
        IsSkippedFilesWarningOpen = false;
        LoadingProgress = 0;
        LoadingTotal = 0;

        var pathsList = paths.ToList();
        // 既存パスはUIスレッドでキャプチャ
        var existingPaths = new HashSet<string>(Files.Select(f => f.FilePath));

        // Phase 1: パス列挙（高速）→ 合計件数確定
        var (validPaths, skippedUnsupported, skippedEmpty) =
            await Task.Run(() => EnumeratePaths(pathsList, existingPaths));

        LoadingTotal = validPaths.Count;

        // Phase 2: 透明度チェック（Progress<T>がUIスレッドに自動マーシャル）
        var progress = new Progress<int>(count => LoadingProgress = count);
        var items = await Task.Run(() => ProcessFiles(validPaths, progress));

        foreach (var item in items)
            Files.Add(item);

        FileCount = Files.Count;
        OnPropertyChanged(nameof(ShowTransparencyWarning));
        if (FileCount > 0)
            StatusMessage = $"{FileCount} 件のファイルが追加されています";

        IsLoadingFiles = false;
        LoadingProgress = 0;
        LoadingTotal = 0;

        int skippedTotal = skippedUnsupported + skippedEmpty;
        if (skippedTotal > 0)
        {
            SkippedFilesMessage = BuildSkippedMessage(skippedUnsupported, skippedEmpty);
            IsSkippedFilesWarningOpen = true;
        }
    }

    // Phase 1: パス列挙のみ（透明度チェックなし）
    private static (List<string> validPaths, int skippedUnsupported, int skippedEmpty)
        EnumeratePaths(List<string> paths, HashSet<string> existingPaths)
    {
        var validPaths = new List<string>();
        int skippedUnsupported = 0;
        int skippedEmpty = 0;

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (!ConversionService.SupportedInputExtensions.Contains(ext))
                {
                    skippedUnsupported++;
                    continue;
                }
                if (existingPaths.Contains(path)) continue;
                validPaths.Add(path);
                existingPaths.Add(path);
            }
            else if (Directory.Exists(path))
            {
                var dirFiles = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => ConversionService.SupportedInputExtensions.Contains(
                        Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                if (dirFiles.Count == 0)
                {
                    skippedEmpty++;
                    continue;
                }

                foreach (var filePath in dirFiles)
                {
                    if (existingPaths.Contains(filePath)) continue;
                    validPaths.Add(filePath);
                    existingPaths.Add(filePath);
                }
            }
        }

        return (validPaths, skippedUnsupported, skippedEmpty);
    }

    // Phase 2: 透明度チェック＋進捗報告
    private static List<ImageItem> ProcessFiles(List<string> paths, IProgress<int> progress)
    {
        var items = new List<ImageItem>();
        int count = 0;

        foreach (var path in paths)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            items.Add(new ImageItem
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                SourceFormat = ext.TrimStart('.').ToUpperInvariant(),
                HasTransparency = ConversionService.CheckTransparency(path)
            });
            progress.Report(++count);
        }

        return items;
    }

    private static string BuildSkippedMessage(int unsupported, int empty)
    {
        var parts = new List<string>();
        if (unsupported > 0) parts.Add($"{unsupported} 件は対応外の形式");
        if (empty > 0)       parts.Add($"{empty} 件は対象ファイルなしのフォルダ");
        return string.Join("、", parts) + "のため除外しました。";
    }

    private string? ResolveOutputRoot()
    {
        if (UseSubfolder) return "";
        if (string.IsNullOrWhiteSpace(CustomOutputFolder)) return null;
        return CustomOutputFolder;
    }

    // ── 設定の読み書き ────────────────────────────────────────────
    private void LoadFromSettings()
    {
        _updatingAspect = true; // ロード中はアスペクト比ロジックを無効化
        var s = App.Settings.Current;
        SelectedOutputFormat = s.OutputFormat;
        ResizeEnabled        = s.ResizeEnabled;
        ResizeMode           = s.ResizeMode;
        ResizeWidth          = s.ResizeWidth;
        ResizeHeight         = s.ResizeHeight;
        LockAspectRatio      = s.LockAspectRatio;
        ResizePercent        = s.ResizePercent;
        UseSubfolder         = s.UseSubfolder;
        CustomOutputFolder   = s.CustomOutputFolder;
        RenameMode           = s.RenameMode;
        RenameSuffixPreset   = s.RenameSuffixPreset;
        RenameSuffixCustom   = s.RenameSuffixCustom;
        RenameReplaceString  = s.RenameReplaceString;
        _updatingAspect = false;

        // ロード後に比率をキャプチャ
        if (LockAspectRatio && ResizeHeight > 0)
            _lockedAspectRatio = (double)ResizeWidth / ResizeHeight;
    }

    public void SaveToSettings()
    {
        var s = App.Settings.Current;
        s.OutputFormat       = SelectedOutputFormat;
        s.ResizeEnabled      = ResizeEnabled;
        s.ResizeMode         = ResizeMode;
        s.ResizeWidth        = ResizeWidth;
        s.ResizeHeight       = ResizeHeight;
        s.LockAspectRatio    = LockAspectRatio;
        s.ResizePercent      = ResizePercent;
        s.UseSubfolder       = UseSubfolder;
        s.CustomOutputFolder = CustomOutputFolder;
        s.RenameMode         = RenameMode;
        s.RenameSuffixPreset = RenameSuffixPreset;
        s.RenameSuffixCustom = RenameSuffixCustom;
        s.RenameReplaceString = RenameReplaceString;
        App.Settings.Save();
    }
}
