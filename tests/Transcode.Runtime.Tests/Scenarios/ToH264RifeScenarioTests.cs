using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Runtime.Tests.Scenarios;

public sealed class ToH264RifeScenarioTests
{
    [Fact]
    public void RequestCtor_WhenInterpolationQualityProfileIsEmpty_Throws()
    {
        Action action = static () => _ = new ToH264RifeRequest(interpolationQualityProfile: "");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("interpolationQualityProfile");
    }

    [Fact]
    public void RequestCtor_WhenOutputContainerIsWhitespace_Throws()
    {
        Action action = static () => _ = new ToH264RifeRequest(outputContainer: " ");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("outputContainer");
    }

    [Fact]
    public void BuildDecision_WhenMultiplierIsNotSpecified_UsesDoubleSourceFrameRate()
    {
        var sut = CreateSut();
        var video = CreateVideo(framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);
        var encodeVideo = actual.Video.Should().BeOfType<EncodeVideoIntent>().Subject;

        actual.RequiresInterpolation.Should().BeTrue();
        actual.FramesPerSecondMultiplier.Should().Be(2);
        actual.ResolvedTargetFramesPerSecond.Should().BeApproximately(48000d / 1001d, 0.0001);
        actual.UserFacingTargetFramesPerSecond.Should().Be(48);
        actual.InterpolationQualityProfile.Should().Be(InterpolationQualityProfile.Default);
        actual.InterpolationModelName.Should().Be(InterpolationModelName.Rife425);
        actual.ResolvedVideoSettings.ContentProfile.Should().Be(VideoContentProfile.Film);
        actual.ResolvedVideoSettings.QualityProfile.Should().Be(VideoQualityProfile.Default);
        actual.ResolvedVideoSettings.Cq.Should().Be(22);
        actual.ResolvedVideoSettings.Maxrate.Should().Be(5.272m);
        actual.ResolvedVideoSettings.Bufsize.Should().Be(10.544m);
        encodeVideo.UseFrameInterpolation.Should().BeTrue();
        encodeVideo.TargetFramesPerSecond.Should().BeApproximately(48000d / 1001d, 0.0001);
    }

