using FluentAssertions;
using Transcode.Core.VideoSettings;

namespace Transcode.Runtime.Tests.VideoSettings;

/*
Это тесты resolver-а profile-driven video settings.
Они покрывают выбор профиля, вычисление default-настроек и сочетание их с explicit overrides.
*/
/// <summary>
/// Verifies profile resolution and override application in the video-settings resolver.
/// </summary>
public sealed class VideoSettingsResolverTests
{
    [Fact]
    public void ResolveForEncode_WhenRequestIsNull_UsesResolvedOutputProfile()
    {
        var sut = CreateSut();

        var actual = sut.ResolveForEncode(
            request: null,
            outputHeight: 650,
            sourceHeight: 1080);

        actual.Profile.TargetHeight.Should().Be(720);
        actual.BaseSettings.ContentProfile.Should().Be("film");
        actual.BaseSettings.QualityProfile.Should().Be("default");
    }

    [Fact]
    public void ResolveForEncode_WhenRequestContainsProfiles_UsesProvidedProfiles()
    {
        var sut = CreateSut();
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        var actual = sut.ResolveForEncode(
            request: request,
            outputHeight: 650,
            sourceHeight: 1080);

        actual.BaseSettings.ContentProfile.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Should().Be("high");
    }

    [Fact]
    public void ResolveForDownscale_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        var action = () => sut.ResolveForDownscale(
            request: null!,
            videoSettings: null,
            sourceHeight: 1080);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveForDownscale_WhenRequestContainsProfiles_UsesTargetProfileAndOverrides()
    {
        var sut = CreateSut();
        var downscale = new DownscaleRequest(576);
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        var actual = sut.ResolveForDownscale(
            request: downscale,
            videoSettings: request,
            sourceHeight: 1080);

        actual.Profile.TargetHeight.Should().Be(576);
        actual.BaseSettings.ContentProfile.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Should().Be("high");
    }

    private static VideoSettingsResolver CreateSut(VideoSettingsProfiles? profiles = null)
    {
        return new VideoSettingsResolver(profiles ?? VideoSettingsProfiles.Default);
    }

    private static VideoSettingsRequest CreateRequest(
        string? contentProfile = null,
        string? qualityProfile = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        return new VideoSettingsRequest(
            contentProfile: contentProfile,
            qualityProfile: qualityProfile,
            cq: cq,
            maxrate: maxrate,
            bufsize: bufsize);
    }
}
