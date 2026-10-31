using System.IO;
using System.Security.Cryptography;

namespace ImageBatchConverter.Services;

public class BackupService : IBackupService
{
    private readonly ILogService _logService;

    public BackupService(ILogService logService)
    {
        _logService = logService;
    }

    public string GenerateDefaultBackupFolderName(string? baseFolderPath)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string backupDirName = $"Backup_{timestamp}";

        if (!string.IsNullOrWhiteSpace(baseFolderPath) && Directory.Exists(baseFolderPath))
        {
            var dirInfo = new DirectoryInfo(baseFolderPath);
            string? parent = dirInfo.Parent?.FullName;
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                return Path.Combine(parent, backupDirName);
            }
            return Path.Combine(baseFolderPath, backupDirName);
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, backupDirName);
    }

    public async Task<(bool Success, string? BackupPath, string? ErrorMessage)> CreateBackupAsync(
        string originalPath,
        string relativePath,
        string backupRootFolder,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(originalPath))
            {
                return (false, null, "Original file does not exist");
            }

            string targetBackupPath = Path.Combine(backupRootFolder, relativePath);
            string? targetDirectory = Path.GetDirectoryName(targetBackupPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Copy file
            using (var sourceStream = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            using (var destStream = new FileStream(targetBackupPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                await sourceStream.CopyToAsync(destStream, cancellationToken);
            }

            // Verify backup copy
            var origInfo = new FileInfo(originalPath);
            var backupInfo = new FileInfo(targetBackupPath);

            if (!backupInfo.Exists || backupInfo.Length != origInfo.Length)
            {
                return (false, targetBackupPath, $"Backup size mismatch: expected {origInfo.Length} bytes, got {backupInfo.Length} bytes.");
            }

            // Checksum verification
            using (var sha = SHA256.Create())
            {
                byte[] origHash;
                using (var fs1 = File.OpenRead(originalPath))
                {
                    origHash = await sha.ComputeHashAsync(fs1, cancellationToken);
                }

                byte[] backupHash;
                using (var fs2 = File.OpenRead(targetBackupPath))
                {
                    backupHash = await sha.ComputeHashAsync(fs2, cancellationToken);
                }

                if (!origHash.SequenceEqual(backupHash))
                {
                    return (false, targetBackupPath, "Backup checksum verification failed.");
                }
            }

            _logService.LogInfo($"Backup created and verified: {originalPath} -> {targetBackupPath}");
            return (true, targetBackupPath, null);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to backup {originalPath}", ex);
            return (false, null, ex.Message);
        }
    }

    public bool SafeDeleteOriginal(
        string originalPath,
        bool backupEnabled,
        bool backupVerified,
        bool conversionSuccess,
        string? outputPath)
    {
        // Safety Rule 1: Never delete if backup was enabled but verification failed
        if (backupEnabled && !backupVerified)
        {
            _logService.LogWarning($"Refusing to delete original: Backup was enabled but not verified for {originalPath}");
            return false;
        }

        // Safety Rule 2: Never delete if conversion did not succeed
        if (!conversionSuccess)
        {
            _logService.LogWarning($"Refusing to delete original: Conversion failed for {originalPath}");
            return false;
        }

        // Safety Rule 3: Never delete if output file does not exist or size <= 0
        if (string.IsNullOrWhiteSpace(outputPath) || !File.Exists(outputPath))
        {
            _logService.LogWarning($"Refusing to delete original: Output file does not exist: {outputPath}");
            return false;
        }

        var outInfo = new FileInfo(outputPath);
        if (outInfo.Length <= 0)
        {
            _logService.LogWarning($"Refusing to delete original: Output file is 0 bytes: {outputPath}");
            return false;
        }

        try
        {
            // Clear read-only attribute if present
            var attributes = File.GetAttributes(originalPath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(originalPath, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(originalPath);
            _logService.LogInfo($"Original file deleted after verified conversion: {originalPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Could not delete original file {originalPath}", ex);
            return false;
        }
    }
}
