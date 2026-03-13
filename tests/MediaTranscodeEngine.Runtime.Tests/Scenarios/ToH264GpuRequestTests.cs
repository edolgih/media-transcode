using FluentAssertions;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

namespace MediaTranscodeEngine.Runtime.Tests.Scenarios;

public sealed class ToH264GpuRequestTests
{
    [Fact]
    public void TryParseArgs_WithValidOptions_ReturnsRequest()
    {
        var actual = ToH264GpuRequest.TryParseArgs(
            [
                "--keep-source",
                "--downscale", "576",
                "--keep-fps",
                "--content-profile", "film",
                "--quality-profile", "default",
                "--autosample-mode", "fast",
                "--downscale-algo", "lanczos",
                "--cq", "21",
                "--maxrate", "4.2",
                "--bufsize", "8.4",
                "--nvenc-preset", "p6",
                "--denoise",
                "--sync-audio",
                "--mkv"
            ],
            out var request,
            out var errorText);

        actual.Should().BeTrue();
        errorText.Should().BeNull();
        request.KeepSource.Should().BeTrue();
        request.Downscale.Should().NotBeNull();
        request.Downscale!.TargetHeight.Should().Be(576);
        request.Downscale.Algorithm.Should().Be("lanczos");
        request.KeepFramesPerSecond.Should().BeTrue();
        request.VideoSettings.Should().NotBeNull();
        request.VideoSettings!.ContentProfile.Should().Be("film");
        request.VideoSettings.QualityProfile.Should().Be("default");
        request.VideoSettings.AutoSampleMode.Should().Be("fast");
        request.VideoSettings.Cq.Should().Be(21);
        request.VideoSettings.Maxrate.Should().Be(4.2m);
        request.VideoSettings.Bufsize.Should().Be(8.4m);
        request.NvencPreset.Should().Be("p6");
        request.Denoise.Should().BeTrue();
        request.SynchronizeAudio.Should().BeTrue();
        request.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void TryParseArgs_WhenDownscaleHeightIsUnsupported_ReturnsFalse()
    {
        var actual = ToH264GpuRequest.TryParseArgs(
            [
                "--downscale", "360"
            ],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().Be("--downscale must be one of: 720, 576, 480, 424.");
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
        var actual = ToH264GpuRequest.TryParseArgs(
            [
                optionName, optionValue
            ],
            out _,
            out var errorText);

        actual.Should().BeFalse();
        errorText.Should().NotBeNullOrWhiteSpace();
    }
}
