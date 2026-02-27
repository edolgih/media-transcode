using FluentAssertions;
using MediaTranscodeEngine.Cli;

namespace MediaTranscodeEngine.Core.Tests.Cli;

public class ToMkvCliArgumentParserTests
{
    [Fact]
    public void Parse_WhenForceVideoEncodeSpecified_SetsForceVideoEncodeTrue()
    {
        var sut = CreateSut();

        var actual = sut.Parse(["--force-video-encode"]);

        actual.IsValid.Should().BeTrue();
        actual.Options!.ForceVideoEncode.Should().BeTrue();
    }

    [Theory]
    [InlineData("--downscale", "576", true)]
    [InlineData("--downscale", "720", true)]
    [InlineData("--downscale", "1080", false)]
    public void Parse_WhenDownscaleProvided_ReturnsExpectedValidity(string option, string value, bool expectedValid)
    {
        var sut = CreateSut();

        var actual = sut.Parse([option, value]);

        actual.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Parse_WhenInputAndNumericOverridesProvided_ParsesExpectedValues()
    {
        var sut = CreateSut();

        var actual = sut.Parse([
            "--input", "C:\\video\\a.mkv",
            "--content-profile", "anime",
            "--quality-profile", "high",
            "--cq", "20",
            "--maxrate", "2.6",
            "--bufsize", "5.2",
            "--nvenc-preset", "p7",
            "--auto-sample-mode", "fast"
        ]);

        actual.IsValid.Should().BeTrue();
        actual.Options!.Inputs.Should().ContainSingle().Which.Should().Be("C:\\video\\a.mkv");
        actual.Options.ContentProfile.Should().Be("anime");
        actual.Options.QualityProfile.Should().Be("high");
        actual.Options.Cq.Should().Be(20);
        actual.Options.Maxrate.Should().Be(2.6);
        actual.Options.Bufsize.Should().Be(5.2);
        actual.Options.NvencPreset.Should().Be("p7");
        actual.Options.AutoSampleMode.Should().Be("fast");
    }

    [Fact]
    public void Parse_WhenUnknownOptionProvided_ReturnsInvalidResult()
    {
        var sut = CreateSut();

        var actual = sut.Parse(["--unknown-option"]);

        actual.IsValid.Should().BeFalse();
        actual.ErrorMessage.Should().Contain("Unknown option");
    }

    private static ToMkvCliArgumentParser CreateSut()
    {
        return new ToMkvCliArgumentParser();
    }
}
