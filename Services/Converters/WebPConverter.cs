using System.Diagnostics;
using System.IO;
using System.Text;
using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public class WebPConverter : IImageConverter
{
    private readonly ILogService _logService;
    private readonly string _cwebpPath;

    public OutputFormatType Format => OutputFormatType.WebP;

    public WebPConverter(ILogService logService)
    {
        _logService = logService;

        // Locate cwebp.exe in AppContext.BaseDirectory/tools or relative tools folder
        string appDir = AppContext.BaseDirectory;
        string candidate = Path.Combine(appDir, "tools", "cwebp.exe");
        if (File.Exists(candidate))
        {
            _cwebpPath = candidate;
        }
        else
        {
            string rootCandidate = Path.Combine(Directory.GetCurrentDirectory(), "tools", "cwebp.exe");
            _cwebpPath = File.Exists(rootCandidate) ? rootCandidate : "cwebp.exe";
        }
    }

    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken)
    {
        string? tempIntermediateFile = null;

        try
        {
            // Ensure output directory exists
            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            // Check if input requires intermediate handling via Magick.NET
            // Reasons:
            // 1. Non-standard input format (HEIC, AVIF, BMP, GIF, ICO, JXL, etc.)
            // 2. Common operations required (Auto-rotate, custom Resize filter, metadata stripping)
            // 3. Animated GIF input handling
            string inputExt = Path.GetExtension(inputPath).ToLowerInvariant();
            bool isDirectInput = (inputExt == ".png" || inputExt == ".jpg" || inputExt == ".jpeg" || inputExt == ".tiff" || inputExt == ".tif")
                                 && !settings.AutoRotate
                                 && !settings.EnableResize
                                 && settings.MetadataMode == MetadataMode.KeepAll;

            string effectiveInput = inputPath;

            if (!isDirectInput)
            {
                // Process with Magick.NET to intermediate PNG
                tempIntermediateFile = Path.Combine(Path.GetTempPath(), $"ibc_temp_{Guid.NewGuid():N}.png");

                using (var image = new MagickImage(inputPath))
                {
                    // Handle animated gif: if gif and user wants first frame or not keep animation
                    // (cwebp converts single image)
                    ImageProcessingHelper.ApplyCommonOperations(image, settings);
                    image.Format = MagickFormat.Png;
                    await image.WriteAsync(tempIntermediateFile, cancellationToken);
                }

                effectiveInput = tempIntermediateFile;
            }

            // Build cwebp argument list
            var args = new StringBuilder();

            // Quality / Lossless
            if (settings.WebPLossless)
            {
                args.Append("-lossless ");
                if (settings.WebPExact)
                {
                    args.Append("-exact ");
                }
            }
            else
            {
                args.Append($"-q {Math.Clamp(settings.WebPQuality, 0, 100)} ");

                if (settings.WebPNearLossless < 100)
                {
                    args.Append($"-near_lossless {Math.Clamp(settings.WebPNearLossless, 0, 100)} ");
                }

                if (settings.WebPAlphaQuality < 100)
                {
                    args.Append($"-alpha_q {Math.Clamp(settings.WebPAlphaQuality, 0, 100)} ");
                }
            }

            // Method / Effort
            args.Append($"-m {Math.Clamp(settings.WebPMethod, 0, 6)} ");

            // Preset
            if (!string.IsNullOrWhiteSpace(settings.WebPPreset) && !settings.WebPPreset.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                args.Append($"-preset {settings.WebPPreset.ToLowerInvariant()} ");
            }

            // Sharp YUV
            if (settings.WebPSharpYuv)
            {
                args.Append("-sharp_yuv ");
            }

            // Filter strength & Sharpness
            if (settings.WebPFilterStrength > 0)
            {
                args.Append($"-f {Math.Clamp(settings.WebPFilterStrength, 0, 100)} ");
            }
            if (settings.WebPSharpness > 0)
            {
                args.Append($"-sharpness {Math.Clamp(settings.WebPSharpness, 0, 7)} ");
            }

            // Multithreading
            if (settings.WebPMultithread)
            {
                args.Append("-mt ");
            }

            // Metadata
            if (!string.IsNullOrWhiteSpace(settings.WebPMetadata))
            {
                args.Append($"-metadata {settings.WebPMetadata.ToLowerInvariant()} ");
            }

            // Target size in bytes
            if (settings.WebPTargetSize > 0)
            {
                args.Append($"-size {settings.WebPTargetSize} ");
            }

            // Direct resize if not already pre-resized via intermediate
            if (isDirectInput && settings.EnableResize && settings.ResizeWidth > 0 && settings.ResizeHeight > 0)
            {
                args.Append($"-resize {settings.ResizeWidth} {settings.ResizeHeight} ");
            }

            // Quoted input and output file paths
            args.Append($"\"{effectiveInput}\" -o \"{outputPath}\"");

            var psi = new ProcessStartInfo
            {
                FileName = _cwebpPath,
                Arguments = args.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string err = errorBuilder.ToString().Trim();
                _logService.LogWarning($"cwebp failed (exit code {process.ExitCode}): {err}. Falling back to Magick.NET WebP engine.");

                // Fallback to Magick.NET
                return await FallbackMagickConvertAsync(inputPath, outputPath, settings, cancellationToken);
            }

            if (!File.Exists(outputPath))
            {
                return new ConversionResult(false, null, 0, "cwebp produced no output file.");
            }

            var fi = new FileInfo(outputPath);
            return new ConversionResult(true, outputPath, fi.Length, null);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath)) { try { File.Delete(outputPath); } catch { } }
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogError($"WebP conversion failed for {inputPath}", ex);
            return new ConversionResult(false, null, 0, ex.Message);
        }
        finally
        {
            if (tempIntermediateFile != null && File.Exists(tempIntermediateFile))
            {
                try { File.Delete(tempIntermediateFile); } catch { }
            }
        }
    }

    private static async Task<ConversionResult> FallbackMagickConvertAsync(
        string inputPath,
        string outputPath,
        ConversionSettings settings,
        CancellationToken cancellationToken)
    {
        using var image = new MagickImage(inputPath);
        ImageProcessingHelper.ApplyCommonOperations(image, settings);
        image.Format = MagickFormat.WebP;
        image.Quality = (uint)Math.Clamp(settings.WebPQuality, 0, 100);

        if (settings.WebPLossless)
        {
            image.Settings.SetDefine(MagickFormat.WebP, "lossless", "true");
        }

        await image.WriteAsync(outputPath, cancellationToken);
        var fi = new FileInfo(outputPath);
        return new ConversionResult(true, outputPath, fi.Length, null);
    }
}
