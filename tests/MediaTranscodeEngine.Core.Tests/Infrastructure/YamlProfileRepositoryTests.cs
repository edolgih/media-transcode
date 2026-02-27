using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class YamlProfileRepositoryTests
{
    [Fact]
    public void Get576Config_WhenYamlIsValid_ReturnsExpectedValues()
    {
        var yamlPath = GetTestDataPath("ToMkvGPU.576.Profiles.yaml");
        var sut = new YamlProfileRepository(yamlPath);

        var actual = sut.Get576Config();

        actual.ContentProfiles.ContainsKey("anime").Should().BeTrue();
        actual.ContentProfiles.ContainsKey("mult").Should().BeTrue();
        actual.ContentProfiles.ContainsKey("film").Should().BeTrue();
        actual.ContentProfiles["film"].Defaults["default"].Cq.Should().Be(26);
        actual.ContentProfiles["anime"].Limits["high"].MaxrateMax.Should().Be(4.2);
        actual.SourceBuckets.Should().NotBeNull();
        actual.SourceBuckets!.Count.Should().Be(2);
        actual.SourceBuckets[1].Name.Should().Be("fhd_1080");
    }

    [Fact]
    public void Get576Config_WhenYamlFileDoesNotExist_ThrowsFileNotFoundException()
    {
        var yamlPath = Path.Combine(Path.GetTempPath(), $"profiles-{Guid.NewGuid():N}.yaml");
        var sut = new YamlProfileRepository(yamlPath);

        var action = () => sut.Get576Config();

        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Get576Config_WhenYamlHasInvalidSyntax_ThrowsInvalidOperationException()
    {
        var yamlPath = GetTestDataPath("invalid.syntax.yaml");
        var sut = new YamlProfileRepository(yamlPath);

        var action = () => sut.Get576Config();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*parsing failed*");
    }

    [Fact]
    public void Get576Config_WhenYamlConfigInvalid_ThrowsInvalidOperationException()
    {
        var yamlPath = GetTestDataPath("invalid.config.yaml");
        var sut = new YamlProfileRepository(yamlPath);

        var action = () => sut.Get576Config();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing Limits*");
    }

    [Fact]
    public void Get576Config_WhenCalledTwice_ReturnsCachedInstance()
    {
        var yamlPath = GetTestDataPath("ToMkvGPU.576.Profiles.yaml");
        var sut = new YamlProfileRepository(yamlPath);

        var first = sut.Get576Config();
        var second = sut.Get576Config();

        second.Should().BeSameAs(first);
    }

    private static string GetTestDataPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
    }
}
