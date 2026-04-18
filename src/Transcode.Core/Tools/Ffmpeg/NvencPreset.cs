namespace Transcode.Core.Tools.Ffmpeg;

/*
Это одно допустимое значение NVENC preset.
Тип нужен, чтобы не тащить "сырые" строки через Core и сценарии.
*/
/// <summary>
/// Represents one supported NVENC preset value.
/// </summary>
public sealed record NvencPreset
{
    public static readonly NvencPreset P1 = new("p1");
    public static readonly NvencPreset P2 = new("p2");
    public static readonly NvencPreset P3 = new("p3");
    public static readonly NvencPreset P4 = new("p4");
    public static readonly NvencPreset P5 = new("p5");
    public static readonly NvencPreset P6 = new("p6");
    public static readonly NvencPreset P7 = new("p7");

    private static string SupportedValuesText => $"{P1}, {P2}, {P3}, {P4}, {P5}, {P6}, {P7}";
    private static readonly string[] SupportedValuesArray = [P1.Value, P2.Value, P3.Value, P4.Value, P5.Value, P6.Value, P7.Value];

    private NvencPreset(string value) => Value = value;

    /// <summary>
    /// Gets the normalized NVENC preset value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported NVENC preset values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Gets the default NVENC preset used when no override is supplied.
    /// </summary>
    public static NvencPreset Default => P6;

    /// <summary>
    /// Parses and normalizes a required NVENC preset value.
    /// </summary>
    public static NvencPreset Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        var parsed = TryParseCore(value);
        return parsed ?? throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.");
    }

    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static NvencPreset? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out NvencPreset? preset)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            preset = null;
            return false;
        }

        preset = TryParseCore(value);

        return preset is not null;
    }

    /// <summary>
    /// Determines whether the supplied NVENC preset value is supported.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    private static NvencPreset? TryParseCore(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "p1" => P1,
            "p2" => P2,
            "p3" => P3,
            "p4" => P4,
            "p5" => P5,
            "p6" => P6,
            "p7" => P7,
            _ => null
        };
    }

    public override string ToString() => Value;
}
