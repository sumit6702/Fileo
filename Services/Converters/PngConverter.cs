using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class PngConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.PNG;

    public PngConverter(ILogService logService)
    {
        _logService = logService;
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            using var image = new MagickImage(inputPath);
            ImageProcessingHelper.ApplyCommonOperations(image, settings);

            image.Format = MagickFormat.Png;

            // Compression level 0-9
            image.Settings.SetDefine(MagickFormat.Png, "compression-level", Math.Clamp(settings.PngCompressionLevel, 0, 9).ToString());

            image.Settings.Interlace = settings.PngInterlaced ? Interlace.Line : Interlace.NoInterlace;

            // Optional 8-bit palette quantization (pngquant style)
            if (settings.PngQuantize8Bit)
            {
                var qSettings = new QuantizeSettings
                {
                    Colors = (uint)Math.Clamp(settings.PngQuantizeColors, 2, 256),
                    DitherMethod = DitherMethod.Riemersma
                };
                image.Quantize(qSettings);
            }

            await image.WriteAsync(outputPath, cancellationToken);

            var fi = new FileInfo(outputPath);
            return new ConversionResult(true, outputPath, fi.Length, null);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath)) { try { File.Delete(outputPath); } catch { } }
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"PNG conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
