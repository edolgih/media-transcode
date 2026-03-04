using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Resolutions;

namespace MediaTranscodeEngine.Core.Tests.Resolutions;

public class ResolutionTransformExecutionTests
{
    [Fact]
    public void Apply_WhenSourceNotAboveTarget_DoesNotDownscale()
    {
        var sut = new ProfileBackedResolutionPolicyRepository(
            profileRepository: new LegacyPolicyConfigProfileRepository(new StaticProfileRepository()),
            policy: new ProfilePolicy());

        var actual = sut.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(SourceHeight: 576, TargetHeight: 576),
            ContentProfile: "anime",
            QualityProfile: "default"));

        actual.IsSupported.Should().BeTrue();
        actual.ApplyDownscale.Should().BeFalse();
        actual.Settings.Should().BeNull();
    }
}
