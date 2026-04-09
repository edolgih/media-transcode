using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это общая геометрия рендера для tomkvgpu.
Она нужна и scenario decision, и ffmpeg rendering path, чтобы не дублировать
overlay/downscale output size rules.
*/
/// <summary>
/// Provides shared output-dimension calculations for <c>tomkvgpu</c> overlay and downscale paths.
/// </summary>
internal static class ToMkvGpuVideoGeometry
{
    /*
    Это расчет итоговых размеров кадра с учетом overlay/downscale intent.
    */
    /// <summary>
    /// Resolves output dimensions for the supplied video intent and overlay mode.
    /// </summary>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="videoIntent">Resolved scenario video intent.</param>
    /// <param name="applyOverlayBackground">Whether overlay-background mode is enabled.</param>
    /// <returns>Output width and height in pixels.</returns>
    public static (int Width, int Height) ResolveOutputDimensions(SourceVideo video, VideoIntent videoIntent, bool applyOverlayBackground)
    {
        var downscale = videoIntent is EncodeVideoIntent { Downscale: { } explicitDownscale }
            ? explicitDownscale
            : null;

        if (applyOverlayBackground)
        {
            return ResolveOverlayOutputDimensions(video, downscale?.TargetHeight);
        }

        if (downscale is null)
        {
            return (video.Width, video.Height);
        }

        if (video.Width <= 0 || video.Height <= 0)
        {
            return (video.Width, video.Height);
        }

        var outputWidth = (int)Math.Round(video.Width * (double)downscale.TargetHeight / video.Height);
        return (MakeEven(outputWidth), MakeEven(downscale.TargetHeight));
    }

    /*
    Это расчет размеров для overlay-фильтра с гарантией четных сторон.
    */
    /// <summary>
    /// Resolves output dimensions used by overlay rendering paths.
    /// </summary>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="targetHeight">Optional downscale target height.</param>
    /// <returns>Overlay output width and height in pixels.</returns>
    public static (int Width, int Height) ResolveOverlayOutputDimensions(SourceVideo video, int? targetHeight)
    {
        var outputWidth = video.Width;
        var outputHeight = video.Height;

        if (outputWidth <= 0 || outputHeight <= 0)
        {
            outputWidth = 1920;
            outputHeight = 1080;
        }

        if (outputWidth < outputHeight)
        {
            (outputWidth, outputHeight) = (outputHeight, outputWidth);
        }

        if (targetHeight.HasValue)
        {
            var ratio = (double)targetHeight.Value / outputHeight;
            outputWidth = (int)Math.Round(outputWidth * ratio);
            outputHeight = targetHeight.Value;
        }

        return (MakeEven(outputWidth), MakeEven(outputHeight));
    }

    private static int MakeEven(int value)
    {
        if (value <= 0)
        {
            return value;
        }

        return (value % 2) == 0
            ? value
            : value + 1;
    }
}
