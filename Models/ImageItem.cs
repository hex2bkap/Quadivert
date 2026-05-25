using CommunityToolkit.Mvvm.ComponentModel;

namespace PixShift.Models;

public enum ConversionStatus { Pending, Converting, Done, Error }

public partial class ImageItem : ObservableObject
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string SourceFormat { get; set; } = "";
    public bool HasTransparency { get; set; } = false;

    [ObservableProperty]
    private ConversionStatus _status = ConversionStatus.Pending;

    [ObservableProperty]
    private string _errorMessage = "";

    public string StatusIcon => Status switch
    {
        ConversionStatus.Done => "",
        ConversionStatus.Error => "",
        ConversionStatus.Converting => "",
        _ => ""
    };

    public string DisplayText => HasTransparency
        ? $"{FileName}  [{SourceFormat}]  透明あり"
        : $"{FileName}  [{SourceFormat}]";
}
