using FluentAssertions;
using MediaTranscodeEngine.Core.Scenarios;

namespace MediaTranscodeEngine.Core.Tests.Scenarios;

public class TranscodeScenarioCatalogTests
{
    [Fact]
    public void Get_WhenScenarioUnknown_ReturnsNull()
    {
        var sut = new TranscodeScenarioCatalog([]);

        var actual = sut.Get("missing");

        actual.Should().BeNull();
    }
}
