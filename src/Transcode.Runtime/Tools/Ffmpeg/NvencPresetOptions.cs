namespace Transcode.Runtime.Tools.Ffmpeg;

/*
Это общий источник supported NVENC preset values.
Он нужен, чтобы request parsing и CLI help брали один и тот же набор значений.
*/
/// <summary>
/// Provides supported NVENC preset values shared by Runtime and CLI help.
/// </summary>
public static class NvencPresetOptions
{
    private static readonly string[] SupportedPresetsValues = ["p1", "p2", "p3", "p4", "p5", "p6", "p7"];

    /// <summary>
    /// Gets the canonical NVENC preset values supported by Runtime.
    /// </summary>
    public static IReadOnlyList<string> SupportedPresets => SupportedPresetsValues;

    /// <summary>
    /// Determines whether the supplied NVENC preset value is supported.
    /// </summary>
    public static bool IsSupportedPreset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SupportedPresetsValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
