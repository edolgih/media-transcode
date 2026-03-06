using FluentAssertions;
using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Cli.Tests.Parsing;

public class CliScenarioPresetTests
{
    [Fact]
    public void Parse_WhenScenarioTomkvgpuProvided_AppliesScenario()
    {
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "tomkvgpu"],
            out var parsed,
            out var errorText);
        var catalog = new TranscodeScenarioCatalog([TranscodeScenario.CreateToMkvGpu()]);

        var merged = catalog.Apply(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        merged.Scenario.Should().Be("tomkvgpu");
        merged.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        merged.EncoderBackend.Should().Be(RequestContracts.General.GpuEncoderBackend);
        merged.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    [Fact]
    public void Parse_WhenScenarioAndExplicitCq_ExplicitWins()
    {
        var catalog = new TranscodeScenarioCatalog(
        [
            new TranscodeScenario(
                name: "custom",
                cq: 24)
        ]);
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "custom", "--cq", "19"],
            out var parsed,
            out var errorText);

        var merged = catalog.Apply(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        merged.Cq.Should().Be(19);
    }

    [Fact]
    public void Parse_WhenScenarioUnknown_ReturnsValidationError()
    {
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "missing"],
            out var parsed,
            out var errorText);
        var catalog = new TranscodeScenarioCatalog([]);

        var act = () => catalog.Apply(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown scenario: missing*");
    }

    [Fact]
    public void Parse_WhenScenarioToMkvGpuAndContainerMp4_UsesH264CodecByScenarioRule()
    {
        var ok = CliArgumentParser.TryParse(
            ["--input", "C:\\video\\movie.mp4", "--scenario", "tomkvgpu", "--container", "mp4"],
            out var parsed,
            out var errorText);
        var catalog = new TranscodeScenarioCatalog([TranscodeScenario.CreateToMkvGpu()]);

        var merged = catalog.Apply(parsed.RequestTemplate, parsed.ExplicitTemplateFields);

        ok.Should().BeTrue();
        errorText.Should().BeNull();
        merged.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }
}
