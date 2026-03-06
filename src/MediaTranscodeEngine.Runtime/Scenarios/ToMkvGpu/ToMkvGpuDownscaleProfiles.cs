namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/// <summary>
/// Provides typed downscale profiles used by the ToMkvGpu workflow.
/// </summary>
internal sealed class ToMkvGpuDownscaleProfiles
{
    private readonly IReadOnlyDictionary<int, ToMkvGpuDownscaleProfile> _profilesByTargetHeight;

    private ToMkvGpuDownscaleProfiles(IReadOnlyDictionary<int, ToMkvGpuDownscaleProfile> profilesByTargetHeight)
    {
        _profilesByTargetHeight = profilesByTargetHeight;
    }

    public static ToMkvGpuDownscaleProfiles Default { get; } = CreateDefault();

    public ToMkvGpuDownscaleProfile GetRequiredProfile(int targetHeight)
    {
        if (_profilesByTargetHeight.TryGetValue(targetHeight, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"ToMkvGpu downscale profile '{targetHeight}' is not configured.");
    }

    private static ToMkvGpuDownscaleProfiles CreateDefault()
    {
        var profile576 = new ToMkvGpuDownscaleProfile(
            targetHeight: 576,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            sourceBuckets:
            [
                new ToMkvGpuSourceBucket("hd_720", MinHeight: 650, MaxHeight: 899),
                new ToMkvGpuSourceBucket("fhd_1080", MinHeight: 1000, MaxHeight: 1300)
            ],
            defaults:
            [
                new ToMkvGpuDownscaleDefaults("anime", "high", Cq: 22, Maxrate: 3.3m, Bufsize: 6.5m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("anime", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.1m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("mult", "high", Cq: 24, Maxrate: 2.7m, Bufsize: 5.3m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("mult", "default", Cq: 26, Maxrate: 2.4m, Bufsize: 4.8m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("mult", "low", Cq: 29, Maxrate: 1.7m, Bufsize: 3.5m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("film", "high", Cq: 24, Maxrate: 3.7m, Bufsize: 7.4m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("film", "default", Cq: 26, Maxrate: 3.4m, Bufsize: 6.9m, DownscaleAlgorithm: "bilinear"),
                new ToMkvGpuDownscaleDefaults("film", "low", Cq: 30, Maxrate: 2.2m, Bufsize: 4.5m, DownscaleAlgorithm: "bilinear")
            ]);

        return new ToMkvGpuDownscaleProfiles(
            new Dictionary<int, ToMkvGpuDownscaleProfile>
            {
                [profile576.TargetHeight] = profile576
            });
    }
}

/// <summary>
/// Represents one typed downscale profile keyed by target height.
/// </summary>
internal sealed class ToMkvGpuDownscaleProfile
{
    private readonly IReadOnlyDictionary<string, ToMkvGpuDownscaleDefaults> _defaultsByProfile;

    public ToMkvGpuDownscaleProfile(
        int targetHeight,
        string defaultContentProfile,
        string defaultQualityProfile,
        IReadOnlyList<ToMkvGpuSourceBucket> sourceBuckets,
        IReadOnlyList<ToMkvGpuDownscaleDefaults> defaults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);

        TargetHeight = targetHeight;
        DefaultContentProfile = defaultContentProfile.Trim().ToLowerInvariant();
        DefaultQualityProfile = defaultQualityProfile.Trim().ToLowerInvariant();
        SourceBuckets = sourceBuckets;
        Defaults = defaults;
        _defaultsByProfile = defaults.ToDictionary(
            static entry => BuildDefaultsKey(entry.ContentProfile, entry.QualityProfile),
            StringComparer.OrdinalIgnoreCase);
    }

    public int TargetHeight { get; }

    public string DefaultContentProfile { get; }

    public string DefaultQualityProfile { get; }

    public IReadOnlyList<ToMkvGpuSourceBucket> SourceBuckets { get; }

    public IReadOnlyList<ToMkvGpuDownscaleDefaults> Defaults { get; }

    public string? ResolveSourceBucket(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return null;
        }

        return SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value))?.Name;
    }

    public ToMkvGpuDownscaleDefaults ResolveDefaults(string? contentProfile, string? qualityProfile)
    {
        var effectiveContentProfile = NormalizeProfileName(contentProfile) ?? DefaultContentProfile;
        var effectiveQualityProfile = NormalizeProfileName(qualityProfile) ?? DefaultQualityProfile;
        var key = BuildDefaultsKey(effectiveContentProfile, effectiveQualityProfile);
        if (_defaultsByProfile.TryGetValue(key, out var defaults))
        {
            return defaults;
        }

        throw new InvalidOperationException(
            $"ToMkvGpu downscale defaults are not configured for content '{effectiveContentProfile}' and quality '{effectiveQualityProfile}'.");
    }

    private static string? NormalizeProfileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static string BuildDefaultsKey(string contentProfile, string qualityProfile)
    {
        return $"{contentProfile.Trim().ToLowerInvariant()}::{qualityProfile.Trim().ToLowerInvariant()}";
    }
}

/// <summary>
/// Represents one default encode preset entry inside a typed downscale profile.
/// </summary>
internal sealed record ToMkvGpuDownscaleDefaults(
    string ContentProfile,
    string QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize,
    string DownscaleAlgorithm)
{
    public string ContentProfile { get; init; } = NormalizeRequiredToken(ContentProfile, nameof(ContentProfile));

    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public int Cq { get; init; } = Cq > 0
        ? Cq
        : throw new ArgumentOutOfRangeException(nameof(Cq), Cq, "CQ must be greater than zero.");

    public decimal Maxrate { get; init; } = Maxrate > 0m
        ? Maxrate
        : throw new ArgumentOutOfRangeException(nameof(Maxrate), Maxrate, "Maxrate must be greater than zero.");

    public decimal Bufsize { get; init; } = Bufsize > 0m
        ? Bufsize
        : throw new ArgumentOutOfRangeException(nameof(Bufsize), Bufsize, "Bufsize must be greater than zero.");

    public string DownscaleAlgorithm { get; init; } = NormalizeRequiredToken(DownscaleAlgorithm, nameof(DownscaleAlgorithm));

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}

/// <summary>
/// Represents one source-height bucket used by the ToMkvGpu downscale profiles.
/// </summary>
internal sealed record ToMkvGpuSourceBucket(string Name, int MinHeight, int MaxHeight)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
