using System.Text.Json.Serialization;

namespace ImageBatchConverter.Models;

public class ConversionSettings
{
    // Common / Output
    public OutputFormatType TargetFormat { get; set; } = OutputFormatType.WebP;
    public bool SaveInSameFolder { get; set; } = true;

    [JsonIgnore]
    public bool SaveInSeparateFolder
    {
        get => !SaveInSameFolder;
        set => SaveInSameFolder = !value;
    }

    public string CustomOutputFolder { get; set; } = string.Empty;
    public OverwriteBehavior OverwriteMode { get; set; } = OverwriteBehavior.AutoRename;
    public string Prefix { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;

    // Backup & Safety
    public bool BackupOriginals { get; set; } = true;
    public string BackupFolder { get; set; } = string.Empty;
    public bool DeleteOriginalsAfterSuccess { get; set; } = false;
    public bool KeepOriginalIfLarger { get; set; } = true;
    public bool SkipIfAlreadyTargetFormat { get; set; } = true;
    public int WorkerCount { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    // Common Processing
    public bool EnableResize { get; set; } = false;
    public int ResizeWidth { get; set; } = 1920;
    public int ResizeHeight { get; set; } = 1080;
    public bool KeepAspectRatio { get; set; } = true;
    public bool OnlyShrinkNeverEnlarge { get; set; } = true;
    public string ResampleFilter { get; set; } = "Lanczos";

    public MetadataMode MetadataMode { get; set; } = MetadataMode.KeepAll;
    public bool AutoRotate { get; set; } = true;

    // WebP Settings (cwebp flags)
    public int WebPQuality { get; set; } = 80;
    public bool WebPLossless { get; set; } = false;
    public int WebPNearLossless { get; set; } = 100; // 100 means off
    public int WebPMethod { get; set; } = 4; // 0-6
    public string WebPPreset { get; set; } = "default"; // default, picture, photo, drawing, icon, text
    public int WebPAlphaQuality { get; set; } = 100; // 0-100
    public bool WebPExact { get; set; } = false;
    public bool WebPSharpYuv { get; set; } = false;
    public int WebPFilterStrength { get; set; } = 0; // 0-100
    public int WebPSharpness { get; set; } = 0; // 0-7
    public bool WebPMultithread { get; set; } = true;
    public string WebPMetadata { get; set; } = "all"; // all, none, exif, icc
    public long WebPTargetSize { get; set; } = 0; // bytes, 0 = off

    // AVIF Settings
    public int AvifQuality { get; set; } = 75; // 0-100
    public int AvifSpeedEffort { get; set; } = 4; // 0-10
    public bool AvifLossless { get; set; } = false;
    public string AvifChromaSubsampling { get; set; } = "4:2:0"; // 4:2:0, 4:2:2, 4:4:4
    public int AvifBitDepth { get; set; } = 8; // 8, 10, 12
    public string AvifBitDepthString
    {
        get => AvifBitDepth.ToString();
        set => AvifBitDepth = int.TryParse(value, out int v) ? v : 8;
    }

    // JPEG Settings
    public int JpegQuality { get; set; } = 85; // 1-100
    public bool JpegProgressive { get; set; } = true;
    public string JpegChromaSubsampling { get; set; } = "4:2:0";
    public bool JpegOptimizeHuffman { get; set; } = true;
    public string JpegBackgroundColorHex { get; set; } = "#FFFFFF";

    // PNG Settings
    public int PngCompressionLevel { get; set; } = 6; // 0-9
    public bool PngInterlaced { get; set; } = false;
    public bool PngQuantize8Bit { get; set; } = false;
    public int PngQuantizeColors { get; set; } = 256;

    // JPEG XL Settings
    public int JxlQuality { get; set; } = 85;
    public double JxlDistance { get; set; } = 1.0;
    public bool JxlLossless { get; set; } = false;
    public int JxlEffort { get; set; } = 7; // 1-9

    // TIFF Settings
    public string TiffCompression { get; set; } = "LZW"; // None, LZW, ZIP, JPEG

    // BMP Settings
    public string BmpBackgroundColorHex { get; set; } = "#FFFFFF";

    // GIF Settings
    public int GifColorCount { get; set; } = 256;
    public bool GifDithering { get; set; } = true;
    public bool GifKeepAnimation { get; set; } = true;

    // ICO Settings
    public bool IcoSize16 { get; set; } = true;
    public bool IcoSize32 { get; set; } = true;
    public bool IcoSize48 { get; set; } = true;
    public bool IcoSize64 { get; set; } = false;
    public bool IcoSize128 { get; set; } = false;
    public bool IcoSize256 { get; set; } = true;

    public ConversionSettings Clone()
    {
        return (ConversionSettings)MemberwiseClone();
    }
}
