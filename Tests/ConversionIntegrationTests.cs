using System.IO;
using ImageMagick;
using ImageMagick.Drawing;
using ImageBatchConverter.Models;
using ImageBatchConverter.Services;
using ImageBatchConverter.Services.Converters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageBatchConverter.Tests;

[TestClass]
public class ConversionIntegrationTests
{
    private string _testDir = string.Empty;
    private string _samplePng = string.Empty;
    private string _sampleJpg = string.Empty;
    private string _sampleGif = string.Empty;
    private ILogService _logService = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "IBC_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _logService = new LogService();

        // 1. Generate sample transparent PNG
        _samplePng = Path.Combine(_testDir, "test_alpha.png");
        using (var img = new MagickImage(MagickColors.Transparent, 200, 200))
        {
            var drawables = new Drawables()
                .FillColor(MagickColors.DeepSkyBlue)
                .Circle(100, 100, 100, 160);
            img.Draw(drawables);
            img.Write(_samplePng, MagickFormat.Png);
        }

        // 2. Generate sample JPG
        _sampleJpg = Path.Combine(_testDir, "test_photo.jpg");
        using (var img = new MagickImage(MagickColors.Crimson, 300, 200))
        {
            img.Write(_sampleJpg, MagickFormat.Jpeg);
        }

