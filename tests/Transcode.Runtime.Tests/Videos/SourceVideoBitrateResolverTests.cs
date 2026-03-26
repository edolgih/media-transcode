using FluentAssertions;
using Transcode.Core.Videos;

namespace Transcode.Runtime.Tests.Videos;

public sealed class SourceVideoBitrateResolverTests
{
    [Fact]
    public void ResolveVideoBitrateHint_WhenPrimaryVideoBitrateIsKnown_ReturnsPrimaryVideoBitrate()
    {
        var video = CreateVideo(
            bitrate: 9_000_000,
            primaryVideoBitrate: 7_200_000,
            primaryAudioBitrate: 192_000);

        var actual = SourceVideoBitrateResolver.ResolveVideoBitrateHint(video);

        actual.Should().Be(7_200_000);
    }

    [Fact]
    public void ResolveVideoBitrateHint_WhenOnlyTotalBitrateAndAudioBitrateAreKnown_SubtractsAudioFromTotal()
    {
        var video = CreateVideo(
            bitrate: 8_000_000,
            primaryAudioBitrate: 256_000,
            audioCodecs: ["aac", "aac"]);

        var actual = SourceVideoBitrateResolver.ResolveVideoBitrateHint(video);

        actual.Should().Be(7_488_000);
    }

    [Fact]
    public void ResolveVideoBitrateFromTotal_WhenAudioEstimateIsHigherThanTotal_FallsBackToTotal()
    {
        var video = CreateVideo(
            bitrate: 500_000,
            primaryAudioBitrate: 320_000,
            audioCodecs: ["aac", "aac"]);

        var actual = SourceVideoBitrateResolver.ResolveVideoBitrateFromTotal(500_000, video);

        actual.Should().Be(500_000);
    }

    private static SourceVideo CreateVideo(
        long? bitrate,
        long? primaryVideoBitrate = null,
        long? primaryAudioBitrate = null,
        IReadOnlyList<string>? audioCodecs = null)
    {
        return new SourceVideo(
            filePath: @"C:\video\input.mkv",
            container: "mkv",
            videoCodec: "h264",
            audioCodecs: audioCodecs ?? ["aac"],
            width: 1920,
            height: 1080,
            framesPerSecond: 29.97,
            duration: TimeSpan.FromMinutes(10),
            bitrate: bitrate,
            primaryAudioBitrate: primaryAudioBitrate,
            primaryVideoBitrate: primaryVideoBitrate);
    }
}
