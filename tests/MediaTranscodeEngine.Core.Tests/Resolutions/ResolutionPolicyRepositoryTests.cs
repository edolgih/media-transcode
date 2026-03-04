using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Resolutions;

namespace MediaTranscodeEngine.Core.Tests.Resolutions;

public class ResolutionPolicyRepositoryTests
{
    [Fact]
    public void Resolve_WhenHd720To576_ReturnsExpectedPolicy()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(SourceHeight: 720, TargetHeight: 576),
            ContentProfile: "anime",
            QualityProfile: "default"));

        actual.IsSupported.Should().BeTrue();
        actual.ApplyDownscale.Should().BeTrue();
        actual.SourceBucketName.Should().Be("hd_720");
        actual.Settings.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WhenFhd1080To576_ReturnsExpectedPolicy()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(SourceHeight: 1080, TargetHeight: 576),
            ContentProfile: "anime",
            QualityProfile: "default"));

        actual.IsSupported.Should().BeTrue();
        actual.ApplyDownscale.Should().BeTrue();
        actual.SourceBucketName.Should().Be("fhd_1080");
        actual.Settings.Should().NotBeNull();
    }

    [Fact]
    public void Resolve_WhenTarget720_ReturnsUnsupported()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(SourceHeight: 1080, TargetHeight: 720),
            ContentProfile: "anime",
            QualityProfile: "default"));

        actual.IsSupported.Should().BeFalse();
        actual.Error.Should().Contain("not implemented");
    }

    [Fact]
    public void Resolve_WhenTargetNotConfigured_ReturnsUnsupported()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(SourceHeight: 1080, TargetHeight: 1080),
            ContentProfile: "anime",
            QualityProfile: "default"));

        actual.IsSupported.Should().BeFalse();
        actual.Error.Should().Contain("not supported");
    }

    private static IResolutionPolicyRepository CreateSut()
    {
        return new ProfileBackedResolutionPolicyRepository(
            profileRepository: new LegacyPolicyConfigProfileRepository(new StaticProfileRepository()),
            policy: new ProfilePolicy());
    }
}
