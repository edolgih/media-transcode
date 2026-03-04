using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Policy;
using MediaTranscodeEngine.Core.Profiles;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class LegacyPolicyConfigProfileRepository : IProfileDefinitionRepository
{
    private readonly IProfileRepository _profileRepository;
    private TranscodeProfileDefinition? _cachedDefaultProfile;
    private IReadOnlyDictionary<int, DownscaleTargetProfile>? _cachedTargetProfiles;

    public LegacyPolicyConfigProfileRepository(IProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
    }

    public TranscodeProfileDefinition GetDefaultProfile()
    {
        EnsureCache();
        return _cachedDefaultProfile!;
    }

    public DownscaleTargetProfile? GetTargetProfile(int targetHeight)
    {
        EnsureCache();
        return _cachedTargetProfiles!.TryGetValue(targetHeight, out var profile)
            ? profile
            : null;
    }

    public IReadOnlyCollection<DownscaleTargetProfile> GetTargetProfiles()
    {
        EnsureCache();
        return _cachedTargetProfiles!.Values.ToArray();
    }

    private void EnsureCache()
    {
        if (_cachedDefaultProfile is not null && _cachedTargetProfiles is not null)
        {
            return;
        }

        var config = _profileRepository.Get576Config();
        _cachedDefaultProfile = new TranscodeProfileDefinition(
            ContentProfiles: MapContentProfiles(config.ContentProfiles),
            RateModel: new ProfileRateModel(
                CqStepToMaxrateStep: config.RateModel.CqStepToMaxrateStep,
                BufsizeMultiplier: config.RateModel.BufsizeMultiplier),
            QualityRanges: MapQualityRanges(config.QualityRanges),
            ContentQualityRanges: MapContentQualityRanges(config.ContentQualityRanges),
            SourceBuckets: MapSourceBuckets(config.SourceBuckets),
            AutoSampling: MapAutoSampling(config.AutoSampling));
        _cachedTargetProfiles = BuildTargetProfiles(_cachedDefaultProfile, config.DownscaleTargets);
    }

    private static IReadOnlyDictionary<string, ContentProfileDefinition> MapContentProfiles(
        IReadOnlyDictionary<string, ContentProfileSettings> source)
    {
        var result = new Dictionary<string, ContentProfileDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var content in source)
        {
            result[content.Key] = new ContentProfileDefinition(
                AlgoDefault: content.Value.AlgoDefault,
                Defaults: MapDefaults(content.Value.Defaults),
                Limits: MapLimits(content.Value.Limits));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ProfileDefaultsModel> MapDefaults(
        IReadOnlyDictionary<string, ProfileDefaults> source)
    {
        var result = new Dictionary<string, ProfileDefaultsModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = new ProfileDefaultsModel(
                Cq: item.Value.Cq,
                Maxrate: item.Value.Maxrate,
                Bufsize: item.Value.Bufsize);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ProfileLimitsModel> MapLimits(
        IReadOnlyDictionary<string, ProfileLimits> source)
    {
        var result = new Dictionary<string, ProfileLimitsModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = new ProfileLimitsModel(
                CqMin: item.Value.CqMin,
                CqMax: item.Value.CqMax,
                MaxrateMin: item.Value.MaxrateMin,
                MaxrateMax: item.Value.MaxrateMax);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ReductionRangeDefinition>? MapQualityRanges(
        IReadOnlyDictionary<string, ReductionRange>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new Dictionary<string, ReductionRangeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
        {
            result[item.Key] = MapReductionRange(item.Value);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRangeDefinition>>? MapContentQualityRanges(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, ReductionRangeDefinition>>(StringComparer.OrdinalIgnoreCase);
        foreach (var content in source)
        {
            var qualityRanges = new Dictionary<string, ReductionRangeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var range in content.Value)
            {
                qualityRanges[range.Key] = MapReductionRange(range.Value);
            }

            result[content.Key] = qualityRanges;
        }

        return result;
    }

    private static IReadOnlyList<SourceBucketDefinition>? MapSourceBuckets(
        IReadOnlyList<SourceBucketSettings>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new List<SourceBucketDefinition>(source.Count);
        foreach (var bucket in source)
        {
            result.Add(new SourceBucketDefinition(
                Name: bucket.Name,
                Match: MapBucketMatch(bucket.Match),
                IsDefault: bucket.IsDefault,
                ContentQualityRanges: MapContentQualityRanges(bucket.ContentQualityRanges),
                QualityRanges: MapQualityRanges(bucket.QualityRanges)));
        }

        return result;
    }

    private static SourceBucketMatchDefinition? MapBucketMatch(SourceBucketMatch? source)
    {
        if (source is null)
        {
            return null;
        }

        return new SourceBucketMatchDefinition(
            MinHeightInclusive: source.MinHeightInclusive,
            MinHeightExclusive: source.MinHeightExclusive,
            MaxHeightInclusive: source.MaxHeightInclusive,
            MaxHeightExclusive: source.MaxHeightExclusive);
    }

    private static AutoSamplingDefinition? MapAutoSampling(AutoSamplingSettings? source)
    {
        if (source is null)
        {
            return null;
        }

        return new AutoSamplingDefinition(
            EnabledByDefault: source.EnabledByDefault,
            MaxIterations: source.MaxIterations,
            ModeDefault: source.ModeDefault,
            HybridAccurateIterations: source.HybridAccurateIterations);
    }

    private static ReductionRangeDefinition MapReductionRange(ReductionRange range)
    {
        return new ReductionRangeDefinition(
            MinInclusive: range.MinInclusive,
            MinExclusive: range.MinExclusive,
            MaxInclusive: range.MaxInclusive,
            MaxExclusive: range.MaxExclusive);
    }

    private static IReadOnlyDictionary<int, DownscaleTargetProfile> BuildTargetProfiles(
        TranscodeProfileDefinition defaultProfile,
        IReadOnlyDictionary<int, DownscaleTargetSettings>? targetSettings)
    {
        if (targetSettings is null || targetSettings.Count == 0)
        {
            return new Dictionary<int, DownscaleTargetProfile>
            {
                [576] = new DownscaleTargetProfile(
                    TargetHeight: 576,
                    IsSupported: true,
                    Profile: defaultProfile)
            };
        }

        var result = new Dictionary<int, DownscaleTargetProfile>();
        foreach (var pair in targetSettings)
        {
            var targetHeight = pair.Key;
            var settings = pair.Value ?? new DownscaleTargetSettings();
            if (settings.Supported)
            {
                result[targetHeight] = new DownscaleTargetProfile(
                    TargetHeight: targetHeight,
                    IsSupported: true,
                    Profile: defaultProfile);
                continue;
            }

            result[targetHeight] = new DownscaleTargetProfile(
                TargetHeight: targetHeight,
                IsSupported: false,
                UnsupportedReason: string.IsNullOrWhiteSpace(settings.UnsupportedReason)
                    ? $"Downscale {targetHeight} is not supported."
                    : settings.UnsupportedReason);
        }

        return result;
    }
}
