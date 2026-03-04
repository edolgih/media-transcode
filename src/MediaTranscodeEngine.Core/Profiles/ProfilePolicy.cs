using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Sampling;

namespace MediaTranscodeEngine.Core.Profiles;

public sealed class ProfilePolicy
{
    public QualitySettings ResolveBaseSettings(
        TranscodeProfileDefinition profile,
        QualitySelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        if (!profile.ContentProfiles.TryGetValue(context.ContentProfile, out var contentProfile))
        {
            throw new ArgumentException($"Unsupported ContentProfile: {context.ContentProfile}", nameof(context));
        }

        if (!contentProfile.Defaults.TryGetValue(context.QualityProfile, out var defaults))
        {
            throw new ArgumentException(
                $"Unsupported QualityProfile for '{context.ContentProfile}': {context.QualityProfile}",
                nameof(context));
        }

        if (!contentProfile.Limits.TryGetValue(context.QualityProfile, out var limits))
        {
            throw new ArgumentException(
                $"Missing limits for '{context.ContentProfile}/{context.QualityProfile}'",
                nameof(context));
        }

        var resolvedCq = context.Cq ?? defaults.Cq;
        var resolvedMaxrate = ResolveMaxrate(
            defaults,
            limits,
            profile.RateModel,
            resolvedCq,
            context.Cq.HasValue,
            context.Maxrate);
        var resolvedBufsize = ResolveBufsize(
            defaults,
            profile.RateModel,
            context.Bufsize,
            context.Maxrate,
            context.Cq,
            resolvedMaxrate);
        var resolvedAlgo = string.IsNullOrWhiteSpace(context.DownscaleAlgo)
            ? contentProfile.AlgoDefault
            : context.DownscaleAlgo;

        return new QualitySettings(
            Cq: resolvedCq,
            Maxrate: resolvedMaxrate,
            Bufsize: resolvedBufsize,
            DownscaleAlgo: resolvedAlgo);
    }

    public SourceBucketDefinition? ResolveSourceBucket(
        TranscodeProfileDefinition profile,
        double? sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var buckets = profile.SourceBuckets;
        if (buckets is null || buckets.Count == 0)
        {
            return null;
        }

        SourceBucketDefinition? defaultBucket = null;
        foreach (var bucket in buckets)
        {
            if (bucket.IsDefault && defaultBucket is null)
            {
                defaultBucket = bucket;
            }

            if (bucket.Match is null)
            {
                continue;
            }

            if (IsSourceHeightMatched(bucket.Match, sourceHeight))
            {
                return bucket;
            }
        }

        return defaultBucket;
    }

    public string? GetSourceBucketMatrixValidationError(
        TranscodeProfileDefinition profile,
        SourceBucketDefinition bucket,
        string contentProfile,
        string qualityProfile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualityProfile);

        var hasContentRanges = bucket.ContentQualityRanges is not null;
        var hasQualityRanges = bucket.QualityRanges is not null;
        if (!hasContentRanges && !hasQualityRanges)
        {
            return "missing ContentQualityRanges/QualityRanges";
        }

        var hasContentQualityRange =
            bucket.ContentQualityRanges is not null &&
            bucket.ContentQualityRanges.TryGetValue(contentProfile, out var qualityRanges) &&
            qualityRanges.ContainsKey(qualityProfile);

        var hasQualityRange =
            bucket.QualityRanges is not null &&
            bucket.QualityRanges.ContainsKey(qualityProfile);

        if (!hasContentQualityRange && !hasQualityRange)
        {
            return $"missing corridor '{contentProfile}/{qualityProfile}'";
        }

