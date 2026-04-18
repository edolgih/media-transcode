using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.Failures;
using Transcode.Core.MediaIntent;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using Transcode.Core.VideoSettings.Profiles;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Runtime.Tests.Scenarios;

/*
Это тесты сценария tomkvgpu.
Они покрывают принятие решений между remux, encode и explicit downscale путями.
*/
/// <summary>
/// Verifies decision behavior of the ToMkvGpu scenario.
/// </summary>
public sealed class ToMkvGpuScenarioTests
{
    [Theory]
    [InlineData("h264")]
    [InlineData("mpeg4")]
    public void BuildDecision_WhenVideoCodecIsCopyCompatibleAndNoOverrides_CopiesVideo(string videoCodec)
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: videoCodec, audioCodecs: ["mp3"], container: "mkv");

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.Video.Should().BeOfType<CopyVideoIntent>();
        actual.TargetContainer.Should().Be(TargetContainer.Mkv);
        actual.OutputPath.Should().Be(video.FilePath);
    }

    [Fact]
    public void BuildDecision_WhenSourceContainerIsNotMkv_ReturnsMkvOutputPath()
    {
        var sut = CreateSut();
        var video = CreateVideo(container: "mp4");

        var actual = sut.BuildDecision(video);

        actual.TargetContainer.Should().Be(TargetContainer.Mkv);
        actual.OutputPath.Should().Be(@"C:\video\input.mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndMkvInputRequiresEncode_ReturnsDistinctOutputPath()
    {
        var sut = CreateSut(keepSource: true);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv");

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input_out.mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_UsesTargetHeightInOutputPath()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 720);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (720p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceIsDisabledAndDownscaleIsRequested_UsesTargetHeightInOutputPath()
    {
        var sut = CreateSut(keepSource: false, downscaleTarget: 720);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeFalse();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (720p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_UsesParenthesizedTargetHeightInOutputPath()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 720);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input.mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (720p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequestedForFileEndingWithYear_AppendsTargetHeightInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 720);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input (2012).mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (2012, 720p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_ReplacesExistingHeightMarkerInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 576);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input (2012, 720p).mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (2012, 576p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_PreservesNonHeightMarkersInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 576);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input (59fps, 720p).mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (59fps, 576p).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_PreservesMarkerOrderWhenHeightComesFirst()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 576);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input (720p, 59fps).mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (576p, 59fps).mkv");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceIsDisabledAndDownscaleIsRequested_ReplacesExistingHeightMarkerInsideParentheses()
    {
        var sut = CreateSut(keepSource: false, downscaleTarget: 576);
        var video = CreateVideo(container: "mkv", videoCodec: "av1", filePath: @"C:\video\input (2012, 720p).mkv", height: 1080);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeFalse();
        actual.CopyVideo.Should().BeFalse();
        actual.OutputPath.Should().Be(@"C:\video\input (2012, 576p).mkv");
    }

    [Theory]
    [InlineData(@"C:\video\input.wmv")]
    [InlineData(@"C:\video\input.asf")]
    public void BuildDecision_WhenInputIsAsfFamily_ForcesEncodeAndTimestampFix(string filePath)
    {
        var sut = CreateSut();
        var video = CreateVideo(container: Path.GetExtension(filePath).TrimStart('.'), videoCodec: "h264", audioCodecs: ["aac"], filePath: filePath);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        actual.FixTimestamps.Should().BeTrue();
        GetRequiredEncodeVideo(actual).TargetVideoCodec.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void BuildDecision_WhenVideoCodecIsNotCopyCompatible_UsesH264GpuEncodePlan()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "av1", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        actual.CopyVideo.Should().BeFalse();
        encodeVideo.TargetVideoCodec.Should().Be(TargetVideoCodec.H264);
        encodeVideo.CompatibilityProfile.Should().Be(H264OutputProfile.H264High);
        encodeVideo.EncoderPreset.Should().Be(NvencPreset.P6);
        encodeVideo.PreferredBackend.Should().Be(VideoBackend.Gpu);
        actual.CopyAudio.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenOverlayBackgroundIsRequested_ForcesVideoEncode()
    {
        var sut = CreateSut(overlayBackground: true);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        actual.ApplyOverlayBackground.Should().BeTrue();
        GetRequiredEncodeVideo(actual).TargetVideoCodec.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void BuildDecision_WhenAudioContainsNonMp3_ForcesAudioEncodeWhileKeepingVideoCopy()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["mp3", "ac3"]);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        actual.SynchronizeAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenSynchronizeAudioIsRequested_ForcesAudioEncode()
    {
        var sut = CreateSut(synchronizeAudio: true);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["mp3"]);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
    }

    [Fact]
    public void BuildDecision_WhenMaxFpsIsLowerThanSourceFrameRate_ForcesVideoEncodeWithTargetFrameRate()
    {
        var sut = CreateSut(maxFramesPerSecond: 50);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"], framesPerSecond: 59.94);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        GetRequiredEncodeVideo(actual).TargetFramesPerSecond.Should().Be(50);
        actual.FixTimestamps.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenMaxFpsIsNotLowerThanSourceFrameRate_DoesNotForceVideoEncode()
    {
        var sut = CreateSut(maxFramesPerSecond: 30);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["mp3"], framesPerSecond: 23.976);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
        actual.Video.Should().BeOfType<CopyVideoIntent>();
    }

    [Fact]
    public void BuildDecision_WhenForceEncodeIsRequestedOnCopyCompatibleSource_UsesEncodeAtSourceResolution()
    {
        var sut = CreateSut(forceEncode: true);
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["mp3"], container: "mkv", height: 1080);

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        encodeVideo.Downscale.Should().BeNull();
        actual.VideoResolution.Should().NotBeNull();
        actual.VideoResolution!.Profile.TargetHeight.Should().Be(1080);
    }

    [Fact]
    public void Ctor_WhenMaxFpsIsNotSupported_ThrowsArgumentOutOfRangeException()
    {
        Action action = static () => _ = new ToMkvGpuRequest(maxFramesPerSecond: 55);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxFramesPerSecond");
    }

    [Fact]
    public void BuildDecision_WhenVideoIsEncoded_AlwaysEncodesAudio()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "hevc", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenDownscale576RequestedForLargerSource_AppliesDownscaleAndForcesEncode()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 1080, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        encodeVideo.VideoSettings.Should().BeNull();
        encodeVideo.Downscale.Should().NotBeNull();
        encodeVideo.Downscale!.TargetHeight.Should().Be(576);
        actual.CopyVideo.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
        encodeVideo.TargetVideoCodec.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void BuildDecision_WhenDownscale576RequestedFor640Source_AppliesDownscaleWithoutBucketError()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 640, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        encodeVideo.Downscale.Should().NotBeNull();
        encodeVideo.Downscale!.TargetHeight.Should().Be(576);
        actual.CopyVideo.Should().BeFalse();
        actual.FixTimestamps.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenDownscale576RequestedForSmallerSource_DoesNotApplyDownscale()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 480, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.Video.Should().BeOfType<CopyVideoIntent>();
        actual.CopyVideo.Should().BeTrue();
    }

    [Fact]
    public void BuildDecision_WhenDownscale480RequestedForLargerSource_AppliesDownscale()
    {
        var sut = CreateSut(downscaleTarget: 480);
        var video = CreateVideo(height: 692, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(480);
        actual.CopyVideo.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenDownscale424RequestedForLargerSource_AppliesDownscale()
    {
        var sut = CreateSut(downscaleTarget: 424);
        var video = CreateVideo(height: 576, videoCodec: "h264", audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(424);
        actual.CopyVideo.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenDownscale576RequestedForZeroHeight_ThrowsBucketHint()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(height: 0, videoCodec: "h264", audioCodecs: ["aac"]);

        var action = () => sut.BuildDecision(video);

        action.Should().Throw<RuntimeFailureException>()
            .Which.Code.Should().Be(RuntimeFailureCode.DownscaleSourceBucketIssue);
    }

    [Fact]
    public void BuildDecision_WhenDownscale480RequestedForZeroHeight_ThrowsBucketHint()
    {
        var sut = CreateSut(downscaleTarget: 480);
        var video = CreateVideo(height: 0, videoCodec: "h264", audioCodecs: ["aac"]);

        var action = () => sut.BuildDecision(video);

        action.Should().Throw<RuntimeFailureException>()
            .Which.Code.Should().Be(RuntimeFailureCode.DownscaleSourceBucketIssue);
    }

    [Fact]
    public void BuildDecision_WhenEncodeOverridesAreRequestedWithoutResize_PreservesOverridesForToolRendering()
    {
        var sut = new ToMkvGpuScenario(new ToMkvGpuRequest(
            videoSettings: new VideoSettingsRequest(cq: 23),
            nvencPreset: "p5"));
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        var encodeVideo = GetRequiredEncodeVideo(actual);
        encodeVideo.VideoSettings.Should().NotBeNull();
        encodeVideo.VideoSettings!.Cq.Should().Be(23);
        encodeVideo.EncoderPreset.Should().Be(NvencPreset.P5);
    }

    [Fact]
    public void BuildDecision_WhenDownscaleUsesManualCqOnAsymmetricAnimeProfile_UsesDirectionalRateCorridor()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(
                downscale: new DownscaleRequest(424),
                videoSettings: new VideoSettingsRequest(
                    contentProfile: "anime",
                    qualityProfile: "default",
                    cq: 24)),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(
            container: "mp4",
            height: 1080,
            videoCodec: "av1",
            audioCodecs: ["aac"],
            filePath: @"C:\video\input.mp4");

        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;
        actual.VideoResolution.Should().NotBeNull();
        var settings = actual.VideoResolution!.Settings;

        settings.Cq.Should().Be(24);
        settings.Maxrate.Should().Be(2.1m);
        settings.Bufsize.Should().Be(4.2m);
    }

    [Fact]
    public void BuildDecision_WhenDownscale720RequestedForLargerSource_AppliesDownscaleAndForcesEncode()
    {
        var sut = CreateSut(downscaleTarget: 720);
        var video = CreateVideo();

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        encodeVideo.VideoSettings.Should().BeNull();
        encodeVideo.Downscale.Should().NotBeNull();
        encodeVideo.Downscale!.TargetHeight.Should().Be(720);
        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        encodeVideo.TargetVideoCodec.Should().Be(TargetVideoCodec.H264);
    }

    [Fact]
    public void BuildDecision_WhenDownscale576SourceBucketIsMissing_ThrowsInformativeError()
    {
        var profile = new VideoSettingsProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(0.4m, 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300)
            ],
            defaults: CreateDefaults());
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(downscale: new DownscaleRequest(576)),
            VideoSettingsProfiles.Create(profile));
        var video = CreateVideo(container: "mp4", height: 900, videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");

        var action = () => sut.BuildDecision(video);

        action.Should().Throw<RuntimeFailureException>()
            .Which.Code.Should().Be(RuntimeFailureCode.DownscaleSourceBucketIssue);
    }

    [Fact]
    public void BuildDecision_WhenPlanCopiesVideo_ReturnsDecisionWithoutEncodePayload()
    {
        var sut = CreateSut();
        var video = CreateVideo(videoCodec: "h264", audioCodecs: ["aac"], container: "mkv");
        var actual = sut.BuildDecision(video);

        actual.VideoResolution.Should().BeNull();
    }

    [Fact]
    public void BuildDecision_WhenEncodeIsRequired_ReturnsResolvedTomkvgpuPayload()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4", bitrate: 10_000_000);
        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;

        actual.VideoResolution.Should().NotBeNull();
        actual.SourceBitrate.Should().NotBeNull();
        var videoResolution = actual.VideoResolution!;
        var sourceBitrate = actual.SourceBitrate!;

        videoResolution.Settings.ContentProfile.Value.Should().Be("film");
        videoResolution.Settings.QualityProfile.Value.Should().Be("default");
        sourceBitrate.Origin.Should().Be("probe");
    }

    [Fact]
    public void BuildDecision_WhenEncodeUsesProbeTotalBitrateWithKnownAudio_ResolvesVideoOnlySourceBitrate()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            filePath: @"C:\video\input.mp4",
            audioCodecs: ["aac", "aac"],
            bitrate: 10_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;

        actual.SourceBitrate.Should().NotBeNull();
        var sourceBitrate = actual.SourceBitrate!;
        sourceBitrate.Bitrate.Should().Be(9_000_000);
        sourceBitrate.Origin.Should().Be("probe_minus_audio");
    }

    [Fact]
    public void BuildDecision_WhenFilmAccurateAutosampleIsRequested_UsesFixedBucketDefaults()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4", bitrate: 10_000_000);
        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;

        actual.VideoResolution.Should().NotBeNull();
        var videoResolution = actual.VideoResolution!;

        videoResolution.Settings.Cq.Should().Be(20);
        videoResolution.Settings.Maxrate.Should().Be(5.8m);
        videoResolution.Settings.Bufsize.Should().Be(11.6m);
    }

    [Fact]
    public void BuildDecision_WhenFilmProfileSourceBitrateIsLowerThanBucketMaxrate_CapsRateControlToVideoOnlySourceBitrate()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            filePath: @"C:\video\input.mp4",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;
        actual.VideoResolution.Should().NotBeNull();
        var settings = actual.VideoResolution!.Settings;

        settings.Cq.Should().Be(20);
        settings.Maxrate.Should().Be(2.0m);
        settings.Bufsize.Should().Be(4.0m);
    }

    [Fact]
    public void BuildDecision_WhenFilmProfileHasManualRateOverrides_DoesNotApplySourceBitrateCap()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default",
                maxrate: 6.5m,
                bufsize: 13.0m)),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            filePath: @"C:\video\input.mp4",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video).Should().BeOfType<ToMkvGpuDecision>().Subject;
        actual.VideoResolution.Should().NotBeNull();
        var settings = actual.VideoResolution!.Settings;

        settings.Maxrate.Should().Be(6.5m);
        settings.Bufsize.Should().Be(13.0m);
    }

    [Fact]
    public void FormatInfo_WhenEncodeDecisionIsNeeded_RendersInfoWithoutExecutionPayload()
    {
        var sut = new ToMkvGpuScenario(
            new ToMkvGpuRequest(
                downscale: new DownscaleRequest(576),
                videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")),
            VideoSettingsProfiles.Default);
        var video = CreateVideo(
            container: "mp4",
            videoCodec: "av1",
            filePath: @"C:\video\input.mp4",
            height: 1080,
            bitrate: 10_000_000);

        var actual = sut.FormatInfo(video);

        actual.Should().Contain("vcodec av1");
        actual.Should().Contain("1920x1080 fps 29.97");
    }

    [Fact]
    public void BuildExecution_WhenDecisionIsNoOp_ReturnsEmptyExecution()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["mp3"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut().BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void BuildExecution_WhenEncodeIsRequired_BuildsNvencCommandAndDeleteStep()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", audioCodecs: ["ac3"], filePath: @"C:\video\input.mp4");
        var decision = CreateSut().BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands.Should().HaveCount(2);
        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-preset p6");
        actual.Commands[0].Should().Contain("-c:a libmp3lame");
        actual.Commands[0].Should().Contain("-q:a 2");
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenForceEncodeIsRequestedOnCopyCompatibleSource_BuildsEncodeCommandWithoutResize()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["mp3"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut(forceEncode: true).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().NotContain("scale_cuda=");
        actual.Commands[0].Should().NotContain("-filter_complex");
    }

    [Fact]
    public void BuildExecution_WhenOverlayBackgroundIsEnabled_UsesFilterComplex()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mp4", videoCodec: "av1", filePath: @"C:\video\input.mp4", width: 720, height: 1280);
        var decision = CreateSut(overlayBackground: true).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-filter_complex");
        actual.Commands[0].Should().Contain("overlay");
    }

    [Fact]
    public void BuildExecution_WhenSynchronizeAudioOnCopyVideo_UsesStrongSyncRemux()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["mp3"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut(synchronizeAudio: true).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:v copy -copytb 1");
        actual.Commands[0].Should().Contain("-c:a libmp3lame");
        actual.Commands[0].Should().Contain("-q:a 2");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
    }

    private static ToMkvGpuScenario CreateSut(
        bool overlayBackground = false,
        int? downscaleTarget = null,
        bool synchronizeAudio = false,
        bool keepSource = false,
        bool forceEncode = false,
        int? maxFramesPerSecond = null)
    {
        return new ToMkvGpuScenario(new ToMkvGpuRequest(
            overlayBackground: overlayBackground,
            synchronizeAudio: synchronizeAudio,
            keepSource: keepSource,
            forceEncode: forceEncode,
            downscale: downscaleTarget.HasValue ? new DownscaleRequest(downscaleTarget.Value) : null,
            maxFramesPerSecond: maxFramesPerSecond));
    }

    private static ToMkvGpuFfmpegTool CreateFfmpegTool()
    {
        return new ToMkvGpuFfmpegTool("ffmpeg", NullLogger<ToMkvGpuFfmpegTool>.Instance);
    }

    private static EncodeVideoIntent GetRequiredEncodeVideo(ToMkvGpuDecision decision)
    {
        return decision.Video.Should().BeOfType<EncodeVideoIntent>().Subject;
    }

    private static SourceVideo CreateVideo(
        string container = "mkv",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        string filePath = @"C:\video\input.mkv",
        long? bitrate = null,
        long? primaryAudioBitrate = null)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs ?? ["aac"],
            width: width,
            height: height,
            framesPerSecond: framesPerSecond,
            duration: TimeSpan.FromMinutes(10),
            bitrate: bitrate,
            primaryAudioBitrate: primaryAudioBitrate);
    }

    private static VideoSettingsDefaults[] CreateDefaults()
    {
        return
        [
            new VideoSettingsDefaults("anime", "high", 22, 3.3m, 6.5m, "bilinear", 19, 24, 2.4m, 4.2m),
            new VideoSettingsDefaults("anime", "default", 23, 2.4m, 4.8m, "bilinear", 20, 26, 2.0m, 3.0m),
            new VideoSettingsDefaults("anime", "low", 29, 2.1m, 4.1m, "bilinear", 24, 35, 1.0m, 3.2m),
            new VideoSettingsDefaults("mult", "high", 24, 2.7m, 5.3m, "bilinear", 21, 26, 2.4m, 3.2m),
            new VideoSettingsDefaults("mult", "default", 26, 2.4m, 4.8m, "bilinear", 23, 29, 2.0m, 2.8m),
            new VideoSettingsDefaults("mult", "low", 29, 1.7m, 3.5m, "bilinear", 26, 31, 1.6m, 2.0m),
            new VideoSettingsDefaults("film", "high", 24, 3.7m, 7.4m, "bilinear", 16, 33, 2.0m, 8.0m),
            new VideoSettingsDefaults("film", "default", 26, 3.4m, 6.9m, "bilinear", 18, 35, 1.6m, 8.0m),
            new VideoSettingsDefaults("film", "low", 30, 2.2m, 4.5m, "bilinear", 20, 38, 1.2m, 4.0m)
        ];
    }

}
