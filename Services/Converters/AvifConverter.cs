using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class AvifConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.AVIF;

    public AvifConverter(ILogService logService)
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

            image.Format = MagickFormat.Avif;
            image.Quality = (uint)Math.Clamp(settings.AvifQuality, 0, 100);

            // Effort / speed: 0 (slowest/best) to 10 (fastest)
            image.Settings.SetDefine(MagickFormat.Avif, "speed", Math.Clamp(settings.AvifSpeedEffort, 0, 10).ToString());

            if (settings.AvifLossless)
            {
                image.Settings.SetDefine(MagickFormat.Avif, "lossless", "true");
            }

            if (!string.IsNullOrWhiteSpace(settings.AvifChromaSubsampling))
            {
                image.Settings.SetDefine(MagickFormat.Avif, "chroma-subsample", settings.AvifChromaSubsampling);
            }

            if (settings.AvifBitDepth is 8 or 10 or 12)
            {
                image.Depth = (uint)settings.AvifBitDepth;
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
            _logService.LogError($"AVIF conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
