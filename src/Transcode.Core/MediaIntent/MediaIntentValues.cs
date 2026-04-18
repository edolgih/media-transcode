namespace Transcode.Core.MediaIntent;

/*
Это одно допустимое значение целевого контейнера.
Тип нужен, чтобы убрать "сырые" строки из decision и scenario-слоев.
*/
/// <summary>
/// Represents one supported target container value.
/// </summary>
public sealed record TargetContainer
{
    public static readonly TargetContainer Mp4 = new("mp4");
    public static readonly TargetContainer Mkv = new("mkv");

    private const string SupportedValuesText = "mp4, mkv";
    private static readonly string[] SupportedValuesArray = [Mp4.Value, Mkv.Value];

    private TargetContainer(string value) => Value = value;

    /// <summary>
    /// Gets the normalized container value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported target container values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Parses and normalizes a required target container value.
    /// </summary>
    public static TargetContainer Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "mp4" => Mp4,
            "mkv" => Mkv,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static TargetContainer? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out TargetContainer? container)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            container = null;
            return false;
        }

        container = value.Trim().ToLowerInvariant() switch
        {
            "mp4" => Mp4,
            "mkv" => Mkv,
            _ => null
        };

        return container is not null;
    }

    /// <summary>
    /// Determines whether the supplied target container value is supported.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    public override string ToString() => Value;
}

/*
Это одно допустимое значение целевого видеокодека encode-пути.
*/
/// <summary>
/// Represents one supported target video codec value.
/// </summary>
public sealed record TargetVideoCodec
{
    public static readonly TargetVideoCodec H264 = new("h264");
    public static readonly TargetVideoCodec H265 = new("h265");

    private const string SupportedValuesText = "h264, h265";
    private static readonly string[] SupportedValuesArray = [H264.Value, H265.Value];

    private TargetVideoCodec(string value) => Value = value;

    /// <summary>
    /// Gets the normalized codec value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported target video codec values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Parses and normalizes a required target video codec value.
    /// </summary>
    public static TargetVideoCodec Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "h264" => H264,
            "h265" => H265,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static TargetVideoCodec? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out TargetVideoCodec? codec)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            codec = null;
            return false;
        }

        codec = value.Trim().ToLowerInvariant() switch
        {
            "h264" => H264,
            "h265" => H265,
            _ => null
        };

        return codec is not null;
    }

    /// <summary>
    /// Determines whether the supplied target video codec value is supported.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    public override string ToString() => Value;
}

/*
Это одно допустимое значение preferred backend для encode-пути.
*/
/// <summary>
/// Represents one supported preferred backend value.
/// </summary>
public sealed record VideoBackend
{
    public static readonly VideoBackend Gpu = new("gpu");
    public static readonly VideoBackend Cpu = new("cpu");

    private const string SupportedValuesText = "gpu, cpu";
    private static readonly string[] SupportedValuesArray = [Gpu.Value, Cpu.Value];

    private VideoBackend(string value) => Value = value;

    /// <summary>
    /// Gets the normalized backend value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported backend values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Parses and normalizes a required backend value.
    /// </summary>
    public static VideoBackend Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "gpu" => Gpu,
            "cpu" => Cpu,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static VideoBackend? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out VideoBackend? backend)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            backend = null;
            return false;
        }

        backend = value.Trim().ToLowerInvariant() switch
        {
            "gpu" => Gpu,
            "cpu" => Cpu,
            _ => null
        };

        return backend is not null;
    }

    /// <summary>
    /// Determines whether the supplied backend value is supported.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    public override string ToString() => Value;
}
