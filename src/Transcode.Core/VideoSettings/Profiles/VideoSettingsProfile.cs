using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это один профиль video settings для конкретной целевой высоты.
Он хранит defaults и source-height buckets.
*/
/// <summary>
/// Represents one typed video-settings profile keyed by target height.
/// </summary>
internal sealed class VideoSettingsProfile
{
    private readonly IReadOnlyDictionary<string, VideoSettingsDefaults> _defaultsByProfile;

    public VideoSettingsProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        VideoSettingsRateModel rateModel,
        IReadOnlyList<SourceHeightBucket> sourceBuckets,
        IReadOnlyList<VideoSettingsDefaults> defaults,
        bool supportsDownscale = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);
        ArgumentNullException.ThrowIfNull(rateModel);
        ArgumentNullException.ThrowIfNull(sourceBuckets);
        ArgumentNullException.ThrowIfNull(defaults);

        TargetHeight = targetHeight;
        SupportsDownscale = supportsDownscale;
        DefaultContentProfile = defaultContentProfile.Trim().ToLowerInvariant();
        DefaultQualityProfile = defaultQualityProfile.Trim().ToLowerInvariant();
        RateModel = rateModel;
        SourceBuckets = sourceBuckets;
        Defaults = defaults;
        _defaultsByProfile = defaults.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
    }

    public int TargetHeight { get; }

    public bool SupportsDownscale { get; }

    public string DefaultContentProfile { get; }

    public string DefaultQualityProfile { get; }

    public VideoSettingsRateModel RateModel { get; }

    public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

    public IReadOnlyList<VideoSettingsDefaults> Defaults { get; }

    public string? ResolveSourceBucket(int? sourceHeight)
    {
        return ResolveSourceBucketDefinition(sourceHeight)?.Name;
    }

    public string? ResolveSourceBucketIssue(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            var fallbackBucket = ResolveSourceBucketDefinition(sourceHeight);
            if (fallbackBucket is null)
            {
                return $"{TargetHeight} source bucket missing: height is unknown; add SourceBuckets";
            }

            return null;
        }

        var bucket = ResolveSourceBucketDefinition(sourceHeight);
        if (bucket is null)
        {
            return $"{TargetHeight} source bucket missing: height {sourceHeight.Value}; add SourceBuckets";
        }

        return null;
    }

    public VideoSettingsDefaults ResolveDefaults(EffectiveVideoSettingsSelection selection)
    {
        return ResolveDefaults(sourceHeight: null, selection);
    }

    public VideoSettingsDefaults ResolveDefaults(int? sourceHeight, EffectiveVideoSettingsSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        var key = BuildDefaultsKey(selection.ContentProfile, selection.QualityProfile);
        if (_defaultsByProfile.TryGetValue(key, out var defaults))
        {
            var boundsOverride = ResolveSourceBucketDefinition(sourceHeight)?.ResolveBoundsOverride(selection.ContentProfile, selection.QualityProfile);
            return boundsOverride is null
                ? defaults
                : defaults with
                {
                    CqMin = boundsOverride.CqMin ?? defaults.CqMin,
                    CqMax = boundsOverride.CqMax ?? defaults.CqMax,
                    MaxrateMin = boundsOverride.MaxrateMin ?? defaults.MaxrateMin,
                    MaxrateMax = boundsOverride.MaxrateMax ?? defaults.MaxrateMax
                };
        }

        throw new InvalidOperationException(
            $"Video settings defaults are not configured for content '{selection.ContentProfile}' and quality '{selection.QualityProfile}'.");
    }

    private SourceHeightBucket? ResolveSourceBucketDefinition(int? sourceHeight)
    {
        if (sourceHeight.HasValue)
        {
            var matched = SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value));
            if (matched is not null)
            {
                return matched;
            }
        }

        return SourceBuckets.FirstOrDefault(static bucket => bucket.IsDefault);
    }

    private static string BuildDefaultsKey(string contentProfile, string qualityProfile)
    {
        return $"{contentProfile.Trim().ToLowerInvariant()}::{qualityProfile.Trim().ToLowerInvariant()}";
    }
}
