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
    public void Ctor_WhenCopyVideoPlanRequestsDownscale_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(targetHeight: 576);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request downscale*");
    }

    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsTargetFrameRate_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(targetFramesPerSecond: 60);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request target frame rate*");
    }

    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsInterpolation_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(useFrameInterpolation: true);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request frame interpolation*");
    }

    [Fact]
    public void Ctor_WhenCopyVideoPlanRequestsCompatibilityProfile_ThrowsArgumentException()
    {
        Action action = () => CreateCopyVideoPlan(videoCompatibilityProfile: VideoCompatibilityProfile.H264High);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*Video copy plan cannot request compatibility profile*");
    }

    [Fact]
    public void Ctor_WhenInterpolationHasNoTargetFrameRate_ThrowsArgumentException()
    {
        Action action = () => new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            videoCompatibilityProfile: VideoCompatibilityProfile.H264High,
            targetFramesPerSecond: null,
            useFrameInterpolation: true,
            videoSettings: null,
            downscale: null,
            copyVideo: false,
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
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            videoCompatibilityProfile: null,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            videoSettings: null,
            downscale: null,
            copyVideo: false,
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
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            videoCompatibilityProfile: VideoCompatibilityProfile.H264High,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            videoSettings: new VideoSettingsRequest(contentProfile: "film"),
            downscale: new DownscaleRequest(576),
            copyVideo: false,
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
            targetVideoCodec: " H264 ",
            preferredBackend: " Nvenc ",
            videoCompatibilityProfile: VideoCompatibilityProfile.H264High,
            targetFramesPerSecond: 23.976,
            useFrameInterpolation: false,
            videoSettings: new VideoSettingsRequest(contentProfile: "film"),
            downscale: new DownscaleRequest(576),
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true,
            encoderPreset: " P5 ",
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
            targetVideoCodec: "h264",
            preferredBackend: "nvenc",
            videoCompatibilityProfile: VideoCompatibilityProfile.H264High,
            targetFramesPerSecond: null,
            useFrameInterpolation: false,
            videoSettings: null,
            downscale: null,
            copyVideo: false,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);

        actual.VideoSettings.Should().BeNull();
    }

    private static TranscodePlan CreateCopyVideoPlan(
        int? targetHeight = null,
        double? targetFramesPerSecond = null,
        bool useFrameInterpolation = false,
        VideoCompatibilityProfile? videoCompatibilityProfile = null)
    {
        return new TranscodePlan(
            targetContainer: "mkv",
            targetVideoCodec: null,
            preferredBackend: null,
            videoCompatibilityProfile: videoCompatibilityProfile,
            targetFramesPerSecond: targetFramesPerSecond,
            useFrameInterpolation: useFrameInterpolation,
            videoSettings: null,
            downscale: targetHeight.HasValue ? new DownscaleRequest(targetHeight.Value) : null,
            copyVideo: true,
            copyAudio: true,
            fixTimestamps: false,
            keepSource: true);
    }
}
