namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это явное намерение downscale.
Отдельный тип нужен, чтобы факт изменения высоты не угадывался по полям общего VideoSettingsRequest.
*/
/// <summary>
/// Captures explicit downscale intent and scaling-specific overrides.
/// </summary>
public sealed class DownscaleRequest
{
    private static readonly int[] SupportedTargetHeightsValues =
        [.. VideoSettingsProfiles.Default.GetSupportedDownscaleTargetHeights()];
    private static readonly string[] SupportedAlgorithmsValues = ["bilinear", "bicubic", "lanczos"];

    /// <summary>
    /// Gets target heights that are supported by configured downscale profiles.
    /// </summary>
    public static IReadOnlyList<int> SupportedTargetHeights => SupportedTargetHeightsValues;

    /// <summary>
    /// Gets the canonical scaling algorithm values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedAlgorithms => SupportedAlgorithmsValues;

    /// <summary>
    /// Initializes explicit downscale directives.
    /// </summary>
    /// <param name="targetHeight">Requested target height.</param>
    /// <param name="algorithm">Explicit scaling algorithm override.</param>
    public DownscaleRequest(int targetHeight, string? algorithm = null)
    {
        if (targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHeight), targetHeight, "Target height must be greater than zero.");
        }

        if (!IsSupportedTargetHeight(targetHeight))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetHeight),
                targetHeight,
                $"Supported values: {GetSupportedTargetHeightsDisplay()}.");
        }

        var normalizedAlgorithm = NormalizeName(algorithm);
        if (normalizedAlgorithm is not null && !IsSupportedAlgorithm(normalizedAlgorithm))
        {
            throw new ArgumentOutOfRangeException(
                nameof(algorithm),
                algorithm,
                $"Supported values: {GetSupportedAlgorithmsDisplay()}.");
        }

        TargetHeight = targetHeight;
        Algorithm = normalizedAlgorithm;
    }

    /// <summary>
    /// Gets the requested target height.
    /// </summary>
    public int TargetHeight { get; }

    /// <summary>
    /// Gets the explicit scaling algorithm override.
    /// </summary>
    public string? Algorithm { get; }

    /// <summary>
    /// Determines whether the supplied target height is supported by configured downscale profiles.
    /// </summary>
    public static bool IsSupportedTargetHeight(int targetHeight)
    {
        return Array.IndexOf(SupportedTargetHeightsValues, targetHeight) >= 0;
    }

    /// <summary>
    /// Determines whether the supplied scaling algorithm is supported.
    /// </summary>
    public static bool IsSupportedAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SupportedAlgorithmsValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSupportedTargetHeightsDisplay()
    {
        return string.Join(", ", SupportedTargetHeightsValues);
    }

    private static string GetSupportedAlgorithmsDisplay()
    {
        return string.Join(", ", SupportedAlgorithmsValues);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
