using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class TranscodePolicyBucketTests
{
    [Fact]
    public void ResolveSourceBucket_WhenHeightMatchesConfiguredBucket_ReturnsMatchedBucket()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 1080);

        actual.Should().NotBeNull();
        actual!.Name.Should().Be("fhd_1080");
    }

    [Fact]
    public void ResolveSourceBucket_WhenHeightDoesNotMatchConfiguredBuckets_ReturnsDefaultBucket()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 900);

        actual.Should().NotBeNull();
        actual!.Name.Should().Be("default_bucket");
    }

    [Fact]
    public void ResolveSourceBucket_WhenNoDefaultAndNoMatches_ReturnsNull()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveSourceBucket(config, sourceHeight: 901);

        actual.Should().BeNull();
    }

    [Theory]
    [InlineData(650, "hd_720")]
    [InlineData(899, "hd_720")]
    [InlineData(900, null)]
    [InlineData(999, null)]
    [InlineData(1000, "fhd_1080")]
    [InlineData(1105, "fhd_1080")]
    [InlineData(1300, "fhd_1080")]
    [InlineData(1301, null)]
    public void ResolveSourceBucket_WhenBoundaryHeightProvided_ReturnsExpectedBucket(
        double sourceHeight,
        string? expectedBucketName)
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveSourceBucket(config, sourceHeight);

        if (expectedBucketName is null)
        {
            actual.Should().BeNull();
            return;
        }

        actual.Should().NotBeNull();
        actual!.Name.Should().Be(expectedBucketName);
    }

    [Fact]
    public void ResolveQualityRange_WhenBucketHasContentSpecificRange_ReturnsBucketRange()
    {
        var sut = CreateSut();
        var config = CreateConfig();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 1080);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(45.0);
        actual.MaxInclusive.Should().Be(60.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenNoBucketMatch_FallsBackToGlobalContentRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 901);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(40.0);
        actual.MaxInclusive.Should().Be(50.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenContentRangeMissing_FallsBackToGlobalQualityRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutGlobalContentRanges();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 901);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(35.0);
        actual.MaxInclusive.Should().Be(52.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenHd720BucketMatched_ReturnsHd720BucketRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithoutDefaultBucket();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: 720);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(32.0);
        actual.MaxInclusive.Should().Be(46.0);
    }

    [Fact]
    public void ResolveQualityRange_WhenSourceHeightMissingAndDefaultBucketHasQualityRange_UsesDefaultBucketRange()
    {
        var sut = CreateSut();
        var config = CreateConfigWithDefaultBucketQualityRange();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            sourceHeight: null);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(20.0);
        actual.MaxInclusive.Should().Be(30.0);
    }

    [Theory]
    [InlineData("anime", 45.0, 60.0)]
    [InlineData("mult", 42.0, 57.0)]
    [InlineData("film", 35.0, 50.0)]
    public void ResolveQualityRange_WhenUsingStaticProfilesForFhdBucket_ReturnsExpectedRange(
        string contentProfile,
        double expectedMin,
        double expectedMax)
    {
        var sut = CreateSut();
        var config = new StaticProfileRepository().Get576Config();

        var actual = sut.ResolveQualityRange(
            config,
            contentProfile: contentProfile,
            qualityProfile: "default",
            sourceHeight: 1080);

        actual.Should().NotBeNull();
        actual!.MinInclusive.Should().Be(expectedMin);
        actual.MaxInclusive.Should().Be(expectedMax);
    }

    [Theory]
    [InlineData(0, "anime", "high", "missing corridor 'anime/high'")]
    [InlineData(1, "anime", "default", null)]
    public void GetSourceBucketMatrixValidationError_WhenCalled_ReturnsExpectedValue(
        int bucketIndex,
        string contentProfile,
        string qualityProfile,
        string? expected)
    {
        var sut = CreateSut();
        var config = CreateConfigForBucketValidation();
        var bucket = config.SourceBuckets![bucketIndex];

        var actual = sut.GetSourceBucketMatrixValidationError(config, bucket, contentProfile, qualityProfile);

        actual.Should().Be(expected);
    }

    private static TranscodePolicy CreateSut()
    {
        return new TranscodePolicy();
    }

    private static TranscodePolicyConfig CreateConfig()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: CreateGlobalContentQualityRanges(),
            SourceBuckets: CreateSourceBuckets(includeDefault: true));
    }

    private static TranscodePolicyConfig CreateConfigWithoutDefaultBucket()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: CreateGlobalContentQualityRanges(),
            SourceBuckets: CreateSourceBuckets(includeDefault: false));
    }

    private static TranscodePolicyConfig CreateConfigWithoutGlobalContentRanges()
    {
        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase),
            SourceBuckets: CreateSourceBuckets(includeDefault: false));
    }

    private static TranscodePolicyConfig CreateConfigWithDefaultBucketQualityRange()
    {
        var sourceBuckets = CreateSourceBuckets(includeDefault: false).ToList();
        sourceBuckets.Add(new SourceBucketSettings(
            Name: "default_bucket",
            IsDefault: true,
            QualityRanges: new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 20.0, MaxInclusive: 30.0)
            }));

        return new TranscodePolicyConfig(
            ContentProfiles: CreateContentProfiles(),
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            QualityRanges: CreateGlobalQualityRanges(),
            ContentQualityRanges: CreateGlobalContentQualityRanges(),
            SourceBuckets: sourceBuckets);
    }

    private static TranscodePolicyConfig CreateConfigForBucketValidation()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileDefaults(Cq: 22, Maxrate: 2.8, Bufsize: 5.6),
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["high"] = new ProfileLimits(CqMin: 20, CqMax: 25, MaxrateMin: 2.0, MaxrateMax: 3.2),
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings("bilinear", defaults, limits)
        };

        var invalidBucket = new SourceBucketSettings(
            Name: "invalid_bucket",
            Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
            ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
            {
                ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["default"] = new ReductionRange(MinInclusive: 45.0, MaxInclusive: 60.0)
                }
            });

        var validBucket = new SourceBucketSettings(
            Name: "valid_bucket",
            Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 899),
            ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
            {
                ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                {
                    ["high"] = new ReductionRange(MinInclusive: 30.0, MaxInclusive: 45.0),
                    ["default"] = new ReductionRange(MinInclusive: 45.0, MaxInclusive: 60.0)
                }
            });

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            SourceBuckets: new[] { invalidBucket, validBucket });
    }

    private static IReadOnlyDictionary<string, ContentProfileSettings> CreateContentProfiles()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        return new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: defaults,
                Limits: limits)
        };
    }

    private static IReadOnlyDictionary<string, ReductionRange> CreateGlobalQualityRanges()
    {
        return new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ReductionRange(MinInclusive: 35.0, MaxInclusive: 52.0)
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>> CreateGlobalContentQualityRanges()
    {
        return new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 50.0)
            }
        };
    }

    private static IReadOnlyList<SourceBucketSettings> CreateSourceBuckets(bool includeDefault)
    {
        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "hd_720",
                Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 899),
                ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new ReductionRange(MinInclusive: 32.0, MaxInclusive: 46.0)
                    }
                }),
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["default"] = new ReductionRange(MinInclusive: 45.0, MaxInclusive: 60.0)
                    }
                })
        };

        if (includeDefault)
        {
            buckets.Add(new SourceBucketSettings(
                Name: "default_bucket",
                IsDefault: true));
        }

        return buckets;
    }
}
