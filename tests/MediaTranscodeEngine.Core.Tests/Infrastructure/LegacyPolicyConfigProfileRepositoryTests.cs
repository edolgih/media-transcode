using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class LegacyPolicyConfigProfileRepositoryTests
{
    [Fact]
    public void GetDefaultProfile_WhenCalled_ReturnsProfile()
    {
        var sut = new LegacyPolicyConfigProfileRepository(new StaticProfileRepository());

        var actual = sut.GetDefaultProfile();

        actual.ContentProfiles.Should().ContainKey("film");
    }

    [Fact]
    public void GetTargetProfile_WhenSupportedTarget_ReturnsProfile()
    {
        var sut = new LegacyPolicyConfigProfileRepository(new StaticProfileRepository());

        var actual = sut.GetTargetProfile(576);

        actual.Should().NotBeNull();
        actual!.IsSupported.Should().BeTrue();
        actual.Profile.Should().NotBeNull();
    }

    [Fact]
    public void GetTargetProfile_WhenUnsupportedTarget_ReturnsReason()
    {
        var sut = new LegacyPolicyConfigProfileRepository(new StaticProfileRepository());

        var actual = sut.GetTargetProfile(720);

        actual.Should().NotBeNull();
        actual!.IsSupported.Should().BeFalse();
        actual.Profile.Should().BeNull();
        actual.UnsupportedReason.Should().Contain("not implemented");
    }
}
