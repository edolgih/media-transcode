using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Holds scenario-specific input for the <c>toh264rife</c> workflow.
/// </summary>
public sealed class ToH264RifeRequest
{
    public static IReadOnlyList<int> SupportedFramesPerSecondMultipliers { get; } = [2, 3];

    public static IReadOnlyList<string> SupportedContainers { get; } = ["mp4", "mkv"];

    public static IReadOnlyList<string> SupportedInterpolationQualityProfiles { get; } = ["low", "default", "high"];

    public static IReadOnlyList<string> SupportedContentProfiles => VideoSettingsRequest.SupportedContentProfiles;

    public static IReadOnlyList<string> SupportedQualityProfiles => VideoSettingsRequest.SupportedQualityProfiles;

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

    public bool KeepSource { get; }

    public int FramesPerSecondMultiplier { get; }

    public string InterpolationQualityProfile { get; }

    public string? OutputContainer { get; }

    public VideoSettingsRequest? VideoSettings { get; }

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
