using System.IO;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public class ScanService : IScanService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".avif", ".heic", ".heif", ".tiff", ".tif", ".bmp", ".gif", ".jxl", ".ico"
    };

    public Task<(List<ImageFileInfo> Files, ScanSummary Summary)> ScanPathsAsync(
        IEnumerable<string> paths,
        bool includeSubfolders,
        HashSet<string> allowedFormats,
        bool allSupported,
        bool skipTargetFormat,
        OutputFormatType targetFormat,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var filesList = new List<ImageFileInfo>();
            var summary = new ScanSummary();
            var targetExt = FormatExtensions.GetExtensionForOutput(targetFormat);

            var pathsList = paths.ToList();
            if (pathsList.Count == 0) return (filesList, summary);

            string? primaryRootFolder = null;
            if (pathsList.Count == 1 && Directory.Exists(pathsList[0]))
            {
                primaryRootFolder = pathsList[0];
            }

            foreach (var path in pathsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(path))
                {
                    var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var dirInfo = new DirectoryInfo(path);
                    
                    try
                    {
                        var foundFiles = dirInfo.EnumerateFiles("*.*", searchOption);
                        foreach (var file in foundFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            ProcessCandidateFile(file, path, targetExt, allowedFormats, allSupported, skipTargetFormat, filesList, summary);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip inaccessible folders
                    }
                }
                else if (File.Exists(path))
                {
                    var file = new FileInfo(path);
                    string baseFolder = file.DirectoryName ?? string.Empty;
                    ProcessCandidateFile(file, baseFolder, targetExt, allowedFormats, allSupported, skipTargetFormat, filesList, summary);
                }
            }

            summary.TotalFiles = filesList.Count;
            summary.TotalSizeBytes = filesList.Sum(f => f.FileSizeBytes);

            return (filesList, summary);
        }, cancellationToken);
    }

    private static void ProcessCandidateFile(
        FileInfo file,
        string baseFolder,
        string targetExt,
        HashSet<string> allowedFormats,
        bool allSupported,
        bool skipTargetFormat,
        List<ImageFileInfo> filesList,
        ScanSummary summary)
    {
        string ext = file.Extension;
        if (string.IsNullOrEmpty(ext) || !SupportedExtensions.Contains(ext))
            return;

        if (skipTargetFormat && ext.Equals(targetExt, StringComparison.OrdinalIgnoreCase))
            return;

        if (!FormatExtensions.ExtensionMap.TryGetValue(ext, out var formatName))
        {
            formatName = ext.TrimStart('.').ToUpperInvariant();
        }

        if (!allSupported && !allowedFormats.Contains(formatName))
            return;

        string relativePath;
        try
        {
            if (!string.IsNullOrEmpty(baseFolder) && file.FullName.StartsWith(baseFolder, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = Path.GetRelativePath(baseFolder, file.FullName);
            }
            else
            {
                relativePath = file.Name;
            }
        }
        catch
        {
            relativePath = file.Name;
        }

        bool isGif = ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);

        var item = new ImageFileInfo
        {
            FullPath = file.FullName,
            RelativePath = relativePath,
            FileName = file.Name,
            Extension = ext,
            FormatName = formatName,
            FileSizeBytes = file.Length,
            IsAnimatedGif = isGif,
            HasTransparency = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                             ext.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
                             ext.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                             ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)
        };

        filesList.Add(item);

        if (!summary.CountByFormat.TryGetValue(formatName, out int currentCount))
        {
            summary.CountByFormat[formatName] = 1;
            summary.SizeByFormat[formatName] = file.Length;
        }
        else
        {
            summary.CountByFormat[formatName] = currentCount + 1;
            summary.SizeByFormat[formatName] += file.Length;
        }
    }
}
