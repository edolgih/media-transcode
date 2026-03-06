using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/// <summary>
/// Formats a concise ToMkvGpu decision summary from an inspected source video and the scenario plan.
/// </summary>
public sealed class ToMkvGpuInfoFormatter
{
    /// <summary>
    /// Builds the standard info marker used when probe data could not be loaded.
    /// </summary>
    /// <param name="filePath">Path to the source file that could not be probed.</param>
    /// <returns>A single-line ffprobe failure marker.</returns>
    public string FormatProbeFailure(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return $"{Path.GetFileName(filePath.Trim())}: [ffprobe failed]";
    }

    /// <summary>
    /// Builds a single-line summary of the actions requested by ToMkvGpu for the supplied video and plan.
    /// </summary>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="plan">Resolved ToMkvGpu plan.</param>
    /// <returns>A summary line or an empty string when no action is required.</returns>
    public string Format(SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        var parts = new List<string>();

        if (!video.Container.Equals(plan.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"container .{video.Container}→{plan.TargetContainer}");
        }

        if (!plan.CopyVideo)
        {
            parts.Add($"vcodec {video.VideoCodec}");
        }

        if (HasNonAacAudio(video))
        {
            parts.Add("audio non-AAC");
        }

        if (plan.SynchronizeAudio && video.HasAudio)
        {
            parts.Add("sync audio");
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return $"{video.FileName}: [{string.Join("] [", parts)}]";
    }

    private static bool HasNonAacAudio(SourceVideo video)
    {
        return video.AudioCodecs.Any(codec => !codec.Equals("aac", StringComparison.OrdinalIgnoreCase));
    }
}
