namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это общая request-модель для video settings.
Она хранит только общие quality/rate overrides и используется и в ordinary encode, и рядом с explicit downscale,
но сама по себе не несет намерения менять разрешение.
*/
/// <summary>
/// Captures reusable video-settings directives independent from a specific scenario.
/// </summary>
public sealed class VideoSettingsRequest
{
    private static readonly string[] SupportedContentProfilesValues = ["anime", "mult", "film"];
    private static readonly string[] SupportedQualityProfilesValues = ["high", "default", "low"];
    private static readonly string[] SupportedAutoSampleModesValues = ["accurate", "fast", "hybrid"];

    public static IReadOnlyList<string> SupportedContentProfiles => SupportedContentProfilesValues;

    public static string SupportedContentProfilesDisplay => string.Join(", ", SupportedContentProfilesValues);

    public static string SupportedContentProfilesHelpDisplay => string.Join("|", SupportedContentProfilesValues);

    public static IReadOnlyList<string> SupportedQualityProfiles => SupportedQualityProfilesValues;

    public static string SupportedQualityProfilesDisplay => string.Join(", ", SupportedQualityProfilesValues);

    public static string SupportedQualityProfilesHelpDisplay => string.Join("|", SupportedQualityProfilesValues);

    public static IReadOnlyList<string> SupportedAutoSampleModes => SupportedAutoSampleModesValues;

    public static string SupportedAutoSampleModesDisplay => string.Join(", ", SupportedAutoSampleModesValues);

    public static string SupportedAutoSampleModesHelpDisplay => string.Join("|", SupportedAutoSampleModesValues);

    /// <summary>
    /// Initializes reusable video-settings directives.
    /// </summary>
    /// <param name="contentProfile">Requested content profile for profile-driven video settings.</param>
    /// <param name="qualityProfile">Requested quality profile for profile-driven video settings.</param>
    /// <param name="autoSampleMode">Requested autosample mode.</param>
    /// <param name="cq">Explicit CQ override.</param>
    /// <param name="maxrate">Explicit maxrate override in Mbit/s.</param>
    /// <param name="bufsize">Explicit bufsize override in Mbit/s.</param>
    public VideoSettingsRequest(
        string? contentProfile = null,
        string? qualityProfile = null,
        string? autoSampleMode = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        if (cq.HasValue && cq.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cq), cq.Value, "CQ must be greater than zero.");
        }

        if (maxrate.HasValue && maxrate.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxrate), maxrate.Value, "Maxrate must be greater than zero.");
        }

        if (bufsize.HasValue && bufsize.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bufsize), bufsize.Value, "Bufsize must be greater than zero.");
        }

        ContentProfile = NormalizeSupportedValue(
            contentProfile,
            nameof(contentProfile),
            SupportedContentProfilesValues,
            SupportedContentProfilesDisplay);
        QualityProfile = NormalizeSupportedValue(
            qualityProfile,
            nameof(qualityProfile),
            SupportedQualityProfilesValues,
            SupportedQualityProfilesDisplay);
        AutoSampleMode = NormalizeSupportedValue(
            autoSampleMode,
            nameof(autoSampleMode),
            SupportedAutoSampleModesValues,
            SupportedAutoSampleModesDisplay);
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
    }

    /// <summary>
    /// Gets the requested content profile.
    /// </summary>
    public string? ContentProfile { get; }

    /// <summary>
    /// Gets the requested quality profile.
    /// </summary>
    public string? QualityProfile { get; }

    /// <summary>
    /// Gets the requested autosample mode.
    /// </summary>
    public string? AutoSampleMode { get; }

    /// <summary>
    /// Gets the explicit CQ override.
    /// </summary>
    public int? Cq { get; }

    /// <summary>
    /// Gets the explicit maxrate override in Mbit/s.
    /// </summary>
    public decimal? Maxrate { get; }

    /// <summary>
    /// Gets the explicit bufsize override in Mbit/s.
    /// </summary>
    public decimal? Bufsize { get; }

    /// <summary>
    /// Gets a value indicating whether any video-settings directive is actually present.
    /// </summary>
    public bool HasValue =>
        !string.IsNullOrWhiteSpace(ContentProfile) ||
        !string.IsNullOrWhiteSpace(QualityProfile) ||
        !string.IsNullOrWhiteSpace(AutoSampleMode) ||
        Cq.HasValue ||
        Maxrate.HasValue ||
        Bufsize.HasValue;

    public static bool IsSupportedContentProfile(string? value)
    {
        return IsSupportedValue(value, SupportedContentProfilesValues);
    }

    public static bool IsSupportedQualityProfile(string? value)
    {
        return IsSupportedValue(value, SupportedQualityProfilesValues);
    }

    public static bool IsSupportedAutoSampleMode(string? value)
    {
        return IsSupportedValue(value, SupportedAutoSampleModesValues);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static bool IsSupportedValue(string? value, IReadOnlyList<string> supportedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return supportedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeSupportedValue(
        string? value,
        string paramName,
        IReadOnlyList<string> supportedValues,
        string display)
    {
        var normalizedValue = NormalizeName(value);
        if (normalizedValue is null)
        {
            return null;
        }

        if (!supportedValues.Contains(normalizedValue, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {display}.");
        }

        return normalizedValue;
    }
}
