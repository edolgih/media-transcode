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
    public void Resolve_WhenEncodeRequestIsNull_UsesResolvedOutputProfile()
    {
        var sut = CreateSut();

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: 650,
            SourceBitrate: null,
            VideoSettings: null,
            Downscale: null));

        actual.Profile.TargetHeight.Should().Be(720);
        actual.BaseSettings.ContentProfile.Value.Should().Be("film");
        actual.BaseSettings.QualityProfile.Value.Should().Be("default");
    }

    [Fact]
    public void Resolve_WhenEncodeRequestContainsProfiles_UsesProvidedProfiles()
    {
        var sut = CreateSut();
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: 650,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: null));

        actual.BaseSettings.ContentProfile.Value.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Value.Should().Be("high");
    }

    [Fact]
    public void Resolve_WhenContextIsNull_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        var action = () => sut.Resolve(context: null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_WhenDownscaleRequestContainsProfiles_UsesTargetProfileAndOverrides()
    {
        var sut = CreateSut();
        var downscale = new DownscaleRequest(576);
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "high");

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: downscale.TargetHeight,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: downscale));

        actual.Profile.TargetHeight.Should().Be(576);
        actual.BaseSettings.ContentProfile.Value.Should().Be("anime");
        actual.BaseSettings.QualityProfile.Value.Should().Be("high");
    }

    [Fact]
    public void Resolve_WhenEncodeManualCqImprovesDefaultQuality_MovesRatePointAndBoundsTowardsBetterNeighbor()
    {
        var sut = CreateSut();
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "default", cq: 20);

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: 1080,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: null));

        actual.Settings.Cq.Should().Be(20);
        actual.Settings.Maxrate.Should().Be(4.2m);
        actual.Settings.Bufsize.Should().Be(8.4m);
        actual.Settings.MaxrateMin.Should().Be(2.8m);
        actual.Settings.MaxrateMax.Should().Be(5.0m);
    }

    [Fact]
    public void Resolve_WhenDownscaleManualCqImprovesDefaultQuality_MovesRatePointAndBoundsTowardsBetterNeighbor()
    {
        var sut = CreateSut();
        var downscale = new DownscaleRequest(576);
        var request = CreateRequest(contentProfile: "film", qualityProfile: "default", cq: 22);

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: downscale.TargetHeight,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: downscale));

        actual.Settings.Cq.Should().Be(22);
        actual.Settings.Maxrate.Should().Be(4.05m);
        actual.Settings.Bufsize.Should().Be(8.10m);
        actual.Settings.MaxrateMin.Should().Be(1.8m);
        actual.Settings.MaxrateMax.Should().Be(8.0m);
    }

    [Fact]
    public void Resolve_WhenDownscaleManualCqWorsensDefaultQuality_MovesRatePointAndBoundsTowardsWorseNeighbor()
    {
        var sut = CreateSut();
        var downscale = new DownscaleRequest(576);
        var request = CreateRequest(contentProfile: "film", qualityProfile: "default", cq: 24);

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: downscale.TargetHeight,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: downscale));

        actual.Settings.Cq.Should().Be(24);
        actual.Settings.Maxrate.Should().Be(3.55m);
        actual.Settings.Bufsize.Should().Be(7.10m);
        actual.Settings.MaxrateMin.Should().Be(1.5m);
        actual.Settings.MaxrateMax.Should().Be(7.0m);
    }

    [Fact]
    public void Resolve_WhenDownscaleManualCqImprovesAsymmetricAnimeProfile_UsesDirectionalCorridorInsteadOfDefaultClamp()
    {
        var sut = CreateSut();
        var downscale = new DownscaleRequest(424);
        var request = CreateRequest(contentProfile: "anime", qualityProfile: "default", cq: 24);

        var actual = sut.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: 1080,
            OutputHeight: downscale.TargetHeight,
            SourceBitrate: null,
            VideoSettings: request,
            Downscale: downscale));

        actual.Settings.Cq.Should().Be(24);
        actual.Settings.Maxrate.Should().Be(2.1m);
        actual.Settings.Bufsize.Should().Be(4.2m);
        actual.Settings.MaxrateMin.Should().Be(1.6m);
        actual.Settings.MaxrateMax.Should().Be(2.8m);
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
