using ImageMagick;
using ImageBatchConverter.Models;

namespace ImageBatchConverter.Services.Converters;

public static class ImageProcessingHelper
{
    public static void ApplyCommonOperations(IMagickImage<float> image, ConversionSettings settings)
    {
        // 1. Auto-rotate based on EXIF
        if (settings.AutoRotate)
        {
            image.AutoOrient();
        }

        // 2. Resize
        if (settings.EnableResize && settings.ResizeWidth > 0 && settings.ResizeHeight > 0)
        {
            uint origW = image.Width;
            uint origH = image.Height;

            bool shouldResize = true;
            if (settings.OnlyShrinkNeverEnlarge)
            {
                if (origW <= settings.ResizeWidth && origH <= settings.ResizeHeight)
                {
                    shouldResize = false;
                }
            }

            if (shouldResize)
            {
                var geometry = new MagickGeometry((uint)settings.ResizeWidth, (uint)settings.ResizeHeight)
                {
                    IgnoreAspectRatio = !settings.KeepAspectRatio,
                    Greater = settings.OnlyShrinkNeverEnlarge
                };

                image.FilterType = settings.ResampleFilter switch
                {
                    "Mitchell" => FilterType.Mitchell,
                    "Cubic" => FilterType.Cubic,
                    "Box" => FilterType.Box,
                    "Triangle" => FilterType.Triangle,
                    "Point" => FilterType.Point,
                    _ => FilterType.Lanczos
                };

                image.Resize(geometry);
            }
        }

        // 3. Metadata handling
        switch (settings.MetadataMode)
        {
            case MetadataMode.StripAll:
                image.Strip();
                break;

            case MetadataMode.KeepExif:
                var exif = image.GetExifProfile();
                image.Strip();
                if (exif != null) image.SetProfile(exif);
                break;

            case MetadataMode.KeepIcc:
                var icc = image.GetColorProfile();
                image.Strip();
                if (icc != null) image.SetProfile(icc);
                break;

            case MetadataMode.KeepAll:
            default:
                // Keep everything intact
                break;
        }
    }

    public static void FlattenTransparency(IMagickImage<float> image, string hexColor)
    {
        if (!image.HasAlpha) return;

        try
        {
            var bg = new MagickColor(string.IsNullOrWhiteSpace(hexColor) ? "#FFFFFF" : hexColor);
            image.ColorAlpha(bg);
        }
        catch
        {
            image.ColorAlpha(MagickColors.White);
        }
    }
}