    [Fact]
    public void BuildDecision_WhenMultiplierIsThree_UsesTripleSourceFrameRate()
    {
        var sut = CreateSut(framesPerSecondMultiplier: 3);
        var video = CreateVideo(framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.RequiresInterpolation.Should().BeTrue();
        actual.FramesPerSecondMultiplier.Should().Be(3);
        actual.ResolvedTargetFramesPerSecond.Should().BeApproximately(72000d / 1001d, 0.0001);
        actual.UserFacingTargetFramesPerSecond.Should().Be(72);
    }

    [Fact]
    public void FormatInfo_WhenMultiplierIsSpecified_ShowsMultiplierAndResolvedTargetFps()
    {
        var sut = CreateSut(framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.FormatInfo(video);

        actual.Should().Contain("1280x720");
        actual.Should().Contain("fps 23.976");
        actual.Should().Contain("x3");
        actual.Should().Contain("target 71.928");
        actual.Should().Contain("interp default/4.25");
        actual.Should().Contain("profile film/default");
        actual.Should().Contain("cq 22");
        actual.Should().Contain("maxrate 5.672M");
        actual.Should().Contain("bufsize 11.344M");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceIsDisabledAndInterpolationIsRequested_UsesRoundedFpsInOutputPath()
    {
        var sut = CreateSut(keepSource: false, framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (72fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndInterpolationAreRequested_UsesRoundedFpsInOutputPath()
    {
        var sut = CreateSut(keepSource: true, framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (72fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndNameAlreadyEndsWithParentheses_AppendsRoundedFpsInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input (2012).mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (2012, 72fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenNameAlreadyContainsFpsMarker_ReplacesItWithResolvedFps()
    {
        var sut = CreateSut(keepSource: false, framesPerSecondMultiplier: 2);
        var video = CreateVideo(filePath: @"C:\video\input (59fps).mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (48fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenNameContainsMixedMarkers_ReplacesOnlyFpsMarker()
    {
        var sut = CreateSut(keepSource: true, framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input (720p, 59fps).mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (720p, 72fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenNameContainsMixedMarkersAndFpsComesFirst_PreservesMarkerOrder()
    {
        var sut = CreateSut(keepSource: true, framesPerSecondMultiplier: 3);
        var video = CreateVideo(filePath: @"C:\video\input (59fps, 720p).mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (72fps, 720p).mkv");
    }

    [Fact]
    public void BuildExecution_WhenInterpolationIsNeeded_BuildsDockerRunCommand()
    {
        var tool = CreateTool();
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(framesPerSecondMultiplier: 3), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildExecution(video);

        actual.IsEmpty.Should().BeFalse();
        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("docker run --rm --gpus all");
        actual.Commands[0].Should().Contain("media-transcode-rife-trt");
        actual.Commands[0].Should().Contain("\"C:\\video:/workspace/work\"");
        actual.Commands[0].Should().Contain("\"/workspace/work/input.mkv\"");
        actual.Commands[0].Should().Contain("\"/workspace/work/input (72fps).mkv\"");
        actual.Commands[0].Should().Contain(" 3 mkv 4.25 22 5672 11344");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mkv\"");
        actual.Commands.Should().NotContain(command => command.Contains(".png", StringComparison.OrdinalIgnoreCase));
        actual.Commands.Should().NotContain(command => command.Contains("rife-ncnn-vulkan", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildDecision_WhenProfilesAreSpecified_UsesResolvedProfileDefaultsAndInterpolationModel()
    {
        var sut = CreateSut(
            interpolationQualityProfile: "high",
            videoSettings: VideoSettingsRequest.CreateOrNull(contentProfile: "anime", qualityProfile: "high"));
        var video = CreateVideo(height: 720);

        var actual = sut.BuildDecision(video);

        actual.InterpolationQualityProfile.Should().Be(InterpolationQualityProfile.High);
        actual.InterpolationModelName.Should().Be(InterpolationModelName.Rife426Heavy);
        actual.ResolvedVideoSettings.ContentProfile.Should().Be(VideoContentProfile.Anime);
        actual.ResolvedVideoSettings.QualityProfile.Should().Be(VideoQualityProfile.High);
        actual.ResolvedVideoSettings.Cq.Should().Be(22);
        actual.ResolvedVideoSettings.Maxrate.Should().Be(4.0m);
        actual.ResolvedVideoSettings.Bufsize.Should().Be(8.0m);
        actual.ResolvedVideoSettings.MaxrateKbps.Should().Be(4000);
        actual.ResolvedVideoSettings.BufsizeKbps.Should().Be(8000);
    }

    [Fact]
    public void BuildDecision_WhenManualCqImprovesAnimeEncode_UsesDirectionalRateCorridorBeforeInterpolationUplift()
    {
        var sut = CreateSut(
            videoSettings: VideoSettingsRequest.CreateOrNull(
                contentProfile: "anime",
                qualityProfile: "default",
                cq: 20));
        var video = CreateVideo(height: 1080, width: 1920);

        var actual = sut.BuildDecision(video);

        actual.ResolvedVideoSettings.ContentProfile.Should().Be(VideoContentProfile.Anime);
        actual.ResolvedVideoSettings.QualityProfile.Should().Be(VideoQualityProfile.Default);
        actual.ResolvedVideoSettings.Cq.Should().Be(20);
        actual.ResolvedVideoSettings.Maxrate.Should().Be(4.6m);
        actual.ResolvedVideoSettings.Bufsize.Should().Be(9.2m);
        actual.ResolvedVideoSettings.MaxrateKbps.Should().Be(4600);
        actual.ResolvedVideoSettings.BufsizeKbps.Should().Be(9200);
    }

    [Fact]
    public void BuildDecision_WhenSourceBitrateContainsMultiAudio_UsesVideoOnlyEstimateBeforeInterpolationUplift()
    {
        var sut = CreateSut(
            videoSettings: VideoSettingsRequest.CreateOrNull(contentProfile: "mult", qualityProfile: "default"));
        var video = CreateVideo(
            bitrate: 5_000_000,
            audioCodecs: ["aac", "aac"],
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);

        actual.ResolvedVideoSettings.ContentProfile.Should().Be(VideoContentProfile.Mult);
        actual.ResolvedVideoSettings.QualityProfile.Should().Be(VideoQualityProfile.Default);
        actual.ResolvedVideoSettings.Cq.Should().Be(23);
        actual.ResolvedVideoSettings.Maxrate.Should().Be(4.4m);
        actual.ResolvedVideoSettings.Bufsize.Should().Be(8.8m);
    }

    [Fact]
    public void BuildExecution_WhenExecutableNameIsBareCommand_DoesNotQuoteIt()
    {
        var tool = CreateTool();
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mp4", framesPerSecond: 25);

        var actual = sut.BuildExecution(video);

        actual.Commands[0].Should().StartWith("docker run --rm --gpus all");
        actual.Commands.Should().NotContain(command => command.StartsWith("\"docker\"", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildExecution_WhenExecutablePathContainsSpaces_QuotesIt()
    {
        var tool = new ToH264RifeTool(
            "media-transcode-rife-trt",
            NullLogger<ToH264RifeTool>.Instance);
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mp4", framesPerSecond: 25);

        var actual = sut.BuildExecution(video);

        actual.Commands[0].Should().StartWith("docker run --rm --gpus all");
    }

    [Fact]
    public void BuildExecution_WhenKeepSourceIsDisabled_DeletesSourceAfterWritingFpsSuffixedOutput()
    {
        var tool = CreateTool();
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 25);

        var actual = sut.BuildExecution(video);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("\"/workspace/work/input (50fps).mkv\"");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mkv\"");
    }

    private static ToH264RifeScenario CreateSut(
        bool keepSource = false,
        int framesPerSecondMultiplier = 2,
        string? interpolationQualityProfile = null,
        string? outputContainer = null,
        VideoSettingsRequest? videoSettings = null)
    {
        return new ToH264RifeScenario(
            new ToH264RifeRequest(
                keepSource: keepSource,
                framesPerSecondMultiplier: framesPerSecondMultiplier,
                interpolationQualityProfile: interpolationQualityProfile,
                outputContainer: outputContainer,
                videoSettings: videoSettings),
            CreateTool());
    }

    private static ToH264RifeTool CreateTool()
    {
        return new ToH264RifeTool(
            "media-transcode-rife-trt",
            NullLogger<ToH264RifeTool>.Instance);
    }

    private static SourceVideo CreateVideo(
        string filePath = @"C:\video\input.mkv",
        string container = "mkv",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1280,
        int height = 720,
        double framesPerSecond = 25,
        TimeSpan? duration = null,
        long? bitrate = 5_000_000,
        long? primaryAudioBitrate = 128_000,
        long? primaryVideoBitrate = null)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: width,
            height: height,
            framesPerSecond: framesPerSecond,
            duration: duration ?? TimeSpan.FromMinutes(10),
            bitrate: bitrate,
            formatName: null,
            rawFramesPerSecond: null,
            averageFramesPerSecond: null,
            primaryAudioBitrate: primaryAudioBitrate,
            primaryAudioSampleRate: 48_000,
            primaryAudioChannels: 2,
            primaryVideoBitrate: primaryVideoBitrate);
    }
}
