namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Профиль качества интерполяции из входных параметров сценария (low/default/high).
Тип нужен, чтобы не передавать "сырые" строки по коду и централизовать маппинг
в конкретную модель RIFE через ResolveModelName().
*/
/// <summary>
/// User-facing interpolation quality profile (<c>low</c>, <c>default</c>, <c>high</c>).
/// </summary>
public sealed record InterpolationQualityProfile
{
    public static readonly InterpolationQualityProfile Low = new("low");
    public static readonly InterpolationQualityProfile Default = new("default");
    public static readonly InterpolationQualityProfile High = new("high");

    private const string SupportedValuesText = "low, default, high";
    private static readonly string[] SupportedValuesArray = [Low.Value, Default.Value, High.Value];

    private InterpolationQualityProfile(string value) => Value = value;

    /// <summary>
    /// Gets the normalized profile token.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported interpolation quality profiles.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Parses and normalizes a required interpolation quality profile.
    /// </summary>
    public static InterpolationQualityProfile Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "low" => Low,
            "default" => Default,
            "high" => High,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /// <summary>
    /// Returns <see cref="Default"/> when value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static InterpolationQualityProfile ParseOrDefault(string? value, string paramName) =>
        value is null ? Default : Parse(value, paramName);

    /// <summary>
    /// Returns <see langword="null"/> when value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static InterpolationQualityProfile? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out InterpolationQualityProfile? profile)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            profile = null;
            return false;
        }

        profile = value.Trim().ToLowerInvariant() switch
        {
            "low" => Low,
            "default" => Default,
            "high" => High,
            _ => null
        };

        return profile is not null;
    }

    /// <summary>
    /// Maps this quality profile to a concrete RIFE model name.
    /// </summary>
    public InterpolationModelName ResolveModelName()
    {
        return this switch
        {
            var profile when profile == Low => InterpolationModelName.Rife425Lite,
            var profile when profile == Default => InterpolationModelName.Rife425,
            var profile when profile == High => InterpolationModelName.Rife426Heavy,
            _ => throw new InvalidOperationException($"Unsupported interpolation quality profile '{Value}'.")
        };
    }

    public override string ToString() => Value;
}

/*
Конкретное имя модели RIFE, которое передается в tool-слой и docker-команду
(например, 4.25 / 4.25.lite / 4.26.heavy).
*/
/// <summary>
/// Concrete RIFE model name token used by command rendering.
/// </summary>
public sealed record InterpolationModelName
{
    public static readonly InterpolationModelName Rife425Lite = new("4.25.lite");
    public static readonly InterpolationModelName Rife425 = new("4.25");
    public static readonly InterpolationModelName Rife426Heavy = new("4.26.heavy");

    private const string SupportedValuesText = "4.25.lite, 4.25, 4.26.heavy";
    private static readonly string[] SupportedValuesArray = [Rife425Lite.Value, Rife425.Value, Rife426Heavy.Value];

    private InterpolationModelName(string value) => Value = value;

    /// <summary>
    /// Gets the exact model token passed to the tool layer.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets all supported interpolation model names.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /// <summary>
    /// Parses and normalizes a required interpolation model name.
    /// </summary>
    public static InterpolationModelName Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim() switch
        {
            "4.25.lite" => Rife425Lite,
            "4.25" => Rife425,
            "4.26.heavy" => Rife426Heavy,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    public override string ToString() => Value;
}
