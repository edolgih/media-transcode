using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Runtime.Tests.Scenarios;

/*
Это тесты сценария toh264gpu.
Они проверяют выбор между remux и NVENC-перекодированием и влияние scenario-specific опций.
*/
/// <summary>
/// Verifies decision behavior of the ToH264Gpu scenario.
/// </summary>
public sealed class ToH264GpuScenarioTests
{
    [Fact]
    public void BuildDecision_WhenInputIsCopyCompatibleM4v_CreatesRemuxOnlyMp4Plan()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.m4v",
            container: "mov",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.TargetContainer.Should().Be("mp4");
        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
        actual.Video.Should().BeOfType<CopyVideoIntent>();
        actual.OutputPath.Should().Be(@"C:\video\input.mp4");
        spec.OptimizeForFastStart.Should().BeTrue();
        spec.MapPrimaryAudioOnly.Should().BeTrue();
        spec.VideoExecutionDetails.Should().BeNull();
        spec.AudioExecutionDetails.Should().BeNull();
    }

    [Fact]
    public void BuildDecision_WhenFrameRateLooksVariable_CreatesEncodePlan()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"],
            rawFramesPerSecond: 60,
            averageFramesPerSecond: 30);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeFalse();
        GetRequiredEncodeVideo(actual).TargetVideoCodec.Should().Be("h264");
        actual.CopyAudio.Should().BeTrue();
    }

    [Fact]
    public void BuildDecision_WhenSynchronizeAudioIsRequested_KeepsCopyCompatibleVideoAndDisablesAudioCopy()
    {
        var sut = CreateSut(synchronizeAudio: true);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenSynchronizeAudioIsRequested_PopulatesRepairAudioExecutionPayload()
    {
        var sut = CreateSut(synchronizeAudio: true);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        spec.VideoExecutionDetails.Should().BeNull();
        spec.AudioExecutionDetails.Should().NotBeNull();
        spec.AudioBitrateKbps.Should().Be(128);
        spec.AudioSampleRate.Should().Be(48000);
        spec.AudioChannels.Should().Be(2);
        spec.AudioFilter.Should().Be("aresample=async=1:first_pts=0");
    }

    [Theory]
    [InlineData(@"C:\video\input.wmv")]
    [InlineData(@"C:\video\input.asf")]
    public void BuildDecision_WhenInputExtensionRequiresTimestampRepair_EnablesTimestampRepair(string filePath)
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: filePath,
            container: Path.GetExtension(filePath).TrimStart('.'),
            formatName: "asf",
            videoCodec: "wmv3",
            audioCodecs: ["wma"]);

        var actual = sut.BuildDecision(video);

        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
    }

    [Fact]
    public void BuildDecision_WhenFormatNameContainsAsf_EnablesTimestampRepair()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.bin",
            container: "unknown",
            formatName: "asf,webm",
            videoCodec: "wmv3",
            audioCodecs: ["wma"]);

        var actual = sut.BuildDecision(video);

        actual.FixTimestamps.Should().BeTrue();
        actual.SynchronizeAudio.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
    }

    [Theory]
    [InlineData("aac")]
    [InlineData("mp3")]
    public void BuildDecision_WhenPrimaryAudioCodecIsCopyCompatible_EnablesAudioCopy(string codec)
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: [codec]);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
    }

    [Fact]
    public void BuildDecision_WhenPrimaryAudioCodecIsAc3_KeepsCopyCompatibleVideoAndDisablesAudioCopy()
    {
        // Arrange
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "h264",
            audioCodecs: ["ac3"],
            primaryAudioBitrate: 192_000);

        // Act
        var actual = sut.BuildDecision(video);
        var spec = actual;

        // Assert
        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeFalse();
        spec.VideoExecutionDetails.Should().BeNull();
        spec.AudioExecutionDetails.Should().NotBeNull();
        spec.AudioBitrateKbps.Should().Be(192);
    }

    [Fact]
    public void BuildDecision_WhenPrimaryAudioBitrateIsMissing_UsesDefaultAudioBitrate()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            primaryAudioBitrate: null);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.AudioBitrateKbps.Should().Be(192);
    }

    [Fact]
    public void BuildDecision_WhenOrdinaryEncodeHasNoBitrateHint_UsesProfileDefaults()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            duration: TimeSpan.Zero,
            bitrate: null);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeFalse();
        spec.VideoCq.Should().Be(20);
        spec.VideoMaxrateKbps.Should().Be(5800);
        spec.VideoBufferSizeKbps.Should().Be(11600);
    }

    [Fact]
    public void BuildDecision_WhenCqIsSpecified_UsesCqOverride()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(cq: 19)));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"]);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoCq.Should().Be(19);
        spec.VideoMaxrateKbps.Should().Be(6200);
        spec.VideoBufferSizeKbps.Should().Be(12400);
    }

    [Fact]
    public void BuildDecision_WhenOrdinaryAnimeEncodeUsesProfileOnlyRequest_UsesFixedBucketDefaults()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "anime",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"]);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(3400);
        spec.VideoBufferSizeKbps.Should().Be(6800);
    }

    [Fact]
    public void BuildDecision_WhenSynchronizeAudioForcesEncode_ResolvesVideoAutosampleFromVideoOnlySourceBitrate()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            synchronizeAudio: true,
            videoSettings: new VideoSettingsRequest(
                contentProfile: "mult",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 6_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeFalse();
        actual.CopyAudio.Should().BeFalse();
        actual.SynchronizeAudio.Should().BeTrue();
        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(4600);
        spec.VideoBufferSizeKbps.Should().Be(9200);
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceIsRequestedAndTargetPathMatchesSource_ReturnsDistinctOutputPath()
    {
        var sut = CreateSut(keepSource: true);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.OutputPath.Should().Be(@"C:\video\input_out.mp4");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceIsRequestedAndContainerChanges_ReturnsOutSuffixOutputPath()
    {
        var sut = CreateSut(keepSource: true);
        var video = CreateVideo(
            filePath: @"C:\video\input.ts",
            container: "ts",
            formatName: "mpegts",
            videoCodec: "h264",
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.OutputPath.Should().Be(@"C:\video\input_out.mp4");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequested_UsesTargetHeightInOutputPath()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 720);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            height: 1080,
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(720);
        actual.OutputPath.Should().Be(@"C:\video\input (720p).mp4");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequestedAndContainerChanges_UsesTargetHeightInOutputPath()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 424);
        var video = CreateVideo(
            filePath: @"C:\video\input.ts",
            container: "ts",
            formatName: "mpegts",
            videoCodec: "h264",
            height: 1080,
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(424);
        actual.OutputPath.Should().Be(@"C:\video\input (424p).mp4");
    }

    [Fact]
    public void BuildDecision_WhenKeepSourceAndDownscaleAreRequestedForFileEndingWithYear_AppendsTargetHeightInsideParentheses()
    {
        var sut = CreateSut(keepSource: true, downscaleTarget: 720);
        var video = CreateVideo(
            filePath: @"C:\video\input (2012).mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            height: 1080,
            audioCodecs: ["aac"]);

        var actual = sut.BuildDecision(video);

        actual.KeepSource.Should().BeTrue();
        actual.CopyVideo.Should().BeFalse();
        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(720);
        actual.OutputPath.Should().Be(@"C:\video\input (2012, 720p).mp4");
    }

    [Fact]
    public void BuildDecision_WhenEncodePresetIsNotSpecified_UsesP6Preset()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"]);

        var actual = sut.BuildDecision(video);

        GetRequiredEncodeVideo(actual).EncoderPreset.Should().Be("p6");
    }

    [Fact]
    public void BuildDecision_WhenVideoIsEncoded_EnablesDefaultAdaptiveQuantization()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"]);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.EnableAdaptiveQuantization.Should().BeTrue();
        spec.AqStrength.Should().BeNull();
    }

    [Fact]
    public void BuildDecision_WhenDownscaleIsRequestedAndSourceIsAlreadySmall_KeepsSourceDimensions()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"],
            height: 480);

        var actual = sut.BuildDecision(video);

        actual.Video.Should().BeOfType<CopyVideoIntent>();
        actual.CopyVideo.Should().BeTrue();
    }

    [Fact]
    public void BuildDecision_WhenDownscaleIsRequestedAndCopyIsStillSafe_CreatesRemuxOnlyPlan()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(
            filePath: @"C:\video\input.mp4",
            container: "mp4",
            formatName: "mov,mp4,m4a,3gp,3g2,mj2",
            videoCodec: "h264",
            audioCodecs: ["aac"],
            height: 480);

        var actual = sut.BuildDecision(video);

        actual.CopyVideo.Should().BeTrue();
        actual.CopyAudio.Should().BeTrue();
        actual.TargetContainer.Should().Be("mp4");
    }

    [Fact]
    public void BuildDecision_WhenDownscaleIsRequestedForLargeSource_CapsFrameRateTo30000Over1001()
    {
        var sut = CreateSut(downscaleTarget: 576);
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            height: 1080,
            framesPerSecond: 59.94);

        var actual = sut.BuildDecision(video);
        var encodeVideo = GetRequiredEncodeVideo(actual);

        encodeVideo.Downscale!.TargetHeight.Should().Be(576);
        encodeVideo.Downscale.Algorithm.Should().Be("bilinear");
        encodeVideo.TargetFramesPerSecond.Should().BeApproximately(30000d / 1001d, 0.0001);
        encodeVideo.VideoSettings.Should().BeNull();
    }

    [Fact]
    public void BuildDecision_WhenDownscaleUsesProfileOnlyRequest_UsesProfileDefaults()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            downscale: new DownscaleRequest(576),
            videoSettings: new VideoSettingsRequest(
                contentProfile: "anime",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            height: 1080);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(576);
        spec.VideoCq.Should().Be(23);
        spec.VideoMaxrateKbps.Should().Be(2400);
        spec.VideoBufferSizeKbps.Should().Be(4800);
    }

    [Fact]
    public void BuildDecision_WhenDownscale480UsesProfileOnlyRequest_UsesProfileDefaults()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            downscale: new DownscaleRequest(480),
            videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            height: 1080);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(480);
        spec.VideoCq.Should().Be(23);
        spec.VideoMaxrateKbps.Should().Be(3100);
        spec.VideoBufferSizeKbps.Should().Be(6200);
    }

    [Fact]
    public void BuildDecision_WhenFilmAccurateAutosampleIsRequested_UsesFixedBucketDefaultsWithoutSampleProvider()
    {
        var sut = CreateSut(
            new ToH264GpuRequest(
                videoSettings: new VideoSettingsRequest(
                    contentProfile: "film",
                    qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            bitrate: 10_000_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeFalse();
        spec.VideoCq.Should().Be(20);
        spec.VideoMaxrateKbps.Should().Be(5800);
        spec.VideoBufferSizeKbps.Should().Be(11600);
    }

    [Fact]
    public void BuildDecision_WhenFilmProfileSourceBitrateIsLowerThanBucketMaxrate_CapsRateControlToVideoOnlySourceBitrate()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 6_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoCq.Should().Be(20);
        spec.VideoMaxrateKbps.Should().Be(5000);
        spec.VideoBufferSizeKbps.Should().Be(10000);
    }

    [Fact]
    public void BuildDecision_WhenFilmProfileHasManualRateOverrides_DoesNotApplySourceBitrateCap()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default",
                maxrate: 6.5m,
                bufsize: 13.0m)));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 6_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoMaxrateKbps.Should().Be(6500);
        spec.VideoBufferSizeKbps.Should().Be(13000);
    }

    [Fact]
    public void BuildDecision_WhenMultAccurateAutosampleIsRequested_UsesFixedBucketDefaultsWithoutSampleProvider()
    {
        var sut = CreateSut(
            new ToH264GpuRequest(
                videoSettings: new VideoSettingsRequest(
                    contentProfile: "mult",
                    qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            bitrate: 10_000_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeFalse();
        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(4600);
        spec.VideoBufferSizeKbps.Should().Be(9200);
    }

    [Fact]
    public void BuildDecision_WhenMultProfileSourceBitrateIsLowerThanBucketMaxrate_CapsRateControlToVideoOnlySourceBitrate()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "mult",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(2000);
        spec.VideoBufferSizeKbps.Should().Be(4000);
    }

    [Fact]
    public void BuildDecision_WhenMultProfileHasManualRateOverrides_DoesNotApplySourceBitrateCap()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "mult",
                qualityProfile: "default",
                maxrate: 6.5m,
                bufsize: 13.0m)));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoMaxrateKbps.Should().Be(6500);
        spec.VideoBufferSizeKbps.Should().Be(13000);
    }

    [Fact]
    public void BuildDecision_WhenAnimeAccurateAutosampleIsRequested_UsesFixedBucketDefaultsWithoutSampleProvider()
    {
        var sut = CreateSut(
            new ToH264GpuRequest(
                videoSettings: new VideoSettingsRequest(
                    contentProfile: "anime",
                    qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            bitrate: 10_000_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyVideo.Should().BeFalse();
        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(3400);
        spec.VideoBufferSizeKbps.Should().Be(6800);
    }

    [Fact]
    public void BuildDecision_WhenAnimeProfileSourceBitrateIsLowerThanBucketMaxrate_CapsRateControlToVideoOnlySourceBitrate()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "anime",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoCq.Should().Be(21);
        spec.VideoMaxrateKbps.Should().Be(2000);
        spec.VideoBufferSizeKbps.Should().Be(4000);
    }

    [Fact]
    public void BuildDecision_WhenAnimeProfileHasManualRateOverrides_DoesNotApplySourceBitrateCap()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(
                contentProfile: "anime",
                qualityProfile: "default",
                maxrate: 6.5m,
                bufsize: 13.0m)));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["aac", "aac"],
            bitrate: 3_000_000,
            primaryAudioBitrate: 500_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.VideoMaxrateKbps.Should().Be(6500);
        spec.VideoBufferSizeKbps.Should().Be(13000);
    }

    [Fact]
    public void BuildDecision_WhenDownscale424UsesProfileOnlyRequest_UsesFixedBucketDefaults()
    {
        var sut = CreateSut(new ToH264GpuRequest(
            downscale: new DownscaleRequest(424),
            videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            height: 1080);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        GetRequiredEncodeVideo(actual).Downscale!.TargetHeight.Should().Be(424);
        spec.VideoCq.Should().Be(24);
        spec.VideoMaxrateKbps.Should().Be(2900);
        spec.VideoBufferSizeKbps.Should().Be(5800);
    }

    [Fact]
    public void FormatInfo_WhenEncodeDecisionIsNeeded_DoesNotInvokeSampleReductionProvider()
    {
        var sut = CreateSut(
            new ToH264GpuRequest(
                downscale: new DownscaleRequest(576),
                videoSettings: new VideoSettingsRequest(contentProfile: "film", qualityProfile: "default")));
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            height: 1080,
            bitrate: 10_000_000);

        var actual = sut.FormatInfo(video);

        actual.Should().Contain("encode h264");
        actual.Should().Contain("downscale 576p");
    }

    [Fact]
    public void BuildDecision_WhenSourceAudioBitrateIsInsideCorridor_UsesSourceAudioBitrate()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            primaryAudioBitrate: 192_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.AudioBitrateKbps.Should().Be(192);
    }

    [Fact]
    public void BuildDecision_WhenSourceAudioBitrateIsAboveCorridor_ClampsAudioBitrate()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            primaryAudioBitrate: 384_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.AudioBitrateKbps.Should().Be(320);
    }

    [Fact]
    public void BuildDecision_WhenSourceAudioBitrateIsBelowCorridor_RaisesAudioBitrateToMinimum()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["ac3"],
            primaryAudioBitrate: 32_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        spec.AudioBitrateKbps.Should().Be(48);
    }

    [Fact]
    public void BuildDecision_WhenPrimaryAudioCodecIsAmrNb_UsesMonoResampleAudioOptions()
    {
        var sut = CreateSut();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "av1",
            audioCodecs: ["amr_nb"],
            primaryAudioBitrate: 12_000);

        var actual = sut.BuildDecision(video);
        var spec = actual;

        actual.CopyAudio.Should().BeFalse();
        spec.AudioSampleRate.Should().Be(48000);
        spec.AudioChannels.Should().Be(1);
        spec.AudioFilter.Should().Be("aresample=48000:async=1:first_pts=0");
    }

    [Fact]
    public void BuildExecution_WhenCopyMp4NeedsMuxRewrite_BuildsNonEmptyExecution()
    {
        // Arrange
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var decision = CreateSut().BuildDecision(video);

        // Act
        var actual = tool.BuildExecution(video, decision);

        // Assert
        actual.IsEmpty.Should().BeFalse();
        actual.Commands[0].Should().Contain("-movflags +faststart");
        actual.Commands[0].Should().Contain("-map 0:a:0? -c:a copy");
    }

    [Fact]
    public void BuildExecution_WhenVideoCanBeCopiedButAudioNeedsEncode_CopiesVideoAndEncodesAudio()
    {
        // Arrange
        var tool = CreateFfmpegTool();
        var video = CreateVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            formatName: "matroska,webm",
            videoCodec: "h264",
            audioCodecs: ["ac3"]);
        var decision = CreateSut().BuildDecision(video);

        // Act
        var actual = tool.BuildExecution(video, decision);

        // Assert
        actual.Commands[0].Should().Contain("-map 0:v:0 -c:v copy");
        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-movflags +faststart");
        actual.Commands[0].Should().NotContain("-c:v h264_nvenc");
    }

    [Fact]
    public void BuildExecution_WhenEncodeIsRequired_UsesNvencPresetAndCqMode()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["ac3"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut().BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:v h264_nvenc");
        actual.Commands[0].Should().Contain("-preset p6");
        actual.Commands[0].Should().Contain("-rc vbr_hq -cq");
    }

    [Fact]
    public void BuildExecution_WhenDownscaleIsRequested_UsesScaleCudaAndHardwareDecode()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", filePath: @"C:\video\input.mp4", height: 1080);
        var decision = CreateSut(downscaleTarget: 576).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_output_format cuda");
        actual.Commands[0].Should().Contain("scale_cuda=-2:576:interp_algo=bilinear:format=nv12");
    }

    [Fact]
    public void BuildExecution_WhenDenoiseIsRequestedWithoutDownscale_UsesVideoFilterAndPixelFormat()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "av1", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut(denoise: true).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-vf \"hqdn3d=1.2:1.2:6:6\"");
        actual.Commands[0].Should().Contain("-pix_fmt yuv420p");
    }

    [Fact]
    public void BuildExecution_WhenSyncAudioIsRequested_EncodesAudioWithResample()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mkv", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mkv");
        var decision = CreateSut(synchronizeAudio: true).BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands[0].Should().Contain("-c:a aac");
        actual.Commands[0].Should().Contain("-af \"aresample=async=1:first_pts=0\"");
    }

    [Fact]
    public void BuildExecution_WhenOutputReplacesSource_AddsDeleteAndRenameSteps()
    {
        var tool = CreateFfmpegTool();
        var video = CreateVideo(container: "mp4", videoCodec: "h264", audioCodecs: ["aac"], filePath: @"C:\video\input.mp4");
        var decision = CreateSut().BuildDecision(video);

        var actual = tool.BuildExecution(video, decision);

        actual.Commands.Should().HaveCount(3);
        actual.Commands[1].Should().Be("del \"C:\\video\\input.mp4\"");
        actual.Commands[2].Should().Be("ren \"C:\\video\\input_temp.mp4\" \"input.mp4\"");
    }

    [Fact]
    public void BuildExecution_WhenContainerIsUnsupported_CanHandleReturnsFalse()
    {
        var tool = CreateFfmpegTool();
        var decision = new ToH264GpuDecision(
            targetContainer: "avi",
            videoIntent: new CopyVideoIntent(),
            audioIntent: new CopyAudioIntent(),
            keepSource: false,
            outputPath: @"C:\video\input.avi",
            mux: new ToH264GpuDecision.MuxExecution(optimizeForFastStart: true, mapPrimaryAudioOnly: true));

        var actual = tool.CanHandle(decision);

        actual.Should().BeFalse();
    }

    private static ToH264GpuScenario CreateSut(
        bool keepSource = false,
        int? downscaleTarget = null,
        bool keepFramesPerSecond = false,
        VideoSettingsRequest? videoSettings = null,
        string? nvencPreset = null,
        bool denoise = false,
        bool synchronizeAudio = false,
        bool outputMkv = false)
    {
        return CreateSut(
            new ToH264GpuRequest(
                keepSource: keepSource,
                downscale: downscaleTarget.HasValue ? new DownscaleRequest(downscaleTarget.Value) : null,
                keepFramesPerSecond: keepFramesPerSecond,
                videoSettings: videoSettings,
                nvencPreset: nvencPreset,
                denoise: denoise,
                synchronizeAudio: synchronizeAudio,
                outputMkv: outputMkv));
    }

    private static ToH264GpuScenario CreateSut(ToH264GpuRequest? request)
    {
        return request is null
            ? new ToH264GpuScenario()
            : new ToH264GpuScenario(request);
    }

    private static ToH264GpuFfmpegTool CreateFfmpegTool()
    {
        return new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance);
    }

    private static EncodeVideoIntent GetRequiredEncodeVideo(ToH264GpuDecision decision)
    {
        return decision.Video.Should().BeOfType<EncodeVideoIntent>().Subject;
    }

    private static SourceVideo CreateVideo(
        string filePath = @"C:\video\input.mp4",
        string container = "mp4",
        string videoCodec = "h264",
        IReadOnlyList<string>? audioCodecs = null,
        int width = 1920,
        int height = 1080,
        double framesPerSecond = 29.97,
        TimeSpan? duration = null,
        long? bitrate = 5_000_000,
        string? formatName = null,
        double? rawFramesPerSecond = null,
        double? averageFramesPerSecond = null,
        long? primaryAudioBitrate = 128_000,
        int? primaryAudioSampleRate = null,
        int? primaryAudioChannels = null)
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
            formatName: formatName,
            rawFramesPerSecond: rawFramesPerSecond,
            averageFramesPerSecond: averageFramesPerSecond,
            primaryAudioBitrate: primaryAudioBitrate,
            primaryAudioSampleRate: primaryAudioSampleRate,
            primaryAudioChannels: primaryAudioChannels);
    }
}
