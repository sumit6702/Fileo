namespace ImageBatchConverter.Models;

public class PresetModel
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required ConversionSettings Settings { get; set; }
    public bool IsBuiltIn { get; set; }
}
