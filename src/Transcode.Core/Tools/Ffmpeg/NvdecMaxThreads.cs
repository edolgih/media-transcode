using System.Globalization;

namespace Transcode.Core.Tools.Ffmpeg;

/*
Допустимое значение лимита потоков для NVDEC decode.
*/
/// <summary>
/// Represents one supported NVDEC decode thread limit.
/// </summary>
public sealed record NvdecMaxThreads
{
    private const int MinimumValue = 1;
    private const int MaximumValue = 32;

    private NvdecMaxThreads(int value) => Value = value;

    /// <summary>
    /// Gets the minimum supported NVDEC decode thread limit.
    /// </summary>
    public static int Minimum => MinimumValue;

    /// <summary>
    /// Gets the maximum supported NVDEC decode thread limit.
    /// </summary>
    public static int Maximum => MaximumValue;

    /// <summary>
    /// Gets the normalized NVDEC decode thread limit.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Parses and validates a required NVDEC decode thread limit.
    /// </summary>
    public static NvdecMaxThreads Parse(int value, string paramName)
    {
        if (value < MinimumValue || value > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                $"Value must be in range {MinimumValue}..{MaximumValue}.");
        }

        return new NvdecMaxThreads(value);
    }

    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static NvdecMaxThreads? ParseOptional(int? value, string paramName) =>
        value.HasValue ? Parse(value.Value, paramName) : null;

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(int value, out NvdecMaxThreads? maxThreads)
    {
        if (value < MinimumValue || value > MaximumValue)
        {
            maxThreads = null;
            return false;
        }

        maxThreads = new NvdecMaxThreads(value);
        return true;
    }

    /// <summary>
    /// Determines whether the supplied NVDEC decode thread limit is supported.
    /// </summary>
    public static bool IsSupported(int value) => TryParse(value, out _);

    /// <summary>
    /// Returns the normalized thread-limit token used by command rendering.
    /// </summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
