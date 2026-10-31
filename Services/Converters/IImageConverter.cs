using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public record ConversionResult(bool Success, string? OutputPath, long OutputSizeBytes, string? ErrorMessage);

public interface IImageConverter
{
    OutputFormatType Format { get; }
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken);
}
