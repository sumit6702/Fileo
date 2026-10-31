using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class GifConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.GIF;

    public GifConverter(ILogService logService)
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

            // Check if multi-frame / animated
            if (settings.GifKeepAnimation)
            {
                using var collection = new MagickImageCollection(inputPath);
                if (collection.Count > 1)
                {
                    collection.Coalesce();

                    foreach (var frame in collection)
                    {
                        ImageProcessingHelper.ApplyCommonOperations(frame, settings);

                        var qSettings = new QuantizeSettings
                        {
                            Colors = (uint)Math.Clamp(settings.GifColorCount, 2, 256),
                            DitherMethod = settings.GifDithering ? DitherMethod.FloydSteinberg : DitherMethod.No
                        };
                        frame.Quantize(qSettings);
                    }

                    collection.Optimize();
                    await collection.WriteAsync(outputPath, MagickFormat.Gif, cancellationToken);

                    var fiAnim = new FileInfo(outputPath);
                    return new ConversionResult(true, outputPath, fiAnim.Length, null);
                }
            }

            // Single image or first frame only
            using var singleImage = new MagickImage(inputPath);
            ImageProcessingHelper.ApplyCommonOperations(singleImage, settings);

            var quantize = new QuantizeSettings
            {
                Colors = (uint)Math.Clamp(settings.GifColorCount, 2, 256),
                DitherMethod = settings.GifDithering ? DitherMethod.FloydSteinberg : DitherMethod.No
            };
            singleImage.Quantize(quantize);

            singleImage.Format = MagickFormat.Gif;
            await singleImage.WriteAsync(outputPath, cancellationToken);

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
            _logService.LogError($"GIF conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
