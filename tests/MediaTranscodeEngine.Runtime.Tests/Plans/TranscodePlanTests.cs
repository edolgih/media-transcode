using FluentAssertions;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Plans;

namespace MediaTranscodeEngine.Runtime.Tests.Plans;

/*
Это тесты инвариантов TranscodePlan.
Они проверяют допустимые комбинации copy/encode intent и связанных video settings.
*/
/// <summary>
/// Verifies TranscodePlan invariants and invalid plan combinations.
/// </summary>
public sealed class TranscodePlanTests
{
    [Fact]
    public void Ctor_WhenVideoPlanIsCopy_ExposesCopyModeWithoutEncodeProperties()
    {
        var actual = CreateCopyVideoPlan();

        actual.CopyVideo.Should().BeTrue();
        actual.RequiresVideoEncode.Should().BeFalse();
        actual.ChangesResolution.Should().BeFalse();
        actual.ChangesFrameRate.Should().BeFalse();
        actual.Video.Should().BeOfType<CopyVideoPlan>();
    }

    [Fact]
    public void Ctor_WhenAudioPlanIsCopy_ExposesCopyModeWithoutRepairFlags()
    {
        var actual = CreateCopyVideoPlan();

        actual.CopyAudio.Should().BeTrue();
        actual.RequiresAudioEncode.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
        actual.SynchronizeAudio.Should().BeFalse();
        actual.Audio.Should().BeOfType<CopyAudioPlan>();
    }

    [Fact]
    public void Ctor_WhenAudioPlanRequiresRepair_ExposesRepairWithoutSynchronization()
    {
        var actual = new TranscodePlan(
            targetContainer: "mkv",
            video: new CopyVideoPlan(),
            audio: new RepairAudioPlan(),
            keepSource: true);

        actual.CopyAudio.Should().BeFalse();
        actual.RequiresAudioEncode.Should().BeTrue();
        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeFalse();
        actual.Audio.Should().BeOfType<RepairAudioPlan>();
    }

    [Fact]
    public void Ctor_WhenAudioPlanRequiresSynchronization_ExposesRepairAndSyncFlags()
    {
        var actual = new TranscodePlan(
            targetContainer: "mkv",
            video: new CopyVideoPlan(),
            audio: new SynchronizeAudioPlan(),
            keepSource: true);

        actual.CopyAudio.Should().BeFalse();
        actual.RequiresAudioEncode.Should().BeTrue();
        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
        actual.Audio.Should().BeOfType<SynchronizeAudioPlan>();
    }

    [Fact]
    public void Ctor_WhenInterpolationHasNoTargetFrameRate_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "nvenc",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                TargetFramesPerSecond: null,
                UseFrameInterpolation: true),
            audio: new CopyAudioPlan(),
            keepSource: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Frame interpolation requires a target frame rate*");
    }

    [Fact]
    public void Ctor_WhenH264EncodePlanHasNoCompatibilityProfile_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "nvenc",
                CompatibilityProfile: null,
                TargetFramesPerSecond: null,
                UseFrameInterpolation: false),
            audio: new CopyAudioPlan(),
            keepSource: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*H.264 encode plan requires compatibility profile*");
    }

    [Fact]
    public void Ctor_WhenDownscaleIsProvided_DerivesTargetHeightFromDownscale()
    {
        var actual = new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "nvenc",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                VideoSettings: new VideoSettingsRequest(contentProfile: "film"),
                Downscale: new DownscaleRequest(576)),
            audio: new CopyAudioPlan(),
            keepSource: true);

        actual.ChangesResolution.Should().BeTrue();
        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(576);
    }

    [Fact]
    public void Ctor_WhenOptionalTokensAndPathAreProvided_NormalizesThem()
    {
        var actual = new TranscodePlan(
            targetContainer: " MKV ",
            video: new EncodeVideoPlan(
                TargetVideoCodec: " H264 ",
                PreferredBackend: " Nvenc ",
                CompatibilityProfile: VideoCompatibilityProfile.H264High,
                TargetFramesPerSecond: 23.976,
                VideoSettings: new VideoSettingsRequest(contentProfile: "film"),
                Downscale: new DownscaleRequest(576),
                EncoderPreset: " P5 "),
            audio: new CopyAudioPlan(),
            keepSource: true,
            outputPath: @".\output.mkv");

        var encodeVideo = GetRequiredEncodeVideo(actual);
        actual.TargetContainer.Should().Be("mkv");
        actual.OutputPath.Should().Be(Path.GetFullPath(@".\output.mkv"));
        encodeVideo.TargetVideoCodec.Should().Be("h264");
        encodeVideo.PreferredBackend.Should().Be("nvenc");
        encodeVideo.CompatibilityProfile.Should().Be(VideoCompatibilityProfile.H264High);
        encodeVideo.EncoderPreset.Should().Be("p5");
        encodeVideo.Downscale!.TargetHeight.Should().Be(576);
    }

    [Fact]
    public void Ctor_WhenVideoSettingsIsNull_KeepsItNull()
    {
        var actual = new TranscodePlan(
            targetContainer: "mkv",
            video: new EncodeVideoPlan(
                TargetVideoCodec: "h264",
                PreferredBackend: "nvenc",
                CompatibilityProfile: VideoCompatibilityProfile.H264High),
            audio: new CopyAudioPlan(),
            keepSource: true);

        GetRequiredEncodeVideo(actual).VideoSettings.Should().BeNull();
    }

    private static TranscodePlan CreateCopyVideoPlan()
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            video: new CopyVideoPlan(),
            audio: new CopyAudioPlan(),
            keepSource: true);
    }

    private static EncodeVideoPlan GetRequiredEncodeVideo(TranscodePlan plan)
    {
        return plan.Video.Should().BeOfType<EncodeVideoPlan>().Subject;
    }
}
