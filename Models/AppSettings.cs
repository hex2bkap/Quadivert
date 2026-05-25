namespace PixShift.Models;

public enum ResizeMode { Pixel, Percent }
public enum RenameMode { None, Suffix, Replace }

public class AppSettings
{
    public string OutputFormat { get; set; } = "PNG";
    public bool ResizeEnabled { get; set; } = false;
    public ResizeMode ResizeMode { get; set; } = ResizeMode.Pixel;
    public int ResizeWidth { get; set; } = 1920;
    public int ResizeHeight { get; set; } = 1080;
    public bool LockAspectRatio { get; set; } = true;
    public double ResizePercent { get; set; } = 100.0;
    public bool UseSubfolder { get; set; } = true;
    public string CustomOutputFolder { get; set; } = "";

    // リネーム設定
    public RenameMode RenameMode { get; set; } = RenameMode.None;
    public string RenameSuffixPreset { get; set; } = "_R";
    public string RenameSuffixCustom { get; set; } = "";
    public string RenameReplaceString { get; set; } = "";

    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public int WindowWidth { get; set; } = 680;
    public int WindowHeight { get; set; } = 860;
}
