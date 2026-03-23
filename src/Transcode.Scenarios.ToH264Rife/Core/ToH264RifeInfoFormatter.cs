using Transcode.Core.Failures;
using Transcode.Core.Videos;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Formats concise info output for <c>toh264rife</c>.
/// </summary>
public sealed class ToH264RifeInfoFormatter
{
    public string FormatFailure(string filePath, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(exception);

        var marker = exception is RuntimeFailureException runtimeFailure && runtimeFailure.Code == RuntimeFailureCode.NoVideoStream
            ? "no video stream"
            : "ffprobe failed";
        return $"{Path.GetFileName(filePath.Trim())}: [{marker}]";
    }

    internal string Format(SourceVideo video, ToH264RifeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        var parts = new List<string>();
        var sourceContainer = video.FileExtension.TrimStart('.');

        if (decision.RequiresInterpolation)
        {
            parts.Add($"target {decision.ResolvedTargetFramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture)} fps");
        }
        else if (!sourceContainer.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("remux-only");
            parts.Add("already near target fps");
        }
        else
        {
            parts.Add("already near target fps");
        }

        if (!sourceContainer.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{sourceContainer}->{decision.TargetContainer}");
        }

        return $"{video.FileName}: {video.Width}x{video.Height} fps {video.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture)} [{string.Join("] [", parts)}]";
    }
}
