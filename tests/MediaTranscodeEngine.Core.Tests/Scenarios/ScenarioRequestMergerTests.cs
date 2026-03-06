using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Core.Tests.Scenarios;

public class TranscodeScenarioCatalogApplyTests
{
    [Fact]
    public void Apply_WhenScenarioProvided_UsesScenarioValues()
    {
        var catalog = new TranscodeScenarioCatalog(
        [
            new TranscodeScenario(
                name: "custom",
                targetContainer: RequestContracts.General.Mp4Container,
                encoderBackend: RequestContracts.General.CpuEncoderBackend,
                targetVideoCodec: RequestContracts.General.H264VideoCodec,
                qualityProfile: "high")
        ]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom");

        var actual = catalog.Apply(request);

        actual.TargetContainer.Should().Be(RequestContracts.General.Mp4Container);
        actual.EncoderBackend.Should().Be(RequestContracts.General.CpuEncoderBackend);
        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
        actual.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void Apply_WhenExplicitOverridesScenario_UsesExplicitValues()
    {
        var catalog = new TranscodeScenarioCatalog(
        [
            new TranscodeScenario(
                name: "custom",
                targetContainer: RequestContracts.General.Mp4Container,
                encoderBackend: RequestContracts.General.CpuEncoderBackend,
                qualityProfile: "high")
        ]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom",
            TargetContainer: RequestContracts.General.MkvContainer,
            EncoderBackend: RequestContracts.General.GpuEncoderBackend,
            QualityProfile: "default");
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetContainer),
            nameof(RawTranscodeRequest.EncoderBackend),
            nameof(RawTranscodeRequest.QualityProfile)
        };

        var actual = catalog.Apply(request, explicitFields);

        actual.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        actual.EncoderBackend.Should().Be(RequestContracts.General.GpuEncoderBackend);
        actual.QualityProfile.Should().Be("default");
    }

    [Fact]
    public void Apply_WhenTargetVideoCodecExplicit_ExplicitWinsOverScenario()
    {
        var catalog = new TranscodeScenarioCatalog(
        [
            new TranscodeScenario(
                name: "custom",
                targetVideoCodec: RequestContracts.General.H264VideoCodec)
        ]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom",
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetVideoCodec)
        };

        var actual = catalog.Apply(request, explicitFields);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    [Fact]
    public void Apply_WhenScenarioUsesH265Codec_UsesScenarioCodecValue()
    {
        var catalog = new TranscodeScenarioCatalog(
        [
            new TranscodeScenario(
                name: "custom",
                targetVideoCodec: RequestContracts.General.H265VideoCodec)
        ]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom");

        var actual = catalog.Apply(request);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H265VideoCodec);
    }

    [Fact]
    public void Apply_WhenNoScenario_ReturnsOriginalRequest()
    {
        var catalog = new TranscodeScenarioCatalog([]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4");

        var actual = catalog.Apply(request);

        actual.Should().Be(request);
    }

    [Fact]
    public void Apply_WhenToMkvGpuAndContainerMp4AndCodecNotExplicit_UsesH264Codec()
    {
        var catalog = new TranscodeScenarioCatalog([TranscodeScenario.CreateToMkvGpu()]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "tomkvgpu",
            TargetContainer: RequestContracts.General.Mp4Container);

        var actual = catalog.Apply(request);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.H264VideoCodec);
    }

    [Fact]
    public void Apply_WhenToMkvGpuAndCodecExplicit_KeepsExplicitCodec()
    {
        var catalog = new TranscodeScenarioCatalog([TranscodeScenario.CreateToMkvGpu()]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "tomkvgpu",
            TargetContainer: RequestContracts.General.Mp4Container,
            TargetVideoCodec: RequestContracts.General.CopyVideoCodec);
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetVideoCodec)
        };

        var actual = catalog.Apply(request, explicitFields);

        actual.TargetVideoCodec.Should().Be(RequestContracts.General.CopyVideoCodec);
    }

    [Fact]
    public void Apply_WhenScenarioUnknown_ThrowsArgumentException()
    {
        var catalog = new TranscodeScenarioCatalog([]);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "missing");

        var act = () => catalog.Apply(request);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown scenario: missing*");
    }
}
