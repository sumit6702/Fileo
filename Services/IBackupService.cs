namespace ImageBatchConverter.Services;

public interface IBackupService
{
    string GenerateDefaultBackupFolderName(string? baseFolderPath);
    Task<(bool Success, string? BackupPath, string? ErrorMessage)> CreateBackupAsync(
        string originalPath,
        string relativePath,
        string backupRootFolder,
        CancellationToken cancellationToken = default);

    bool SafeDeleteOriginal(
        string originalPath,
        bool backupEnabled,
        bool backupVerified,
        bool conversionSuccess,
        string? outputPath);
}
