using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Runtime.Tests.Scenarios;

public sealed class ToH264RifeScenarioTests
{
    [Fact]
    public void BuildDecision_WhenTargetFpsIsNotSpecified_UsesDoubleSourceFrameRate()
    {
        var sut = CreateSut();
        var video = CreateVideo(framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);
        var encodeVideo = actual.Video.Should().BeOfType<EncodeVideoIntent>().Subject;

        actual.RequiresInterpolation.Should().BeTrue();
        actual.ResolvedTargetFramesPerSecond.Should().BeApproximately(48000d / 1001d, 0.0001);
        actual.UserFacingTargetFramesPerSecond.Should().Be(48);
        encodeVideo.UseFrameInterpolation.Should().BeTrue();
        encodeVideo.TargetFramesPerSecond.Should().BeApproximately(48000d / 1001d, 0.0001);
    }

    [Fact]
    public void BuildDecision_WhenTargetFpsIs60ForNtscSource_Uses5994()
    {
        var sut = CreateSut(targetFramesPerSecond: 60);
        var video = CreateVideo(framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.RequiresInterpolation.Should().BeTrue();
        actual.ResolvedTargetFramesPerSecond.Should().BeApproximately(60000d / 1001d, 0.0001);
        actual.UserFacingTargetFramesPerSecond.Should().Be(60);
    }

    [Fact]
    public void BuildDecision_WhenSourceIsAlreadyNearTarget_DoesNotRequireInterpolation()
    {
        var sut = CreateSut(targetFramesPerSecond: 60);
        var video = CreateVideo(framesPerSecond: 60000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.RequiresInterpolation.Should().BeFalse();
        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
    }

    [Fact]
    public void FormatInfo_WhenTargetFpsIsNotSpecified_ShowsResolvedTargetFps()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.FormatInfo(video);

        actual.Should().Contain("1280x720");
        actual.Should().Contain("fps 23.976");
        actual.Should().Contain("target 47.952");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndInterpolationAreRequested_UsesRoundedFpsInOutputPath()
    {
        var sut = CreateSut(keepSource: true, targetFramesPerSecond: 60);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (60fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndNameAlreadyEndsWithParentheses_AppendsRoundedFpsInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, targetFramesPerSecond: 60);
        var video = CreateVideo(filePath: @"C:\video\input (2012).mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildDecision(video);

        actual.OutputPath.Should().Be(@"C:\video\input (2012, 60fps).mkv");
    }

    [Fact]
    public void BuildExecution_WhenInterpolationIsNeeded_BuildsRifeAndFfmpegCommands()
    {
        var tool = CreateTool();
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(targetFramesPerSecond: 60), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 24000d / 1001d);

        var actual = sut.BuildExecution(video);

        actual.IsEmpty.Should().BeFalse();
        actual.Commands.Should().Contain(command => command.Contains("rife-ncnn-vulkan", StringComparison.OrdinalIgnoreCase));
        actual.Commands.Should().Contain(command => command.Contains("-c:v h264_nvenc", StringComparison.OrdinalIgnoreCase));
        actual.Commands.Should().Contain(command => command.Contains("-c:a copy", StringComparison.OrdinalIgnoreCase));
        actual.Commands.Should().Contain(command => command.Contains("%%08d.png", StringComparison.Ordinal));
        actual.Commands.Take(2).Should().OnlyContain(command => command.Contains("|| ver > nul", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildExecution_WhenInterpolationIsNotNeededAndContainerMatches_ReturnsEmptyExecution()
    {
        var tool = CreateTool();
        var sut = new ToH264RifeScenario(new ToH264RifeRequest(targetFramesPerSecond: 60), tool);
        var video = CreateVideo(filePath: @"C:\video\input.mkv", framesPerSecond: 60000d / 1001d);

        var actual = sut.BuildExecution(video);

        actual.IsEmpty.Should().BeTrue();
    }

    private static ToH264RifeScenario CreateSut(
        bool keepSource = false,
        int? targetFramesPerSecond = null,
        string? outputContainer = null)
    {
        return new ToH264RifeScenario(
            new ToH264RifeRequest(
                keepSource: keepSource,
                targetFramesPerSecond: targetFramesPerSecond,
                outputContainer: outputContainer),
            CreateTool());
    }

    private static ToH264RifeTool CreateTool()
    {
        return new ToH264RifeTool(
            "ffmpeg",
            "rife-ncnn-vulkan",
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
        long? bitrate = 5_000_000)
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
            primaryAudioBitrate: 128_000,
            primaryAudioSampleRate: 48_000,
            primaryAudioChannels: 2);
    }
}
