using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Core.Tests.Scenarios;

public class ScenarioRequestMergerTests
{
    [Fact]
    public void Merge_WhenPresetProvided_UsesPresetValues()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetContainer: RequestContracts.General.Mp4Container,
                ComputeMode: RequestContracts.General.CpuComputeMode,
                PreferH264: true,
                QualityProfile: "high")
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom");

        var actual = sut.Merge(request);

        actual.TargetContainer.Should().Be(RequestContracts.General.Mp4Container);
        actual.ComputeMode.Should().Be(RequestContracts.General.CpuComputeMode);
        actual.PreferH264.Should().BeTrue();
        actual.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void Merge_WhenExplicitOverridesPreset_UsesExplicitValues()
    {
        var repository = new InMemoryScenarioPresetRepository(
        [
            new ScenarioPreset(
                Name: "custom",
                TargetContainer: RequestContracts.General.Mp4Container,
                ComputeMode: RequestContracts.General.CpuComputeMode,
                QualityProfile: "high")
        ]);
        var sut = new ScenarioRequestMerger(repository);
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "custom",
            TargetContainer: RequestContracts.General.MkvContainer,
            ComputeMode: RequestContracts.General.GpuComputeMode,
            QualityProfile: "default");
        var explicitFields = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(RawTranscodeRequest.TargetContainer),
            nameof(RawTranscodeRequest.ComputeMode),
            nameof(RawTranscodeRequest.QualityProfile)
        };

        var actual = sut.Merge(request, explicitFields);

        actual.TargetContainer.Should().Be(RequestContracts.General.MkvContainer);
        actual.ComputeMode.Should().Be(RequestContracts.General.GpuComputeMode);
        actual.QualityProfile.Should().Be("default");
    }

    [Fact]
    public void Merge_WhenNoPreset_UsesSystemDefaults()
    {
        var sut = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Merge(request);

        actual.Should().Be(request);
    }

    [Fact]
    public void Merge_WhenScenarioUnknown_ThrowsArgumentException()
    {
        var sut = new ScenarioRequestMerger(new InMemoryScenarioPresetRepository());
        var request = new RawTranscodeRequest(
            InputPath: "C:\\video\\movie.mp4",
            Scenario: "missing");

        var act = () => sut.Merge(request);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown scenario: missing*");
    }
}
