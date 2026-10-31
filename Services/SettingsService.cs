using System.IO;
using System.Text.Json;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFolder;
    private readonly string _settingsFilePath;
    private readonly string _presetsFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _settingsFolder = Path.Combine(appData, "Fileo");
        Directory.CreateDirectory(_settingsFolder);
        _settingsFilePath = Path.Combine(_settingsFolder, "settings.json");
        _presetsFilePath = Path.Combine(_settingsFolder, "presets.json");
    }

    public async Task<ConversionSettings> LoadSettingsAsync()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<ConversionSettings>(json, _jsonOptions);
                if (settings != null) return settings;
            }
            catch
            {
                // Fall back to defaults on corrupt settings
            }
        }
        return new ConversionSettings();
    }

    public async Task SaveSettingsAsync(ConversionSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch
        {
            // Ignore write failures
        }
    }

    public async Task<List<PresetModel>> GetPresetsAsync()
    {
        var presets = GetBuiltInPresets();

        if (File.Exists(_presetsFilePath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(_presetsFilePath);
                var userPresets = JsonSerializer.Deserialize<List<PresetModel>>(json, _jsonOptions);
                if (userPresets != null)
                {
                    foreach (var up in userPresets)
                    {
                        up.IsBuiltIn = false;
                        presets.Add(up);
                    }
                }
            }
            catch
            {
                // Ignore corrupt custom presets file
            }
        }

        return presets;
    }

    public async Task SavePresetAsync(PresetModel preset)
    {
        var existing = await GetCustomPresetsOnlyAsync();
        existing.RemoveAll(p => p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        preset.IsBuiltIn = false;
        existing.Add(preset);

        string json = JsonSerializer.Serialize(existing, _jsonOptions);
        await File.WriteAllTextAsync(_presetsFilePath, json);
    }

    public async Task DeletePresetAsync(string presetName)
    {
        var existing = await GetCustomPresetsOnlyAsync();
        existing.RemoveAll(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
        string json = JsonSerializer.Serialize(existing, _jsonOptions);
        await File.WriteAllTextAsync(_presetsFilePath, json);
    }

    private async Task<List<PresetModel>> GetCustomPresetsOnlyAsync()
    {
        if (!File.Exists(_presetsFilePath)) return new List<PresetModel>();
        try
        {
            string json = await File.ReadAllTextAsync(_presetsFilePath);
            return JsonSerializer.Deserialize<List<PresetModel>>(json, _jsonOptions) ?? new List<PresetModel>();
        }
        catch
        {
            return new List<PresetModel>();
        }
    }

    private static List<PresetModel> GetBuiltInPresets()
    {
        return new List<PresetModel>
        {
            new PresetModel
            {
                Name = "Banners - Web",
                Description = "WebP format, 80% quality, Lanczos resize to 1920px max width, metadata stripped for quick page loads.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.WebP,
                    WebPQuality = 80,
                    WebPMethod = 5,
                    WebPSharpYuv = true,
                    EnableResize = true,
                    ResizeWidth = 1920,
                    ResizeHeight = 1080,
                    KeepAspectRatio = true,
                    OnlyShrinkNeverEnlarge = true,
                    MetadataMode = MetadataMode.StripAll
                }
            },
            new PresetModel
            {
                Name = "Max Quality",
                Description = "WebP Lossless encoding, highest effort, exact transparent RGB values preserved, all EXIF/ICC metadata retained.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.WebP,
                    WebPLossless = true,
                    WebPExact = true,
                    WebPMethod = 6,
                    MetadataMode = MetadataMode.KeepAll
                }
            },
            new PresetModel
            {
                Name = "Small for WhatsApp",
                Description = "WebP format, 65% quality, resized to 1280px max width, metadata stripped for smallest file transfer.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.WebP,
                    WebPQuality = 65,
                    WebPMethod = 4,
                    EnableResize = true,
                    ResizeWidth = 1280,
                    ResizeHeight = 720,
                    KeepAspectRatio = true,
                    OnlyShrinkNeverEnlarge = true,
                    MetadataMode = MetadataMode.StripAll
                }
            },
            new PresetModel
            {
                Name = "Modern AVIF",
                Description = "Next-gen AVIF encoding, 75% quality, 4:2:0 subsampling with effort level 4.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.AVIF,
                    AvifQuality = 75,
                    AvifSpeedEffort = 4,
                    AvifChromaSubsampling = "4:2:0",
                    MetadataMode = MetadataMode.KeepExif
                }
            },
            new PresetModel
            {
                Name = "Archive TIFF",
                Description = "Lossless TIFF with LZW compression, retaining all original color profiles and metadata.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.TIFF,
                    TiffCompression = "LZW",
                    MetadataMode = MetadataMode.KeepAll
                }
            },
            new PresetModel
            {
                Name = "Windows Icons (ICO)",
                Description = "Multi-resolution Windows icon file containing 16, 32, 48, and 256 pixel layers.",
                IsBuiltIn = true,
                Settings = new ConversionSettings
                {
                    TargetFormat = OutputFormatType.ICO,
                    IcoSize16 = true,
                    IcoSize32 = true,
                    IcoSize48 = true,
                    IcoSize256 = true
                }
            }
        };
    }
}
