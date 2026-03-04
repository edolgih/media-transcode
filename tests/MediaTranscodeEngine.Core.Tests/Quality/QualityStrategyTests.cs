using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Tests.Quality;

public class QualityStrategyTests
{
    [Fact]
    public void Resolve_WhenQualityHigh_AppliesHighProfileRange()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new QualitySelectionContext(
            ContentProfile: "anime",
            QualityProfile: "high"));

        actual.Cq.Should().Be(22);
        actual.Maxrate.Should().Be(3.3);
        actual.Bufsize.Should().Be(6.5);
    }

    [Fact]
    public void Resolve_WhenManualOverridesProvided_HonorsOverrides()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new QualitySelectionContext(
            ContentProfile: "anime",
            QualityProfile: "default",
            Maxrate: 2.6,
            Bufsize: 5.4));

        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.6);
        actual.Bufsize.Should().Be(5.4);
    }

    private static IQualityStrategy CreateSut()
    {
        return new ProfileBackedQualityStrategy(
            profileRepository: new LegacyPolicyConfigProfileRepository(new StaticProfileRepository()),
            policy: new ProfilePolicy());
    }
}
