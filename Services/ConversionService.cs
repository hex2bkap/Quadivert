using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Quadivert.Models;
using ResizeMode = Quadivert.Models.ResizeMode;

namespace Quadivert.Services;

public class ConversionService
{
    public static readonly string[] SupportedInputExtensions =
        [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".tif", ".gif", ".heic", ".heif"];

    public static readonly string[] OutputFormats =
        ["PNG", "JPEG", "WebP", "BMP", "TIFF", "GIF", "ICO"];

    // 透明度を保持できる出力形式
    private static readonly HashSet<string> AlphaFormats =
        ["PNG", "WebP", "ICO"];

    public static bool SupportsAlpha(string outputFormat) =>
        AlphaFormats.Contains(outputFormat.ToUpperInvariant());

    /// <summary>ファイルを読み込んで透明ピクセルが含まれるか確認します。</summary>
    public static bool CheckTransparency(string filePath)
    {
        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".heic" or ".heif") return false; // HEICは透明度なし

            using var image = Image.Load<Rgba32>(filePath);
            // ピクセルをサンプリングして半透明・透明ピクセルの有無を確認
            for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / 64))
            {
                for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / 64))
                {
                    if (image[x, y].A < 255) return true;
                }
            }
            return false;
        }
        catch { return false; }
    }

    /// <summary>1ファイルを変換します。</summary>
    public static async Task ConvertAsync(
        ImageItem item,
        string outputFormat,
        bool resizeEnabled,
        ResizeMode resizeMode,
        int targetWidth,
        int targetHeight,
        bool lockAspect,
        double resizePercent,
        string outputDir,
        RenameMode renameMode,
        string renameSuffix,
        string renameReplaceString,
        int fileIndex,
        int totalFiles,
        CancellationToken ct)
    {
        var ext = GetExtension(outputFormat);
        var baseName = Path.GetFileNameWithoutExtension(item.FileName);
        var outputBaseName = BuildOutputBaseName(
            baseName, renameMode, renameSuffix, renameReplaceString, fileIndex, totalFiles);
        var outputPath = GetUniqueOutputPath(outputDir, outputBaseName, ext);

        var srcExt = Path.GetExtension(item.FilePath).ToLowerInvariant();
        if (srcExt is ".heic" or ".heif")
        {
            await ConvertHeicAsync(item.FilePath, outputPath, outputFormat,
                resizeEnabled, resizeMode, targetWidth, targetHeight, lockAspect, resizePercent, ct);
        }
        else
        {
            using var image = await Image.LoadAsync<Rgba32>(item.FilePath, ct);
            ApplyResize(image, resizeEnabled, resizeMode, targetWidth, targetHeight, lockAspect, resizePercent);
            await SaveImageAsync(image, outputPath, outputFormat, item.HasTransparency, ct);
        }
    }

    /// <summary>リネームモードに応じた出力ベース名を生成します。</summary>
    private static string BuildOutputBaseName(
        string originalBaseName,
        RenameMode renameMode,
        string renameSuffix,
        string renameReplaceString,
        int fileIndex,
        int totalFiles)
    {
        if (renameMode == RenameMode.Suffix && !string.IsNullOrEmpty(renameSuffix))
            return originalBaseName + renameSuffix;

        if (renameMode == RenameMode.Replace && !string.IsNullOrEmpty(renameReplaceString))
        {
            int digits = Math.Max(1, totalFiles.ToString().Length);
            return renameReplaceString + "_" + fileIndex.ToString().PadLeft(digits, '0');
        }

        return originalBaseName;
    }

    private static void ApplyResize(
        Image image, bool enabled, ResizeMode mode, int w, int h, bool lockAspect, double percent)
    {
        if (!enabled) return;

        if (mode == ResizeMode.Percent)
        {
            if (percent <= 0) return;
            int newW = Math.Max(1, (int)(image.Width  * percent / 100.0));
            int newH = Math.Max(1, (int)(image.Height * percent / 100.0));
            image.Mutate(ctx => ctx.Resize(newW, newH));
            return;
        }

        // ピクセル指定
        if (w <= 0 || h <= 0) return;
        if (lockAspect)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(w, h),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
            }));
        }
        else
        {
            image.Mutate(ctx => ctx.Resize(w, h));
        }
    }

    private static async Task SaveImageAsync(
        Image<Rgba32> image, string outputPath, string format, bool hasAlpha, CancellationToken ct)
    {
        if (format == "ICO")
        {
            await SaveAsIcoAsync(image, outputPath, ct);
            return;
        }

        // JPEGは透明度なし → 白背景に合成してから保存
        if (format == "JPEG")
        {
            using var bg = new Image<Rgba32>(image.Width, image.Height, new Rgba32(255, 255, 255, 255));
            bg.Mutate(ctx => ctx.DrawImage(image, 1f));
            await bg.SaveAsJpegAsync(outputPath, new JpegEncoder { Quality = 90 }, ct);
            return;
        }

        switch (format)
        {
            case "PNG":
                await image.SaveAsPngAsync(outputPath, ct);
                break;
            case "WebP":
                await image.SaveAsWebpAsync(outputPath, new WebpEncoder { Quality = 90 }, ct);
                break;
            case "BMP":
                await image.SaveAsBmpAsync(outputPath, ct);
                break;
            case "TIFF":
                await image.SaveAsTiffAsync(outputPath, ct);
                break;
            case "GIF":
                await image.SaveAsGifAsync(outputPath, ct);
                break;
        }
    }

    private static async Task SaveAsIcoAsync(Image<Rgba32> source, string outputPath, CancellationToken ct)
    {
        int[] sizes = [16, 32, 48, 256];
        var pngDatas = new List<byte[]>();

        foreach (var size in sizes)
        {
            ct.ThrowIfCancellationRequested();
            using var resized = source.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad
            }));
            using var ms = new MemoryStream();
            await resized.SaveAsPngAsync(ms, ct);
            pngDatas.Add(ms.ToArray());
        }

        await using var fs = File.Create(outputPath);
        await using var bw = new BinaryWriter(fs);

        // ICOヘッダー
        bw.Write((short)0);           // Reserved
        bw.Write((short)1);           // Type: ICO
        bw.Write((short)sizes.Length);// 画像数

        // ディレクトリエントリのオフセット計算
        int dataOffset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            var size = sizes[i];
            var data = pngDatas[i];
            bw.Write((byte)(size == 256 ? 0 : size)); // 幅 (256=0)
            bw.Write((byte)(size == 256 ? 0 : size)); // 高さ (256=0)
            bw.Write((byte)0);   // カラー数
            bw.Write((byte)0);   // Reserved
            bw.Write((short)1);  // プレーン数
            bw.Write((short)32); // ビット深度
            bw.Write(data.Length);
            bw.Write(dataOffset);
            dataOffset += data.Length;
        }

        foreach (var data in pngDatas)
            bw.Write(data);
    }

    // HEICをWindowsコーデック経由で読み込んでから変換
    private static async Task ConvertHeicAsync(
        string inputPath, string outputPath, string outputFormat,
        bool resizeEnabled, ResizeMode resizeMode, int targetWidth, int targetHeight,
        bool lockAspect, double resizePercent, CancellationToken ct)
    {
        using var fileStream = File.OpenRead(inputPath);
        var randomAccessStream = fileStream.AsRandomAccessStream();
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied);

        using var pngStream = new MemoryStream();
        var randomMs = pngStream.AsRandomAccessStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, randomMs);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        pngStream.Position = 0;

        using var image = await Image.LoadAsync<Rgba32>(pngStream, ct);
        ApplyResize(image, resizeEnabled, resizeMode, targetWidth, targetHeight, lockAspect, resizePercent);
        await SaveImageAsync(image, outputPath, outputFormat, false, ct);
    }

    private static string GetExtension(string format) => format switch
    {
        "JPEG" => ".jpg",
        "ICO"  => ".ico",
        _      => $".{format.ToLowerInvariant()}"
    };

    private static string GetUniqueOutputPath(string dir, string baseName, string ext)
    {
        var path = Path.Combine(dir, baseName + ext);
        if (!File.Exists(path)) return path;
        int n = 2;
        while (true)
        {
            path = Path.Combine(dir, $"{baseName}_{n}{ext}");
            if (!File.Exists(path)) return path;
            n++;
        }
    }
}
