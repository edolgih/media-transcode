using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это реестр типизированных video-settings профилей shared core-слоя.
Он хранит profile data по целевой высоте и выступает единым source of truth для ordinary encode и downscale.
*/
/// <summary>
/// Provides typed video-settings profiles used by Core.
/// </summary>
internal sealed class VideoSettingsProfiles
{
    private readonly IReadOnlyDictionary<int, VideoSettingsProfile> _profilesByTargetHeight;
    private readonly int[] _supportedDownscaleTargetHeights;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    internal VideoSettingsProfiles(IReadOnlyDictionary<int, VideoSettingsProfile> profilesByTargetHeight)
    {
        _profilesByTargetHeight = profilesByTargetHeight;
        _supportedDownscaleTargetHeights = profilesByTargetHeight.Values
            .Where(static profile => profile.SupportsDownscale)
            .OrderByDescending(static profile => profile.TargetHeight)
            .Select(static profile => profile.TargetHeight)
            .ToArray();
        _supportedContentProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.ContentProfile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _supportedQualityProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.QualityProfile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static VideoSettingsProfiles Default { get; } = CreateDefault();

    public VideoSettingsProfile GetRequiredProfile(int targetHeight)
    {
        if (_profilesByTargetHeight.TryGetValue(targetHeight, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"Video settings profile '{targetHeight}' is not configured.");
    }

    public bool TryGetProfile(int targetHeight, out VideoSettingsProfile profile)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out profile!);
    }

    public VideoSettingsProfile ResolveOutputProfile(int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        return _profilesByTargetHeight.Values
            .OrderBy(profile => Math.Abs(profile.TargetHeight - outputHeight))
            .ThenByDescending(profile => profile.TargetHeight)
            .First();
    }

    public IReadOnlyList<int> GetSupportedDownscaleTargetHeights()
    {
        return _supportedDownscaleTargetHeights;
    }

    public IReadOnlyList<string> GetSupportedContentProfiles()
    {
        return _supportedContentProfiles;
    }

    public IReadOnlyList<string> GetSupportedQualityProfiles()
    {
        return _supportedQualityProfiles;
    }

    public bool SupportsDownscaleTargetHeight(int targetHeight)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out var profile) &&
               profile.SupportsDownscale;
    }

    internal static VideoSettingsProfiles Create(params VideoSettingsProfile[] profiles)
    {
        return new VideoSettingsProfiles(profiles.ToDictionary(static profile => profile.TargetHeight));
    }

    private static VideoSettingsProfiles CreateDefault()
    {
        return Create(
            VideoSettings1080Profile.Create(),
            VideoSettings720Profile.Create(),
            VideoSettings424Profile.Create(),
            VideoSettings480Profile.Create(),
            VideoSettings576Profile.Create());
    }
}

/*
Это одна запись default-настроек внутри video-settings профиля.
Она описывает базовые CQ/maxrate/bufsize и алгоритм масштабирования.
*/
/// <summary>
/// Represents one default settings entry inside a typed video-settings profile.
/// </summary>
internal sealed record VideoSettingsDefaults(
    string ContentProfile,
    string QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize,
    string Algorithm,
    int CqMin,
    int CqMax,
    decimal MaxrateMin,
    decimal MaxrateMax)
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

    public string Algorithm { get; init; } = NormalizeRequiredToken(Algorithm, nameof(Algorithm));

    public int CqMin { get; init; } = CqMin > 0
        ? CqMin
        : throw new ArgumentOutOfRangeException(nameof(CqMin), CqMin, "CQ minimum must be greater than zero.");

    public int CqMax { get; init; } = CqMax >= CqMin
        ? CqMax
        : throw new ArgumentOutOfRangeException(nameof(CqMax), CqMax, "CQ maximum must be greater than or equal to minimum.");

    public decimal MaxrateMin { get; init; } = MaxrateMin > 0m
        ? MaxrateMin
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMin), MaxrateMin, "Maxrate minimum must be greater than zero.");

    public decimal MaxrateMax { get; init; } = MaxrateMax >= MaxrateMin
        ? MaxrateMax
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMax), MaxrateMax, "Maxrate maximum must be greater than or equal to minimum.");

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}

/*
Это коэффициенты rate model для video-settings профиля.
Они используются, когда пользователь переопределяет CQ и нужно пересчитать maxrate/bufsize.
*/
/// <summary>
/// Stores the shared rate-model constants used when CQ is overridden.
/// </summary>
internal sealed record VideoSettingsRateModel(decimal CqStepToMaxrateStep, decimal BufsizeMultiplier)
{
    public decimal CqStepToMaxrateStep { get; init; } = CqStepToMaxrateStep > 0m
        ? CqStepToMaxrateStep
        : throw new ArgumentOutOfRangeException(nameof(CqStepToMaxrateStep), CqStepToMaxrateStep, "CQ step must be greater than zero.");

    public decimal BufsizeMultiplier { get; init; } = BufsizeMultiplier > 0m
        ? BufsizeMultiplier
        : throw new ArgumentOutOfRangeException(nameof(BufsizeMultiplier), BufsizeMultiplier, "Bufsize multiplier must be greater than zero.");
}

/*
Это необязательный bounds-override для конкретного source-height bucket.
Он позволяет локально подправить допустимые границы профиля.
*/
/// <summary>
/// Represents one optional bounds override attached to a source-height bucket.
/// </summary>
internal sealed record VideoSettingsBoundsOverride(
    string ContentProfile,
    string QualityProfile,
    int? CqMin = null,
    int? CqMax = null,
    decimal? MaxrateMin = null,
    decimal? MaxrateMax = null)
{
    public string ContentProfile { get; init; } = NormalizeRequiredToken(ContentProfile, nameof(ContentProfile));

    public string QualityProfile { get; init; } = NormalizeRequiredToken(QualityProfile, nameof(QualityProfile));

    public int? CqMin { get; init; } = NormalizeOptionalPositiveInt(CqMin, nameof(CqMin));

    public int? CqMax { get; init; } = NormalizeOptionalPositiveInt(CqMax, nameof(CqMax));

    public decimal? MaxrateMin { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMin, nameof(MaxrateMin));

    public decimal? MaxrateMax { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMax, nameof(MaxrateMax));

    public bool Matches(string contentProfile, string qualityProfile)
    {
        return ContentProfile.Equals(contentProfile, StringComparison.OrdinalIgnoreCase) &&
               QualityProfile.Equals(qualityProfile, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

    private static decimal? NormalizeOptionalPositiveDecimal(decimal? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0m
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }
}

/*
Это bucket исходной высоты для video-settings профиля.
Через него профиль различает правила для разных source-size диапазонов.
*/
/// <summary>
/// Represents one source-height bucket used by video-settings profiles.
/// </summary>
internal sealed record SourceHeightBucket(
    string Name,
    int MinHeight,
    int MaxHeight,
    IReadOnlyList<VideoSettingsBoundsOverride>? BoundsOverrides = null,
    bool IsDefault = false)
{
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    public IReadOnlyList<VideoSettingsBoundsOverride> BoundsOverrides { get; init; } = BoundsOverrides ?? Array.Empty<VideoSettingsBoundsOverride>();

    public bool IsDefault { get; init; } = IsDefault;

    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    public VideoSettingsBoundsOverride? ResolveBoundsOverride(string contentProfile, string qualityProfile)
    {
        return BoundsOverrides.FirstOrDefault(overrideEntry => overrideEntry.Matches(contentProfile, qualityProfile));
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
