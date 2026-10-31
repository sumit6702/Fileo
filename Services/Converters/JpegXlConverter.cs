using System.Globalization;
using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class JpegXlConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.JPEGXL;

    public JpegXlConverter(ILogService logService)
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

            image.Format = MagickFormat.Jxl;

            if (settings.JxlLossless)
            {
                image.Settings.SetDefine(MagickFormat.Jxl, "lossless", "true");
            }
            else
            {
                image.Quality = (uint)Math.Clamp(settings.JxlQuality, 1, 100);
                if (settings.JxlDistance > 0)
                {
                    image.Settings.SetDefine(MagickFormat.Jxl, "distance", settings.JxlDistance.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }

            image.Settings.SetDefine(MagickFormat.Jxl, "effort", Math.Clamp(settings.JxlEffort, 1, 9).ToString());

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
            _logService.LogError($"JPEG XL conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
