using Transcode.Core.MediaIntent;

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
        double resolvedTargetFramesPerSecond,
        int userFacingTargetFramesPerSecond)
    {
        TargetContainer = targetContainer;
        Video = video;
        Audio = audio;
        KeepSource = keepSource;
        OutputPath = outputPath;
        ResolvedTargetFramesPerSecond = resolvedTargetFramesPerSecond;
        UserFacingTargetFramesPerSecond = userFacingTargetFramesPerSecond;
    }

    public string TargetContainer { get; }

    public VideoIntent Video { get; }

    public AudioIntent Audio { get; }

    public bool KeepSource { get; }

    public string OutputPath { get; }

    public double ResolvedTargetFramesPerSecond { get; }

    public int UserFacingTargetFramesPerSecond { get; }

    public bool CopyVideo => Video is CopyVideoIntent;

    public bool CopyAudio => Audio is CopyAudioIntent;

    public bool RequiresInterpolation => Video is EncodeVideoIntent { UseFrameInterpolation: true };
}
