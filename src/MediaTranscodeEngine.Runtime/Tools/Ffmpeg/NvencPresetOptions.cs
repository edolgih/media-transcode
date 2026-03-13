namespace MediaTranscodeEngine.Runtime.Tools.Ffmpeg;

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

    public static IReadOnlyList<string> SupportedPresets => SupportedPresetsValues;

    public static string SupportedPresetsDisplay => string.Join(", ", SupportedPresetsValues);

    public static string SupportedPresetsHelpDisplay => string.Join("|", SupportedPresetsValues);

    public static bool IsSupportedPreset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return SupportedPresetsValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
