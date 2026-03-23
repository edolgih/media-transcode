namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Holds scenario-specific input for the <c>toh264rife</c> workflow.
/// </summary>
public sealed class ToH264RifeRequest
{
    public static IReadOnlyList<int> SupportedTargetFrameRates { get; } = [50, 60];

    public static IReadOnlyList<string> SupportedContainers { get; } = ["mp4", "mkv"];

    public ToH264RifeRequest(
        bool keepSource = false,
        int? targetFramesPerSecond = null,
        string? outputContainer = null)
    {
        if (targetFramesPerSecond.HasValue &&
            !SupportedTargetFrameRates.Contains(targetFramesPerSecond.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetFramesPerSecond),
                targetFramesPerSecond,
                $"Value must be one of: {string.Join(", ", SupportedTargetFrameRates)}.");
        }

        if (!string.IsNullOrWhiteSpace(outputContainer) &&
            !SupportedContainers.Contains(outputContainer.Trim().ToLowerInvariant()))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputContainer),
                outputContainer,
                $"Value must be one of: {string.Join(", ", SupportedContainers)}.");
        }

        KeepSource = keepSource;
        TargetFramesPerSecond = targetFramesPerSecond;
        OutputContainer = string.IsNullOrWhiteSpace(outputContainer)
            ? null
            : outputContainer.Trim().ToLowerInvariant();
    }

    public bool KeepSource { get; }

    public int? TargetFramesPerSecond { get; }

    public string? OutputContainer { get; }
}
