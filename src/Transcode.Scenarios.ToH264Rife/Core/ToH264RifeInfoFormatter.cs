using Transcode.Core.Failures;
using Transcode.Core.Videos;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это форматтер info-режима для toh264rife.
Он строит короткую строку с основными параметрами решения и единообразные маркеры ошибок для CLI.
*/
/// <summary>
/// Formats concise info output for the <c>toh264rife</c> scenario.
/// </summary>
public sealed class ToH264RifeInfoFormatter
{
    /*
    Это краткий маркер ошибки для CLI.
    */
    /// <summary>
    /// Builds a single-line failure marker for CLI output.
    /// </summary>
    public string FormatFailure(string filePath, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(exception);

        var marker = exception is RuntimeFailureException runtimeFailure && runtimeFailure.Code == RuntimeFailureCode.NoVideoStream
            ? "no video stream"
            : "ffprobe failed";
        return $"{Path.GetFileName(filePath.Trim())}: [{marker}]";
    }

    /*
    Это краткая сводка решения по конкретному входному файлу.
    */
    /// <summary>
    /// Builds a single-line summary with the key resolved execution parameters.
    /// </summary>
    internal string Format(SourceVideo video, ToH264RifeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        var parts = new List<string>();
        var sourceContainer = video.FileExtension.TrimStart('.');

        parts.Add($"x{decision.FramesPerSecondMultiplier}");
        parts.Add($"target {decision.ResolvedTargetFramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture)} fps");
        parts.Add($"interp {decision.InterpolationQualityProfile}/{decision.InterpolationModelName}");
        parts.Add($"profile {decision.ResolvedVideoSettings.ContentProfile}/{decision.ResolvedVideoSettings.QualityProfile}");
        parts.Add($"cq {decision.ResolvedVideoSettings.Cq}");
        parts.Add($"maxrate {decision.ResolvedVideoSettings.Maxrate.ToString("0.###", CultureInfo.InvariantCulture)}M");
        parts.Add($"bufsize {decision.ResolvedVideoSettings.Bufsize.ToString("0.###", CultureInfo.InvariantCulture)}M");

        if (!sourceContainer.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{sourceContainer}->{decision.TargetContainer}");
        }

        return $"{video.FileName}: {video.Width}x{video.Height} fps {video.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture)} [{string.Join("] [", parts)}]";
    }
}
