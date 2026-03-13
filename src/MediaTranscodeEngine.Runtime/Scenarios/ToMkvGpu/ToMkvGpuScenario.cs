using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Failures;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Это прикладной сценарий tomkvgpu.
Он решает, достаточно ли remux в mkv, или нужно строить план GPU-кодирования в H.264/H.265.
*/
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

    private readonly VideoSettingsProfiles _videoSettingsProfiles;

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario()
        : this(new ToMkvGpuRequest(), VideoSettingsProfiles.Default)
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request)
        : this(request, VideoSettingsProfiles.Default)
    {
    }

    internal ToMkvGpuScenario(ToMkvGpuRequest request, VideoSettingsProfiles videoSettingsProfiles)
        : base("tomkvgpu")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _videoSettingsProfiles = videoSettingsProfiles ?? throw new ArgumentNullException(nameof(videoSettingsProfiles));
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToMkvGpu workflow.
    /// </summary>
    public ToMkvGpuRequest Request { get; }

    /// <summary>
    /// Builds a ToMkvGpu plan from the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic plan describing the required MKV conversion work.</returns>
    protected override TranscodePlan BuildPlanCore(SourceVideo video)
    {
        var applyDownscale = Request.Downscale is not null &&
                             video.Height > Request.Downscale.TargetHeight;
        ValidateDownscale(video, applyDownscale);

        var effectiveVideoSettings = ResolveEffectiveVideoSettings(applyDownscale);
        var applyFrameRateCap = Request.MaxFramesPerSecond.HasValue &&
                                video.FramesPerSecond > Request.MaxFramesPerSecond.Value;
        var requiresTimestampFix = TimestampSensitiveExtensions.Contains(video.FileExtension);
        var copyVideo = VideoCopyCodecs.Contains(video.VideoCodec) &&
                        !requiresTimestampFix &&
                        !Request.OverlayBackground &&
                        !applyDownscale &&
                        !applyFrameRateCap;
        var copyAudio = !Request.SynchronizeAudio &&
                        copyVideo &&
                        AreAudioStreamsCopyCompatible(video.AudioCodecs);
        AudioPlan audioPlan = copyAudio
            ? new CopyAudioPlan()
            : Request.SynchronizeAudio
                ? new SynchronizeAudioPlan()
                : requiresTimestampFix
                    ? new RepairAudioPlan()
                    : new EncodeAudioPlan();
        VideoPlan videoPlan = copyVideo
            ? new CopyVideoPlan()
            : new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                TargetFramesPerSecond: applyFrameRateCap ? Request.MaxFramesPerSecond : null,
                UseFrameInterpolation: false,
                VideoSettings: effectiveVideoSettings,
                Downscale: applyDownscale ? Request.Downscale : null,
                EncoderPreset: Request.NvencPreset);

        return new TranscodePlan(
            targetContainer: "mkv",
            video: videoPlan,
            audio: audioPlan,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: Request.OverlayBackground);
    }

    private void ValidateDownscale(SourceVideo video, bool applyDownscale)
    {
        var targetHeight = Request.Downscale?.TargetHeight;
        if (!targetHeight.HasValue)
        {
            return;
        }

        if (!applyDownscale && video.Height > 0)
        {
            return;
        }

        if (!_videoSettingsProfiles.TryGetProfile(targetHeight.Value, out var profile))
        {
            return;
        }

        var issue = profile.ResolveSourceBucketIssue(video.Height);
        if (!string.IsNullOrWhiteSpace(issue))
        {
            throw RuntimeFailures.DownscaleSourceBucketIssue(issue);
        }
    }

    private VideoSettingsRequest? ResolveEffectiveVideoSettings(bool applyDownscale)
    {
        if (Request.VideoSettings is null)
        {
            return null;
        }

        return Request.VideoSettings;
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
        if (!Request.KeepSource ||
            !video.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase) ||
            (copyVideo && copyAudio))
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.mkv");
    }
}