        // 3. Generate sample multi-frame GIF
        _sampleGif = Path.Combine(_testDir, "test_animated.gif");
        using (var collection = new MagickImageCollection())
        {
            var f1 = new MagickImage(MagickColors.Red, 100, 100) { AnimationDelay = 20 };
            var f2 = new MagickImage(MagickColors.Blue, 100, 100) { AnimationDelay = 20 };
            collection.Add(f1);
            collection.Add(f2);
            collection.Write(_sampleGif, MagickFormat.Gif);
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [TestMethod]
    public async Task WebPConverter_GeneratesValidWebP_WithCWebp()
    {
        var converter = new WebPConverter(_logService);
        string outputPath = Path.Combine(_testDir, "out_test.webp");

        var settings = new ConversionSettings
        {
            TargetFormat = OutputFormatType.WebP,
            WebPQuality = 85,
            WebPMethod = 4,
            WebPSharpYuv = true
        };

        var result = await converter.ConvertAsync(_samplePng, outputPath, settings, CancellationToken.None);

        Assert.IsTrue(result.Success, $"WebP conversion failed: {result.ErrorMessage}");
        Assert.IsTrue(File.Exists(outputPath), "Output WebP file does not exist.");
        Assert.IsTrue(new FileInfo(outputPath).Length > 0, "Output WebP file is empty.");

        // Verify with MagickImage that it's a valid WebP
        using var readBack = new MagickImage(outputPath);
        Assert.AreEqual(MagickFormat.WebP, readBack.Format);
        Assert.AreEqual(200u, readBack.Width);
        Assert.AreEqual(200u, readBack.Height);
    }

    [TestMethod]
    public async Task JpegConverter_FlattensTransparencyToWhite()
    {
        var converter = new JpegConverter(_logService);
        string outputPath = Path.Combine(_testDir, "out_test.jpg");

        var settings = new ConversionSettings
        {
            TargetFormat = OutputFormatType.JPEG,
            JpegQuality = 90,
            JpegProgressive = true,
            JpegBackgroundColorHex = "#FFFFFF"
        };

        var result = await converter.ConvertAsync(_samplePng, outputPath, settings, CancellationToken.None);

        Assert.IsTrue(result.Success, $"JPEG conversion failed: {result.ErrorMessage}");
        Assert.IsTrue(File.Exists(outputPath));

        using var readBack = new MagickImage(outputPath);
        Assert.AreEqual(MagickFormat.Jpeg, readBack.Format);
        Assert.IsFalse(readBack.HasAlpha, "JPEG output must not have alpha channel.");
    }

    [TestMethod]
    public async Task IcoConverter_ProducesMultiSizeIcon()
    {
        var converter = new IcoConverter(_logService);
        string outputPath = Path.Combine(_testDir, "out_test.ico");

        var settings = new ConversionSettings
        {
            TargetFormat = OutputFormatType.ICO,
            IcoSize16 = true,
            IcoSize32 = true,
            IcoSize48 = true,
            IcoSize256 = true
        };

        var result = await converter.ConvertAsync(_samplePng, outputPath, settings, CancellationToken.None);

        Assert.IsTrue(result.Success, $"ICO conversion failed: {result.ErrorMessage}");
        Assert.IsTrue(File.Exists(outputPath));

        using var collection = new MagickImageCollection(outputPath);
        Assert.IsTrue(collection.Count >= 4, $"Expected at least 4 layers in ICO, got {collection.Count}");
    }

    [TestMethod]
    public async Task BackupService_CopiesAndVerifiesHash()
    {
        var backupService = new BackupService(_logService);
        string backupRoot = Path.Combine(_testDir, "Backup");

        var (success, backupPath, error) = await backupService.CreateBackupAsync(
            _samplePng,
            "subfolder/test_alpha.png",
            backupRoot,
            CancellationToken.None);

        Assert.IsTrue(success, $"Backup failed: {error}");
        Assert.IsNotNull(backupPath);
        Assert.IsTrue(File.Exists(backupPath));
        Assert.AreEqual(new FileInfo(_samplePng).Length, new FileInfo(backupPath).Length);
    }

    [TestMethod]
    public void BackupService_SafeDeleteOriginal_EnforcesSafetyRules()
    {
        var backupService = new BackupService(_logService);
        string fakeOriginal = Path.Combine(_testDir, "fake_orig.png");
        File.WriteAllText(fakeOriginal, "fake image data");

        // Rule 1: Backup enabled but not verified -> MUST NOT delete
        bool deleted = backupService.SafeDeleteOriginal(fakeOriginal, backupEnabled: true, backupVerified: false, conversionSuccess: true, outputPath: _sampleJpg);
        Assert.IsFalse(deleted, "Should refuse delete when backup is not verified!");
        Assert.IsTrue(File.Exists(fakeOriginal));

        // Rule 2: Conversion failed -> MUST NOT delete
        deleted = backupService.SafeDeleteOriginal(fakeOriginal, backupEnabled: false, backupVerified: false, conversionSuccess: false, outputPath: _sampleJpg);
        Assert.IsFalse(deleted, "Should refuse delete when conversion failed!");
        Assert.IsTrue(File.Exists(fakeOriginal));

        // Rule 3: Output file does not exist -> MUST NOT delete
        deleted = backupService.SafeDeleteOriginal(fakeOriginal, backupEnabled: false, backupVerified: false, conversionSuccess: true, outputPath: "C:\\nonexistent.png");
        Assert.IsFalse(deleted, "Should refuse delete when output file does not exist!");
        Assert.IsTrue(File.Exists(fakeOriginal));
    }

    [TestMethod]
    public async Task ScanService_FiltersAndComputesSummary()
    {
        var scanService = new ScanService();

        // Create a subfolder with an extra image
        string subDir = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.Copy(_samplePng, Path.Combine(subDir, "extra.png"));

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PNG", "JPG" };

        var (files, summary) = await scanService.ScanPathsAsync(
            new[] { _testDir },
            includeSubfolders: true,
            allowedFormats: allowed,
            allSupported: false,
            skipTargetFormat: false,
            targetFormat: OutputFormatType.WebP);

        // Expect 2 PNGs + 1 JPG = 3 files (GIF excluded since not in allowed)
        Assert.AreEqual(3, files.Count);
        Assert.AreEqual(3, summary.TotalFiles);
        Assert.AreEqual(2, summary.CountByFormat["PNG"]);
        Assert.AreEqual(1, summary.CountByFormat["JPG"]);
    }

    [TestMethod]
    public async Task ConversionService_RunsParallelBatch_Successfully()
    {
        var backupService = new BackupService(_logService);
        var converters = new List<IImageConverter>
        {
            new WebPConverter(_logService),
            new JpegConverter(_logService),
            new PngConverter(_logService)
        };

        var conversionService = new ConversionService(_logService, backupService, converters);

        var files = new List<ImageFileInfo>
        {
            new()
            {
                FullPath = _samplePng,
                RelativePath = "test_alpha.png",
                FileName = "test_alpha.png",
                Extension = ".png",
                FormatName = "PNG",
                FileSizeBytes = new FileInfo(_samplePng).Length
            },
            new()
            {
                FullPath = _sampleJpg,
                RelativePath = "test_photo.jpg",
                FileName = "test_photo.jpg",
                Extension = ".jpg",
                FormatName = "JPG",
                FileSizeBytes = new FileInfo(_sampleJpg).Length
            }
        };

        string outDir = Path.Combine(_testDir, "BatchOutput");
        Directory.CreateDirectory(outDir);

        var settings = new ConversionSettings
        {
            TargetFormat = OutputFormatType.WebP,
            SaveInSameFolder = false,
            CustomOutputFolder = outDir,
            BackupOriginals = true,
            BackupFolder = Path.Combine(_testDir, "BatchBackup"),
            WorkerCount = 2,
            KeepOriginalIfLarger = false
        };

        var summary = await conversionService.StartBatchAsync(
            files,
            _testDir,
            settings,
            null,
            CancellationToken.None);

        Assert.AreEqual(2, summary.TotalFiles);
        Assert.AreEqual(2, summary.ConvertedCount);
        Assert.AreEqual(0, summary.FailedCount);

        string expectedWebp1 = Path.Combine(outDir, "test_alpha.webp");
        string expectedWebp2 = Path.Combine(outDir, "test_photo.webp");

        Assert.IsTrue(File.Exists(expectedWebp1), "Output WebP 1 was not created.");
        Assert.IsTrue(File.Exists(expectedWebp2), "Output WebP 2 was not created.");
        Assert.IsTrue(new FileInfo(expectedWebp1).Length > 0);
        Assert.IsTrue(new FileInfo(expectedWebp2).Length > 0);
    }

    [TestMethod]
    public void ConversionSettings_SaveInSeparateFolder_PropertySyncs()
    {
        var settings = new ConversionSettings();
        Assert.IsTrue(settings.SaveInSameFolder);
        Assert.IsFalse(settings.SaveInSeparateFolder);

        // Toggle separate folder
        settings.SaveInSeparateFolder = true;
        Assert.IsFalse(settings.SaveInSameFolder);
        Assert.IsTrue(settings.SaveInSeparateFolder);

        // Toggle same folder
        settings.SaveInSameFolder = true;
        Assert.IsTrue(settings.SaveInSameFolder);
        Assert.IsFalse(settings.SaveInSeparateFolder);
    }

    [TestMethod]
    public void GenerateAppIcon_FromUploadedImage()
    {
        string inputPng = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "app_icon.png");
        if (!File.Exists(inputPng))
        {
            inputPng = @"c:\Users\PC\.gemini\antigravity\scratch\Fileo\Assets\app_icon.png";
        }

        Assert.IsTrue(File.Exists(inputPng), "Source icon PNG must exist.");

        string outIco = @"c:\Users\PC\.gemini\antigravity\scratch\Fileo\Assets\app.ico";

        using var collection = new MagickImageCollection();
        int[] sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };

        foreach (var size in sizes)
        {
            var layer = new MagickImage(inputPng);
            layer.FilterType = FilterType.Lanczos;
            layer.Resize((uint)size, (uint)size);
            layer.Format = MagickFormat.Ico;
            collection.Add(layer);
        }

        collection.Write(outIco, MagickFormat.Ico);
        Assert.IsTrue(File.Exists(outIco));
        Assert.IsTrue(new FileInfo(outIco).Length > 0);
    }
}
