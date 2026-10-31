using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public interface IScanService
{
    Task<(List<ImageFileInfo> Files, ScanSummary Summary)> ScanPathsAsync(
        IEnumerable<string> paths,
        bool includeSubfolders,
        HashSet<string> allowedFormats,
        bool allSupported,
        bool skipTargetFormat,
        OutputFormatType targetFormat,
        CancellationToken cancellationToken = default);
}
