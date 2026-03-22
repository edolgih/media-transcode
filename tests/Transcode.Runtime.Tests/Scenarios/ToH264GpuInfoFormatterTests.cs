using FluentAssertions;
using Transcode.Core.Failures;
using Transcode.Core.MediaIntent;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Runtime.Tests.Scenarios;

/*
Это тесты formatter-а info-режима toh264gpu.
Они проверяют текстовую сводку решения и единообразные failure-маркеры для CLI.
*/
/// <summary>
/// Verifies summary formatting and failure markers produced by the ToH264Gpu info formatter.
/// </summary>
public sealed class ToH264GpuInfoFormatterTests
{
    [Fact]
    public void Format_WhenPlanIsRemuxOnly_ReturnsSourceFactsAndRemuxMarker()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mp4", container: "mp4", videoCodec: "h264", audioCodecs: ["aac"]);
        var decision = CreateDecision(copyVideo: true, copyAudio: true, outputPath: video.FilePath, targetContainer: "mp4");

        var actual = sut.Format(video, decision);

        actual.Should().Be("input.mp4: 1920x1080 fps 29.97 [remux-only]");
    }

    [Fact]
    public void Format_WhenEncodeAndContainerRewriteAndDownscaleAreRequired_ReturnsExpectedMarkers()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", container: "mkv", videoCodec: "av1", audioCodecs: ["ac3"]);
        var decision = CreateDecision(
            copyVideo: false,
            copyAudio: false,
            outputPath: @"C:\video\input.mp4",
            targetContainer: "mp4",
            downscaleHeight: 576);

        var actual = sut.Format(video, decision);

        actual.Should().Be("input.mkv: 1920x1080 fps 29.97 [encode h264] [container .mkv->mp4] [downscale 576p]");
    }

    [Fact]
    public void Format_WhenSyncAudioIsRequested_ReturnsSourceFactsAndSyncAudioMarker()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mp4", container: "mp4", videoCodec: "h264", audioCodecs: ["aac"]);
        var decision = CreateDecision(copyVideo: true, copyAudio: false, outputPath: video.FilePath, synchronizeAudio: true, targetContainer: "mp4");

        var actual = sut.Format(video, decision);

        actual.Should().Be("input.mp4: 1920x1080 fps 29.97 [copy video] [sync audio]");
    }

    [Fact]
    public void Format_WhenVideoIsCopiedButAudioIsEncoded_ReturnsCopyVideoAndAudioMarkers()
    {
        var sut = CreateSut();
        var video = CreateVideo(filePath: @"C:\video\input.mkv", container: "mkv", videoCodec: "h264", audioCodecs: ["ac3"]);
        var decision = CreateDecision(copyVideo: true, copyAudio: false, outputPath: @"C:\video\input.mp4", targetContainer: "mp4");

        var actual = sut.Format(video, decision);

        actual.Should().Be("input.mkv: 1920x1080 fps 29.97 [copy video] [container .mkv->mp4] [audio aac]");
    }

    [Fact]
    public void FormatFailure_WhenNoVideoStreamErrorOccurs_ReturnsNoVideoStreamMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(@"C:\nested\folder\input.mp4", RuntimeFailures.NoVideoStream());

        actual.Should().Be("input.mp4: [no video stream]");
    }

    [Fact]
    public void FormatFailure_WhenProbeErrorIsGeneric_ReturnsFfprobeFailedMarker()
    {
        var sut = CreateSut();

        var actual = sut.FormatFailure(@"C:\nested\folder\input.mp4", RuntimeFailures.ProbeEmptyOutput());

        actual.Should().Be("input.mp4: [ffprobe failed]");
    }

    private static ToH264GpuInfoFormatter CreateSut()
    {
        return new ToH264GpuInfoFormatter();
    }

    private static SourceVideo CreateVideo(
        string filePath,
        string container,
        string videoCodec,
        IReadOnlyList<string> audioCodecs)
    {
        return new SourceVideo(
            filePath: filePath,
            container: container,
            videoCodec: videoCodec,
            audioCodecs: audioCodecs,
            width: 1920,
            height: 1080,
            framesPerSecond: 29.97,
            duration: TimeSpan.FromMinutes(10));
    }

    private static ToH264GpuDecision CreateDecision(
        bool copyVideo,
        bool copyAudio,
        string outputPath,
        string targetContainer,
        bool synchronizeAudio = false,
        int? downscaleHeight = null)
    {
        VideoIntent videoIntent = copyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                Downscale: downscaleHeight.HasValue
                    ? new DownscaleRequest(downscaleHeight.Value, "bilinear")
                    : null);
        AudioIntent audioIntent = copyAudio
            ? new CopyAudioIntent()
            : synchronizeAudio
                ? new SynchronizeAudioIntent()
                : new EncodeAudioIntent();

        return new ToH264GpuDecision(
            targetContainer: targetContainer,
            videoIntent: videoIntent,
            audioIntent: audioIntent,
            keepSource: false,
            outputPath: outputPath,
            mux: new ToH264GpuDecision.MuxExecution());
    }
}
