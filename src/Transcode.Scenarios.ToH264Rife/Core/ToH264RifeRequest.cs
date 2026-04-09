using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это входной запрос сценария toh264rife.
Он хранит только доменные параметры сценария: множитель FPS, профиль интерполяции, контейнер и video-settings override.
*/
/// <summary>
/// Captures scenario-specific input parameters for <c>toh264rife</c>.
/// </summary>
public sealed class ToH264RifeRequest
{
    /*
    Это поддерживаемые множители FPS для интерполяции.
    */
    /// <summary>
    /// Gets supported frame-rate multipliers for interpolation.
    /// </summary>
    public static IReadOnlyList<int> SupportedFramesPerSecondMultipliers { get; } = [2, 3];

    /*
    Это контейнеры, в которые сценарий может сформировать результат.
    */
    /// <summary>
    /// Gets supported output containers.
    /// </summary>
    public static IReadOnlyList<string> SupportedContainers { get; } = ["mp4", "mkv"];

    /*
    Это профили качества интерполяции, которые знает сценарий.
    */
    /// <summary>
    /// Gets supported interpolation quality profiles.
    /// </summary>
    public static IReadOnlyList<string> SupportedInterpolationQualityProfiles { get; } = ["low", "default", "high"];

    /*
    Это поддерживаемые профили контента для финального NVENC-кодирования.
    */
    /// <summary>
    /// Gets supported content profiles for final video settings.
    /// </summary>
    public static IReadOnlyList<string> SupportedContentProfiles => VideoSettingsRequest.SupportedContentProfiles;

    /*
    Это поддерживаемые профили качества для финального NVENC-кодирования.
    */
    /// <summary>
    /// Gets supported quality profiles for final video settings.
    /// </summary>
    public static IReadOnlyList<string> SupportedQualityProfiles => VideoSettingsRequest.SupportedQualityProfiles;

    /*
    Это конструктор запроса сценария.
    Он нормализует строковые значения и валидирует, что все параметры входят в поддерживаемые наборы.
    */
    /// <summary>
    /// Initializes scenario directives for <c>toh264rife</c>.
    /// </summary>
    public ToH264RifeRequest(
        bool keepSource = false,
        int framesPerSecondMultiplier = 2,
        string? interpolationQualityProfile = null,
        string? outputContainer = null,
        VideoSettingsRequest? videoSettings = null)
    {
        if (!SupportedFramesPerSecondMultipliers.Contains(framesPerSecondMultiplier))
        {
            throw new ArgumentOutOfRangeException(
                nameof(framesPerSecondMultiplier),
                framesPerSecondMultiplier,
                $"Value must be one of: {string.Join(", ", SupportedFramesPerSecondMultipliers)}.");
        }

        if (!string.IsNullOrWhiteSpace(outputContainer) &&
            !SupportedContainers.Contains(outputContainer.Trim().ToLowerInvariant()))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputContainer),
                outputContainer,
                $"Value must be one of: {string.Join(", ", SupportedContainers)}.");
        }

        if (!string.IsNullOrWhiteSpace(interpolationQualityProfile) &&
            !SupportedInterpolationQualityProfiles.Contains(interpolationQualityProfile.Trim().ToLowerInvariant()))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interpolationQualityProfile),
                interpolationQualityProfile,
                $"Value must be one of: {string.Join(", ", SupportedInterpolationQualityProfiles)}.");
        }

        if (videoSettings?.Cq is > 51)
        {
            throw new ArgumentOutOfRangeException("cq", videoSettings.Cq.Value, "CQ must be between 1 and 51.");
        }

        KeepSource = keepSource;
        FramesPerSecondMultiplier = framesPerSecondMultiplier;
        InterpolationQualityProfile = string.IsNullOrWhiteSpace(interpolationQualityProfile)
            ? "default"
            : interpolationQualityProfile.Trim().ToLowerInvariant();
        OutputContainer = string.IsNullOrWhiteSpace(outputContainer)
            ? null
            : outputContainer.Trim().ToLowerInvariant();
        VideoSettings = videoSettings;
    }

    /*
    Это флаг, нужно ли оставлять исходный файл.
    */
    /// <summary>
    /// Gets a value indicating whether the source file should be preserved.
    /// </summary>
    public bool KeepSource { get; }

    /*
    Это множитель FPS для интерполяции кадров.
    */
    /// <summary>
    /// Gets the frame-rate multiplier used for interpolation.
    /// </summary>
    public int FramesPerSecondMultiplier { get; }

    /*
    Это профиль качества интерполяции.
    */
    /// <summary>
    /// Gets the interpolation quality profile.
    /// </summary>
    public string InterpolationQualityProfile { get; }

    /*
    Это явный контейнер результата, если пользователь его задал.
    */
    /// <summary>
    /// Gets the explicitly requested output container, or <see langword="null"/> when the scenario should auto-select it.
    /// </summary>
    public string? OutputContainer { get; }

    /*
    Это переопределения video-settings для финального этапа кодирования.
    */
    /// <summary>
    /// Gets optional video settings overrides for the final encode phase.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /*
    Это соответствие профиля качества интерполяции конкретному имени модели.
    */
    /// <summary>
    /// Resolves the interpolation model name for the supplied quality profile.
    /// </summary>
    public static string ResolveInterpolationModelName(string interpolationQualityProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interpolationQualityProfile);

        return interpolationQualityProfile.Trim().ToLowerInvariant() switch
        {
            "low" => "4.25.lite",
            "default" => "4.25",
            "high" => "4.26.heavy",
            _ => throw new ArgumentOutOfRangeException(
                nameof(interpolationQualityProfile),
                interpolationQualityProfile,
                $"Value must be one of: {string.Join(", ", SupportedInterpolationQualityProfiles)}.")
        };
    }
}
