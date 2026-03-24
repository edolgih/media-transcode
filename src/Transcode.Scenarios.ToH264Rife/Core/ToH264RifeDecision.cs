using Transcode.Core.MediaIntent;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Carries the resolved decision for the <c>toh264rife</c> scenario.
/// </summary>
internal sealed class ToH264RifeDecision
{
    public ToH264RifeDecision(
        string targetContainer,
        VideoIntent video,
        AudioIntent audio,
        bool keepSource,
        string outputPath,
        string interpolationQualityProfile,
        string interpolationModelName,
        ResolvedVideoSettingsDefaults resolvedVideoSettings,
        double resolvedTargetFramesPerSecond,
        int userFacingTargetFramesPerSecond,
        int framesPerSecondMultiplier)
    {
        TargetContainer = targetContainer;
        Video = video;
        Audio = audio;
        KeepSource = keepSource;
        OutputPath = outputPath;
        InterpolationQualityProfile = interpolationQualityProfile ?? throw new ArgumentNullException(nameof(interpolationQualityProfile));
        InterpolationModelName = interpolationModelName ?? throw new ArgumentNullException(nameof(interpolationModelName));
        ResolvedVideoSettings = resolvedVideoSettings ?? throw new ArgumentNullException(nameof(resolvedVideoSettings));
        ResolvedTargetFramesPerSecond = resolvedTargetFramesPerSecond;
        UserFacingTargetFramesPerSecond = userFacingTargetFramesPerSecond;
        FramesPerSecondMultiplier = framesPerSecondMultiplier;
    }

    public string TargetContainer { get; }

    public VideoIntent Video { get; }

    public AudioIntent Audio { get; }

    public bool KeepSource { get; }

    public string OutputPath { get; }

    public string InterpolationQualityProfile { get; }

    public string InterpolationModelName { get; }

    public ResolvedVideoSettingsDefaults ResolvedVideoSettings { get; }

    public double ResolvedTargetFramesPerSecond { get; }

    public int UserFacingTargetFramesPerSecond { get; }

    public int FramesPerSecondMultiplier { get; }

    public bool CopyVideo => Video is CopyVideoIntent;

    public bool CopyAudio => Audio is CopyAudioIntent;

    public bool RequiresInterpolation => Video is EncodeVideoIntent { UseFrameInterpolation: true };
}
