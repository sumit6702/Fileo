using System.IO;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class IcoConverter : IImageConverter
{
    private readonly ILogService _logService;

    public OutputFormatType Format => OutputFormatType.ICO;

    public IcoConverter(ILogService logService)
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

            var sizes = new List<uint>();
            if (settings.IcoSize16) sizes.Add(16);
            if (settings.IcoSize32) sizes.Add(32);
            if (settings.IcoSize48) sizes.Add(48);
            if (settings.IcoSize64) sizes.Add(64);
            if (settings.IcoSize128) sizes.Add(128);
            if (settings.IcoSize256) sizes.Add(256);

            if (sizes.Count == 0)
            {
                sizes.Add(32);
                sizes.Add(256);
            }

            using var baseImage = new MagickImage(inputPath);
            ImageProcessingHelper.ApplyCommonOperations(baseImage, settings);

            using var iconCollection = new MagickImageCollection();

            foreach (var size in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var layer = baseImage.Clone();
                layer.Format = MagickFormat.Ico;
                layer.FilterType = FilterType.Lanczos;
                layer.Resize(new MagickGeometry(size, size) { IgnoreAspectRatio = false });
                
                // If aspect ratio is not 1:1, extend onto transparent canvas
                if (layer.Width != size || layer.Height != size)
                {
                    layer.Extent(new MagickGeometry(size, size), Gravity.Center, MagickColors.Transparent);
                }

                iconCollection.Add(layer);
            }

            await iconCollection.WriteAsync(outputPath, MagickFormat.Ico, cancellationToken);

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
            _logService.LogError($"ICO conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
    }
}
