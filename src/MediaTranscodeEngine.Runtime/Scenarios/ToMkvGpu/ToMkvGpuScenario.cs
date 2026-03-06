using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/// <summary>
/// Represents the legacy ToMkvGpu use case as a scenario that decides when MKV remuxing is enough and when H.264 GPU encoding is required.
/// </summary>
public sealed class ToMkvGpuScenario : TranscodeScenario
{
    private static readonly HashSet<string> VideoCopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private static readonly HashSet<string> TimestampSensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wmv",
        ".asf"
    };

    /// <summary>
    /// Initializes a ToMkvGpu scenario with optional runtime directives.
    /// </summary>
    /// <param name="overlayBackground">Whether background overlay should be applied during video encoding.</param>
    /// <param name="downscaleTarget">Requested downscale target height.</param>
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    public ToMkvGpuScenario(
        bool overlayBackground = false,
        int? downscaleTarget = null,
        bool synchronizeAudio = false,
        bool keepSource = false)
        : base("tomkvgpu")
    {
        if (downscaleTarget.HasValue && downscaleTarget.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(downscaleTarget), downscaleTarget.Value, "Downscale target must be greater than zero.");
        }

        OverlayBackground = overlayBackground;
        DownscaleTarget = downscaleTarget;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
    }

    /// <summary>
    /// Gets a value indicating whether the scenario requests background overlay for encoded video.
    /// </summary>
    public bool OverlayBackground { get; }

    /// <summary>
    /// Gets the requested downscale target when the scenario asks for resizing.
    /// </summary>
    public int? DownscaleTarget { get; }

    /// <summary>
    /// Gets a value indicating whether the scenario forces the audio sync-safe path.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Builds a ToMkvGpu plan from the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic plan describing the required MKV conversion work.</returns>
    protected override TranscodePlan BuildPlanCore(SourceVideo video)
    {
        if (DownscaleTarget == 720)
        {
            throw new NotSupportedException("Downscale 720 is not implemented for ToMkvGpu.");
        }

        var applyDownscale = DownscaleTarget.HasValue && video.Height > DownscaleTarget.Value;
        var requiresTimestampFix = TimestampSensitiveExtensions.Contains(video.FileExtension);
        var copyVideo = VideoCopyCodecs.Contains(video.VideoCodec) &&
                        !requiresTimestampFix &&
                        !OverlayBackground &&
                        !applyDownscale;
        var copyAudio = !SynchronizeAudio &&
                        copyVideo &&
                        AreAudioStreamsCopyCompatible(video.AudioCodecs);

        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: copyVideo ? null : "h264",
            preferredBackend: copyVideo ? null : "gpu",
            targetHeight: applyDownscale ? DownscaleTarget : null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            copyVideo: copyVideo,
            copyAudio: copyAudio,
            fixTimestamps: requiresTimestampFix || !copyVideo,
            keepSource: KeepSource,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: OverlayBackground,
            synchronizeAudio: SynchronizeAudio);
    }

    private static bool AreAudioStreamsCopyCompatible(IReadOnlyList<string> audioCodecs)
    {
        if (audioCodecs.Count == 0)
        {
            return true;
        }

        return audioCodecs.All(codec => codec.Equals("aac", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOutputPath(SourceVideo video, bool copyVideo, bool copyAudio)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.mkv");
        if (!KeepSource ||
            !video.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase) ||
            (copyVideo && copyAudio))
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.mkv");
    }
}
