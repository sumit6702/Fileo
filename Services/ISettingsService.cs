using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public interface ISettingsService
{
    Task<ConversionSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(ConversionSettings settings);
    Task<List<PresetModel>> GetPresetsAsync();
    Task SavePresetAsync(PresetModel preset);
    Task DeletePresetAsync(string presetName);
}
