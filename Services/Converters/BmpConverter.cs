using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class BmpConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.BMP;

    public BmpConverter(ILogService logService)
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

            // Flatten transparency for BMP
            ImageProcessingHelper.FlattenTransparency(image, settings.BmpBackgroundColorHex);

            image.Format = MagickFormat.Bmp;

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
            _logService.LogError($"BMP conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
