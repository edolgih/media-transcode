using FluentAssertions;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

namespace MediaTranscodeEngine.Runtime.Tests.Scenarios;

public sealed class ToMkvGpuRequestTests
{
    [Fact]
    public void TryParseArgs_WithValidOptions_ReturnsRequest()
    {
        var actual = ToMkvGpuRequest.TryParseArgs(
            [
                "--keep-source",
                "--overlay-bg",
                "--sync-audio",
                "--downscale", "576",
                "--content-profile", "Film",
                "--quality-profile", "Default",
                "--autosample-mode", "Fast",
                "--downscale-algo", "Bicubic",
                "--max-fps", "40",
                "--cq", "24",
                "--maxrate", "3.7",
                "--bufsize", "7.4",
                "--nvenc-preset", "P6"
            ],
            out var request,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        request.KeepSource.Should().BeTrue();
        request.OverlayBackground.Should().BeTrue();
        request.SynchronizeAudio.Should().BeTrue();
        request.Downscale.Should().NotBeNull();
        request.Downscale!.TargetHeight.Should().Be(576);
        request.Downscale.Algorithm.Should().Be("bicubic");
        request.VideoSettings.Should().NotBeNull();
        request.VideoSettings!.ContentProfile.Should().Be("film");
        request.VideoSettings.QualityProfile.Should().Be("default");
        request.VideoSettings.AutoSampleMode.Should().Be("fast");
        request.VideoSettings.Cq.Should().Be(24);
        request.VideoSettings.Maxrate.Should().Be(3.7m);
        request.VideoSettings.Bufsize.Should().Be(7.4m);
        request.NvencPreset.Should().Be("p6");
        request.MaxFramesPerSecond.Should().Be(40);
    }

    [Theory]
    [InlineData("--content-profile", "other")]
    [InlineData("--quality-profile", "other")]
    [InlineData("--autosample-mode", "other")]
    [InlineData("--nvenc-preset", "p8")]
    public void TryParseArgs_WhenSharedOptionValueIsUnsupported_ReturnsFalse(
        string optionName,
        string optionValue)
    {
        var actual = ToMkvGpuRequest.TryParseArgs(
            [
                optionName, optionValue
            ],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().NotBeNullOrWhiteSpace();
    }
}
