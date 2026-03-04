using FluentAssertions;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Core.Tests.Scenarios;

public class ScenarioPresetRepositoryTests
{
    [Fact]
    public void Get_WhenScenarioUnknown_ReturnsNull()
    {
        var sut = new InMemoryScenarioPresetRepository();

        var actual = sut.Get("missing");

        actual.Should().BeNull();
    }
}
