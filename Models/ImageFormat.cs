namespace ImageBatchConverter.Models;

public enum OutputFormatType
{
    WebP,
    AVIF,
    JPEG,
    PNG,
    JPEGXL,
    TIFF,
    BMP,
    GIF,
    ICO
}

public enum OverwriteBehavior
{
    Skip,
    Overwrite,
    AutoRename
}

public enum MetadataMode
{
    KeepAll,
    StripAll,
    KeepExif,
    KeepIcc
}

public enum ConversionStatus
{
    Pending,
    Converting,
    Success,
    Skipped,
    Failed
}

public static class FormatExtensions
{
    public static readonly Dictionary<string, string> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".png", "PNG" },
        { ".jpg", "JPG" },
        { ".jpeg", "JPG" },
        { ".webp", "WebP" },
        { ".avif", "AVIF" },
        { ".heic", "HEIC" },
        { ".heif", "HEIC" },
        { ".tiff", "TIFF" },
        { ".tif", "TIFF" },
        { ".bmp", "BMP" },
        { ".gif", "GIF" },
        { ".jxl", "JPEG XL" },
        { ".ico", "ICO" }
    };

    public static string GetExtensionForOutput(OutputFormatType format) => format switch
    {
        OutputFormatType.WebP => ".webp",
        OutputFormatType.AVIF => ".avif",
        OutputFormatType.JPEG => ".jpg",
        OutputFormatType.PNG => ".png",
        OutputFormatType.JPEGXL => ".jxl",
        OutputFormatType.TIFF => ".tiff",
        OutputFormatType.BMP => ".bmp",
        OutputFormatType.GIF => ".gif",
        OutputFormatType.ICO => ".ico",
        _ => ".webp"
    };

    public static string GetFormatDisplayName(OutputFormatType format) => format switch
    {
        OutputFormatType.WebP => "WebP",
        OutputFormatType.AVIF => "AVIF",
        OutputFormatType.JPEG => "JPEG",
        OutputFormatType.PNG => "PNG",
        OutputFormatType.JPEGXL => "JPEG XL",
        OutputFormatType.TIFF => "TIFF",
        OutputFormatType.BMP => "BMP",
        OutputFormatType.GIF => "GIF",
        OutputFormatType.ICO => "ICO",
        _ => format.ToString()
    };
}