        return null;
    }

    public ReductionRangeDefinition? ResolveQualityRange(
        TranscodeProfileDefinition profile,
        string contentProfile,
        string qualityProfile,
        double? sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(qualityProfile);

        var bucket = ResolveSourceBucket(profile, sourceHeight);

        if (bucket?.ContentQualityRanges is not null &&
            bucket.ContentQualityRanges.TryGetValue(contentProfile, out var bucketContentRanges) &&
            bucketContentRanges.TryGetValue(qualityProfile, out var bucketContentRange))
        {
            return bucketContentRange;
        }

        if (bucket?.QualityRanges is not null &&
            bucket.QualityRanges.TryGetValue(qualityProfile, out var bucketQualityRange))
        {
            return bucketQualityRange;
        }

        if (profile.ContentQualityRanges is not null &&
            profile.ContentQualityRanges.TryGetValue(contentProfile, out var contentRanges) &&
            contentRanges.TryGetValue(qualityProfile, out var contentRange))
        {
            return contentRange;
        }

        if (profile.QualityRanges is not null &&
            profile.QualityRanges.TryGetValue(qualityProfile, out var qualityRange))
        {
            return qualityRange;
        }

        return null;
    }

    public QualitySettings ResolveAutoSampleSettings(
        TranscodeProfileDefinition profile,
        AutoSamplingContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);

        var qualityRange = ResolveQualityRange(
            profile,
            context.ContentProfile,
            context.QualityProfile,
            context.SourceHeight);
        if (qualityRange is null)
        {
            return context.BaseSettings;
        }

        if (!profile.ContentProfiles.TryGetValue(context.ContentProfile, out var content) ||
            !content.Limits.TryGetValue(context.QualityProfile, out var limits))
        {
            return context.BaseSettings;
        }

        var autoSampling = profile.AutoSampling ?? new AutoSamplingDefinition();
        var maxIterations = Math.Max(autoSampling.MaxIterations, 1);
        var hybridIterations = Math.Max(autoSampling.HybridAccurateIterations, 1);
        var bounds = BuildBounds(qualityRange);
        var mode = context.Mode.ToLowerInvariant();

        if (mode == AutoSamplingMode.Fast)
        {
            return RunLoop(
                maxIterations,
                profile.RateModel,
                limits,
                context.BaseSettings,
                bounds,
                context.FastReductionProvider).Result;
        }

        if (mode == AutoSamplingMode.Hybrid)
        {
            var fast = RunLoop(
                maxIterations,
                profile.RateModel,
                limits,
                context.BaseSettings,
                bounds,
                context.FastReductionProvider,
                captureInBounds: true);

            if (fast.InBounds)
            {
                return fast.Result;
            }

            return RunLoop(
                Math.Min(maxIterations, hybridIterations),
                profile.RateModel,
                limits,
                fast.Result,
                bounds,
                context.AccurateReductionProvider).Result;
        }

        return RunLoop(
            maxIterations,
            profile.RateModel,
            limits,
            context.BaseSettings,
            bounds,
            context.AccurateReductionProvider).Result;
    }

    private static double ResolveMaxrate(
        ProfileDefaultsModel defaults,
        ProfileLimitsModel limits,
        ProfileRateModel rateModel,
        int resolvedCq,
        bool hasCq,
        double? maxrateOverride)
    {
        if (maxrateOverride.HasValue)
        {
            return maxrateOverride.Value;
        }

        if (!hasCq)
        {
            return defaults.Maxrate;
        }

        var delta = defaults.Cq - resolvedCq;
        var modeled = defaults.Maxrate + delta * rateModel.CqStepToMaxrateStep;
        return Clamp(modeled, limits.MaxrateMin, limits.MaxrateMax);
    }

    private static double ResolveBufsize(
        ProfileDefaultsModel defaults,
        ProfileRateModel rateModel,
        double? bufsizeOverride,
        double? maxrateOverride,
        int? cqOverride,
        double resolvedMaxrate)
    {
        if (bufsizeOverride.HasValue)
        {
            return bufsizeOverride.Value;
        }

        if (maxrateOverride.HasValue || cqOverride.HasValue)
        {
            return resolvedMaxrate * rateModel.BufsizeMultiplier;
        }

        return defaults.Bufsize;
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool IsSourceHeightMatched(
        SourceBucketMatchDefinition match,
        double? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return false;
        }

        var height = sourceHeight.Value;
        if (match.MinHeightInclusive.HasValue && height < match.MinHeightInclusive.Value)
        {
            return false;
        }

        if (match.MinHeightExclusive.HasValue && height <= match.MinHeightExclusive.Value)
        {
            return false;
        }

        if (match.MaxHeightInclusive.HasValue && height > match.MaxHeightInclusive.Value)
        {
            return false;
        }

        if (match.MaxHeightExclusive.HasValue && height >= match.MaxHeightExclusive.Value)
        {
            return false;
        }

        return true;
    }

    private static ReductionBounds BuildBounds(ReductionRangeDefinition range)
    {
        var lower = range.MinInclusive ?? range.MinExclusive;
        var lowerInclusive = range.MinInclusive.HasValue;
        var upper = range.MaxInclusive ?? range.MaxExclusive;
        var upperInclusive = range.MaxInclusive.HasValue;

        return new ReductionBounds(
            Lower: lower,
            LowerInclusive: lowerInclusive,
            Upper: upper,
            UpperInclusive: upperInclusive);
    }

    private static bool IsReductionInBounds(double value, ReductionBounds bounds)
    {
        if (bounds.Lower.HasValue)
        {
            if (bounds.LowerInclusive)
            {
                if (value < bounds.Lower.Value)
                {
                    return false;
                }
            }
            else if (value <= bounds.Lower.Value)
            {
                return false;
            }
        }

        if (bounds.Upper.HasValue)
        {
            if (bounds.UpperInclusive)
            {
                if (value > bounds.Upper.Value)
                {
                    return false;
                }
            }
            else if (value >= bounds.Upper.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsReductionBelowBounds(double value, ReductionBounds bounds)
    {
        if (!bounds.Lower.HasValue)
        {
            return false;
        }

        if (bounds.LowerInclusive)
        {
            return value < bounds.Lower.Value;
        }

        return value <= bounds.Lower.Value;
    }

    private static LoopResult RunLoop(
        int maxIterations,
        ProfileRateModel rateModel,
        ProfileLimitsModel limits,
        QualitySettings start,
        ReductionBounds bounds,
        Func<int, double, double, double?> reductionProvider,
        bool captureInBounds = false)
    {
        var cq = start.Cq;
        var maxrate = start.Maxrate;
        var inBounds = false;

        for (var i = 0; i < maxIterations; i++)
        {
            var bufsize = maxrate * rateModel.BufsizeMultiplier;
            var reduction = reductionProvider(cq, maxrate, bufsize);
            if (!reduction.HasValue)
            {
                break;
            }

            var reductionValue = reduction.Value;
            if (IsReductionInBounds(reductionValue, bounds))
            {
                inBounds = true;
                break;
            }

            var prevCq = cq;
            var prevMaxrate = maxrate;

            if (IsReductionBelowBounds(reductionValue, bounds))
            {
                if (cq < limits.CqMax)
                {
                    cq++;
                }

                maxrate = Math.Max(maxrate - rateModel.CqStepToMaxrateStep, limits.MaxrateMin);
            }
            else
            {
                if (cq > limits.CqMin)
                {
                    cq--;
                }

                maxrate = Math.Min(maxrate + rateModel.CqStepToMaxrateStep, limits.MaxrateMax);
            }

            if (prevCq == cq && Math.Abs(prevMaxrate - maxrate) < 0.000001)
            {
                break;
            }
        }

        return new LoopResult(
            Result: start with
            {
                Cq = cq,
                Maxrate = maxrate,
                Bufsize = maxrate * rateModel.BufsizeMultiplier
            },
            InBounds: captureInBounds && inBounds);
    }

    private sealed record ReductionBounds(
        double? Lower,
        bool LowerInclusive,
        double? Upper,
        bool UpperInclusive);

    private sealed record LoopResult(
        QualitySettings Result,
        bool InBounds);
}
