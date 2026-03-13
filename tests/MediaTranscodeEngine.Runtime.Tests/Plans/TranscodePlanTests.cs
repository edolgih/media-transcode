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
        actual.TargetVideoCodec.Should().BeNull();
        actual.PreferredBackend.Should().BeNull();
        actual.VideoCompatibilityProfile.Should().BeNull();
        actual.TargetFramesPerSecond.Should().BeNull();
        actual.VideoSettings.Should().BeNull();
        actual.Downscale.Should().BeNull();
        actual.EncoderPreset.Should().BeNull();
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
            copyAudio: true,
            fixTimestamps: false,
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
            copyAudio: true,
            fixTimestamps: false,
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
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        actual.TargetHeight.Should().Be(576);
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
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true,
            outputPath: @".\output.mkv");

        actual.TargetContainer.Should().Be("mkv");
        actual.TargetVideoCodec.Should().Be("h264");
        actual.PreferredBackend.Should().Be("nvenc");
        actual.VideoCompatibilityProfile.Should().Be(VideoCompatibilityProfile.H264High);
        actual.EncoderPreset.Should().Be("p5");
        actual.OutputPath.Should().Be(Path.GetFullPath(@".\output.mkv"));
        actual.TargetHeight.Should().Be(576);
        actual.Downscale!.TargetHeight.Should().Be(576);
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
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        actual.VideoSettings.Should().BeNull();
    }

    private static TranscodePlan CreateCopyVideoPlan()
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            video: new CopyVideoPlan(),
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);
    }
}
