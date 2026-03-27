using FluentAssertions;
using Transcode.Core.VideoSettings;
using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Runtime.Tests.VideoSettings;

/*
Это тесты каталога и profile-данных VideoSettings.
Они фиксируют минимальные инварианты quality-first модели без autosample.
*/
/// <summary>
/// Verifies profile catalog behavior and bounds overrides in the simplified video-settings model.
/// </summary>
public sealed class VideoSettingsProfilesTests
{
    [Theory]
    [InlineData(1080, 1080)]
    [InlineData(960, 1080)]
    [InlineData(740, 720)]
    [InlineData(600, 576)]
    [InlineData(470, 480)]
    [InlineData(424, 424)]
    public void ResolveOutputProfile_WhenHeightIsMapped_ReturnsExpectedProfile(int outputHeight, int expectedTargetHeight)
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.ResolveOutputProfile(outputHeight);

        actual.TargetHeight.Should().Be(expectedTargetHeight);
    }

    [Fact]
    public void GetSupportedDownscaleTargetHeights_WhenDefaultsAreRequested_ReturnsConfiguredTargets()
    {
        var sut = VideoSettingsProfiles.Default;

        var actual = sut.GetSupportedDownscaleTargetHeights();

        actual.Should().Equal(720, 576, 480, 424);
    }

    [Fact]
    public void SupportsDownscaleTargetHeight_WhenTargetIsEncodeOnly_ReturnsFalse()
    {
        var sut = VideoSettingsProfiles.Default;

        sut.SupportsDownscaleTargetHeight(1080).Should().BeFalse();
    }

    [Fact]
    public void ResolveDefaults_WhenSourceBucketHasBoundsOverride_AppliesOverride()
    {
        var profile = new VideoSettingsProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(0.4m, 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("film", "default", CqMin: 16, MaxrateMax: 6.0m)
                    ])
            ],
            defaults: CreateDefaults());
        var selection = new EffectiveVideoSettingsSelection("film", "default");

        var actual = profile.ResolveDefaults(sourceHeight: 1080, selection);

        actual.CqMin.Should().Be(16);
        actual.MaxrateMax.Should().Be(6.0m);
    }

    [Fact]
    public void ResolveDefaults_WhenSourceBucketDoesNotMatch_UsesProfileDefaults()
    {
        var profile = new VideoSettingsProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(0.4m, 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "fhd",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("film", "default", CqMin: 16, MaxrateMax: 6.0m)
                    ])
            ],
            defaults: CreateDefaults());
        var selection = new EffectiveVideoSettingsSelection("film", "default");

        var actual = profile.ResolveDefaults(sourceHeight: 900, selection);

        actual.CqMin.Should().Be(18);
        actual.MaxrateMax.Should().Be(8.0m);
    }

    [Fact]
    public void ResolveSourceBucketIssue_WhenSourceHeightIsNotCovered_ReturnsMissingBucketError()
    {
        var profile = VideoSettingsProfiles.Create(
            new VideoSettingsProfile(
                targetHeight: 576,
                defaultContentProfile: "film",
                defaultQualityProfile: "default",
                rateModel: new VideoSettingsRateModel(0.4m, 2.0m),
                sourceBuckets:
                [
                    new SourceHeightBucket("fhd", MinHeight: 1000, MaxHeight: 1300)
                ],
                defaults: CreateDefaults()));

        var actual = profile.GetRequiredProfile(576).ResolveSourceBucketIssue(900);

        actual.Should().Contain("source bucket missing");
    }

    [Fact]
    public void SourceHeightBucket_WhenBoundsOverrideExists_ResolvesByProfilePair()
    {
        var bucket = new SourceHeightBucket(
            Name: "fhd",
            MinHeight: 1000,
            MaxHeight: 1300,
            BoundsOverrides:
            [
                new VideoSettingsBoundsOverride("film", "default", CqMin: 18, MaxrateMax: 8.0m)
            ]);

        var actual = bucket.ResolveBoundsOverride("film", "default");

        actual.Should().NotBeNull();
        actual!.CqMin.Should().Be(18);
        actual.MaxrateMax.Should().Be(8.0m);
    }

    private static VideoSettingsDefaults[] CreateDefaults()
    {
        return
        [
            new VideoSettingsDefaults("anime", "high", 22, 3.3m, 6.5m, "bilinear", 19, 24, 2.4m, 4.2m),
            new VideoSettingsDefaults("anime", "default", 23, 2.4m, 4.8m, "bilinear", 20, 26, 2.0m, 3.0m),
            new VideoSettingsDefaults("anime", "low", 29, 2.1m, 4.1m, "bilinear", 24, 35, 1.0m, 3.2m),
            new VideoSettingsDefaults("mult", "high", 22, 3.7m, 7.4m, "bilinear", 18, 31, 2.3m, 5.0m),
            new VideoSettingsDefaults("mult", "default", 24, 3.3m, 6.6m, "bilinear", 20, 33, 1.9m, 4.2m),
            new VideoSettingsDefaults("mult", "low", 28, 2.5m, 5.0m, "bilinear", 22, 36, 1.4m, 3.2m),
            new VideoSettingsDefaults("film", "high", 21, 4.3m, 8.6m, "bilinear", 16, 33, 2.0m, 8.0m),
            new VideoSettingsDefaults("film", "default", 23, 3.8m, 7.6m, "bilinear", 18, 35, 1.6m, 8.0m),
            new VideoSettingsDefaults("film", "low", 27, 2.8m, 5.6m, "bilinear", 20, 38, 1.2m, 4.0m)
        ];
    }
}
