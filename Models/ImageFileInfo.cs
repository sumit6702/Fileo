namespace ImageBatchConverter.Models;

public class ImageFileInfo
{
    public required string FullPath { get; set; }
    public required string RelativePath { get; set; }
    public required string FileName { get; set; }
    public required string Extension { get; set; }
    public required string FormatName { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsAnimatedGif { get; set; }
    public bool HasTransparency { get; set; }
}
