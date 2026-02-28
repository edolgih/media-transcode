using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class TranscodePolicyAutoSamplingTests
{
    [Fact]
    public void ResolveAutoSampleSettings_WhenSamplingModeFast_UsesFastProvider()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();
        var accurateCalls = 0;
        var fastCalls = 0;

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "fast",
            accurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            fastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 45.0;
            });

        fastCalls.Should().Be(1);
        accurateCalls.Should().Be(0);
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenSamplingModeAccurate_UsesAccurateProvider()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();
        var accurateCalls = 0;
        var fastCalls = 0;

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            fastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 45.0;
            });

        accurateCalls.Should().Be(1);
        fastCalls.Should().Be(0);
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenSamplingModeHybridAndFastInBounds_SkipsAccurateProvider()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();
        var accurateCalls = 0;
        var fastCalls = 0;

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "hybrid",
            accurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            fastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 45.0;
            });

        fastCalls.Should().Be(1);
        accurateCalls.Should().Be(0);
        actual.Cq.Should().Be(23);
        actual.Maxrate.Should().Be(2.4);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenSamplingModeHybridAndFastOutOfBounds_RunsAccurateProvider()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();
        var accurateCalls = 0;
        var fastCalls = 0;

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "hybrid",
            accurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            fastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 30.0;
            });

        fastCalls.Should().BeGreaterThan(0);
        accurateCalls.Should().Be(1);
        actual.Cq.Should().Be(26);
        actual.Maxrate.Should().Be(2.0);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionBelowCorridor_IncreasesCqAndDecreasesMaxrate()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: CreateSequentialProvider(30.0, 45.0),
            fastReductionProvider: (_, _, _) => 45.0);

        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0);
        actual.Bufsize.Should().Be(4.0);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionAboveCorridor_DecreasesCqAndIncreasesMaxrate()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: CreateSequentialProvider(70.0, 45.0),
            fastReductionProvider: (_, _, _) => 45.0);

        actual.Cq.Should().Be(22);
        actual.Maxrate.Should().Be(2.8);
        actual.Bufsize.Should().Be(5.6);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionProviderReturnsNull_ReturnsBaseSettings()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) => null,
            fastReductionProvider: (_, _, _) => null);

        actual.Should().Be(baseSettings);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionEqualsLowerInclusive_KeepsBaseSettings()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: CreateSequentialProvider(40.0, 45.0),
            fastReductionProvider: CreateSequentialProvider(40.0, 45.0));

        actual.Should().Be(baseSettings);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionEqualsUpperInclusive_KeepsBaseSettings()
    {
        var sut = CreateSut();
        var config = CreateConfig();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) => 50.0,
            fastReductionProvider: (_, _, _) => 50.0);

        actual.Should().Be(baseSettings);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenReductionEqualsLowerExclusive_AdjustsSettings()
    {
        var sut = CreateSut();
        var config = CreateConfigWithExclusiveLowerBound();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: CreateSequentialProvider(40.0, 45.0),
            fastReductionProvider: CreateSequentialProvider(40.0, 45.0));

        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0);
        actual.Bufsize.Should().Be(4.0);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenMaxIterationsReached_ReturnsLastResolvedSettings()
    {
        var sut = CreateSut();
        var config = CreateConfig(maxIterations: 2);
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) => 30.0,
            fastReductionProvider: (_, _, _) => 30.0);

        actual.Cq.Should().Be(25);
        actual.Maxrate.Should().Be(2.0);
        actual.Bufsize.Should().Be(4.0);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenLimitsReachedAndStillOutOfCorridor_StopsWithoutFurtherChanges()
    {
        var sut = CreateSut();
        var config = CreateConfigWithTightLimits();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 1080,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) => 30.0,
            fastReductionProvider: (_, _, _) => 30.0);

        actual.Cq.Should().Be(24);
        actual.Maxrate.Should().Be(2.0);
        actual.Bufsize.Should().Be(4.0);
    }

    [Fact]
    public void ResolveAutoSampleSettings_WhenMatchedBucketRangeDiffers_UsesMatchedBucketBounds()
    {
        var sut = CreateSut();
        var config = CreateConfigWithHdBucketRange();
        var baseSettings = CreateBaseSettings();

        var actual = sut.ResolveAutoSampleSettings(
            config,
            contentProfile: "anime",
            qualityProfile: "default",
            baseSettings: baseSettings,
            sourceHeight: 720,
            autoSampleMode: "accurate",
            accurateReductionProvider: (_, _, _) => 20.0,
            fastReductionProvider: (_, _, _) => 20.0);

        actual.Should().Be(baseSettings);
    }

    private static Func<int, double, double, double?> CreateSequentialProvider(params double?[] values)
    {
        var index = 0;
        return (_, _, _) =>
        {
            var value = values[Math.Min(index, values.Length - 1)];
            index++;
            return value;
        };
    }

    private static TranscodePolicy CreateSut()
    {
        return new TranscodePolicy();
    }

    private static TranscodePolicyResult CreateBaseSettings()
    {
        return new TranscodePolicyResult(
            Cq: 23,
            Maxrate: 2.4,
            Bufsize: 4.8,
            DownscaleAlgo: "bilinear");
    }

    private static TranscodePolicyConfig CreateConfig(int maxIterations = 8)
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 20, CqMax: 26, MaxrateMin: 2.0, MaxrateMax: 3.0)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: defaults,
                Limits: limits)
        };

        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 50.0)
            }
        };

        var sourceBuckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            SourceBuckets: sourceBuckets,
            AutoSampling: new AutoSamplingSettings(MaxIterations: maxIterations, HybridAccurateIterations: 2));
    }

    private static TranscodePolicyConfig CreateConfigWithExclusiveLowerBound()
    {
        var config = CreateConfig();
        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinExclusive: 40.0, MaxInclusive: 50.0)
            }
        };

        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges)
        };

        return config with { SourceBuckets = buckets };
    }

    private static TranscodePolicyConfig CreateConfigWithTightLimits()
    {
        var defaults = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileDefaults(Cq: 23, Maxrate: 2.4, Bufsize: 4.8)
        };

        var limits = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = new ProfileLimits(CqMin: 23, CqMax: 24, MaxrateMin: 2.0, MaxrateMax: 2.4)
        };

        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: defaults,
                Limits: limits)
        };

        var bucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 50.0)
            }
        };

        var sourceBuckets = new List<SourceBucketSettings>
        {
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: bucketRanges)
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0),
            SourceBuckets: sourceBuckets,
            AutoSampling: new AutoSamplingSettings(MaxIterations: 8, HybridAccurateIterations: 2));
    }

    private static TranscodePolicyConfig CreateConfigWithHdBucketRange()
    {
        var config = CreateConfig();
        var hdBucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 10.0, MaxInclusive: 25.0)
            }
        };

        var fhdBucketRanges = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase)
        {
            ["anime"] = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ReductionRange(MinInclusive: 40.0, MaxInclusive: 50.0)
            }
        };

        var buckets = new List<SourceBucketSettings>
        {
            new(
                Name: "hd_720",
                Match: new SourceBucketMatch(MinHeightInclusive: 650, MaxHeightInclusive: 899),
                ContentQualityRanges: hdBucketRanges),
            new(
                Name: "fhd_1080",
                Match: new SourceBucketMatch(MinHeightInclusive: 1000, MaxHeightInclusive: 1300),
                ContentQualityRanges: fhdBucketRanges)
        };

        return config with { SourceBuckets = buckets };
    }
}
