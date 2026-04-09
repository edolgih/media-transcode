namespace Transcode.Core.Videos;

/*
Этот helper резолвит bitrate-подсказку именно для видео-потока.
Он предпочитает stream-level bitrate из probe, а при наличии только общего bitrate вычитает оценку аудио.
*/
/// <summary>
/// Resolves video-stream bitrate hints from normalized source facts.
/// </summary>
public static class SourceVideoBitrateResolver
{
    /*
    Это резолв: a video-only bitrate hint from source metadata when available
    */
    /// <summary>
    /// Resolves a video-only bitrate hint from source metadata when available.
    /// </summary>
    /// <param name="video">Normalized source video facts.</param>
    /// <returns>Video-stream bitrate in bits per second or <see langword="null"/> when metadata is missing.</returns>
    public static long? ResolveVideoBitrateHint(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        if (video.PrimaryVideoBitrate.HasValue && video.PrimaryVideoBitrate.Value > 0)
        {
            return video.PrimaryVideoBitrate.Value;
        }

        if (video.Bitrate.HasValue && video.Bitrate.Value > 0)
        {
            return ResolveVideoBitrateFromTotal(video.Bitrate.Value, video);
        }

        return null;
    }

    /*
    Это резолв: a video-only bitrate hint from source metadata or estimates it from file size when metadata is missing
    */
    /// <summary>
    /// Resolves a video-only bitrate hint from source metadata or estimates it from file size when metadata is missing.
    /// </summary>
    /// <param name="video">Normalized source video facts.</param>
    /// <returns>Video-stream bitrate in bits per second or <see langword="null"/> when it cannot be determined.</returns>
    public static long? ResolveVideoBitrateHintOrEstimate(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var resolvedMetadataBitrate = ResolveVideoBitrateHint(video);
        if (resolvedMetadataBitrate.HasValue && resolvedMetadataBitrate.Value > 0)
        {
            return resolvedMetadataBitrate.Value;
        }

        if (video.Duration <= TimeSpan.FromSeconds(0.1) ||
            string.IsNullOrWhiteSpace(video.FilePath) ||
            !File.Exists(video.FilePath))
        {
            return null;
        }

        var fileSizeBits = new FileInfo(video.FilePath).Length * 8m;
        if (fileSizeBits <= 0m)
        {
            return null;
        }

        var totalBitrate = Math.Round(fileSizeBits / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        return totalBitrate > 0m && totalBitrate <= long.MaxValue
            ? ResolveVideoBitrateFromTotal((long)totalBitrate, video)
            : null;
    }

    /*
    Это резолв: video-stream bitrate from a total stream bitrate by subtracting estimated audio bitrate when known
    */
    /// <summary>
    /// Resolves video-stream bitrate from a total stream bitrate by subtracting estimated audio bitrate when known.
    /// </summary>
    /// <param name="totalBitrate">Total bitrate in bits per second.</param>
    /// <param name="video">Normalized source video facts.</param>
    /// <returns>Video-stream bitrate in bits per second.</returns>
    public static long ResolveVideoBitrateFromTotal(long totalBitrate, SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        if (totalBitrate <= 0 || !video.HasAudio)
        {
            return totalBitrate;
        }

        var audioBitrate = ResolveEstimatedAudioBitrate(video);
        if (!audioBitrate.HasValue || audioBitrate.Value <= 0)
        {
            return totalBitrate;
        }

        var videoOnlyBitrate = totalBitrate - audioBitrate.Value;
        return videoOnlyBitrate > 0
            ? videoOnlyBitrate
            : totalBitrate;
    }

    private static long? ResolveEstimatedAudioBitrate(SourceVideo video)
    {
        if (!video.PrimaryAudioBitrate.HasValue || video.PrimaryAudioBitrate.Value <= 0)
        {
            return null;
        }

        var streamCount = Math.Max(1, video.AudioCodecs.Count);
        var estimatedAudioBitrate = video.PrimaryAudioBitrate.Value * streamCount;
        return estimatedAudioBitrate > 0
            ? estimatedAudioBitrate
            : null;
    }
}
