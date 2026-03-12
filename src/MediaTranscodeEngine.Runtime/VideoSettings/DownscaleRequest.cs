namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это явное намерение downscale.
*/
/// <summary>
/// Captures explicit downscale intent and scaling-specific overrides.
/// </summary>
public sealed class DownscaleRequest
{
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

        TargetHeight = targetHeight;
        Algorithm = NormalizeName(algorithm);
    }

    /// <summary>
    /// Gets the requested target height.
    /// </summary>
    public int TargetHeight { get; }

    /// <summary>
    /// Gets the explicit scaling algorithm override.
    /// </summary>
    public string? Algorithm { get; }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
