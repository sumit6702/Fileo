using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class JpegConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.JPEG;

    public JpegConverter(ILogService logService)
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

            // Transparency flattening
            ImageProcessingHelper.FlattenTransparency(image, settings.JpegBackgroundColorHex);

            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)Math.Clamp(settings.JpegQuality, 1, 100);

            image.Settings.Interlace = settings.JpegProgressive ? Interlace.Line : Interlace.NoInterlace;

            if (!string.IsNullOrWhiteSpace(settings.JpegChromaSubsampling))
            {
                image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", settings.JpegChromaSubsampling);
            }

            if (settings.JpegOptimizeHuffman)
            {
                image.Settings.SetDefine(MagickFormat.Jpeg, "optimize-coding", "true");
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
            _logService.LogError($"JPEG conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
