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

        ContentProfile = NormalizeName(contentProfile);
        QualityProfile = NormalizeName(qualityProfile);
        AutoSampleMode = NormalizeName(autoSampleMode);
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

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
