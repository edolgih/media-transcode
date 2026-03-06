using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios;

/// <summary>
/// Encapsulates domain rules that inspect a source video and produce a tool-agnostic transcode plan.
/// </summary>
public abstract class TranscodeScenario
{
    /// <summary>
    /// Initializes a named scenario.
    /// </summary>
    /// <param name="name">Stable scenario name.</param>
    protected TranscodeScenario(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// Gets the stable scenario name used by callers to select this behavior.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Builds a tool-agnostic transcode plan for the provided source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic transcode plan.</returns>
    public TranscodePlan BuildPlan(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var plan = BuildPlanCore(video);
        return plan ?? throw new InvalidOperationException($"Scenario '{Name}' returned null transcode plan.");
    }

    /// <summary>
    /// Builds a tool-agnostic transcode plan for the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic transcode plan.</returns>
    protected abstract TranscodePlan BuildPlanCore(SourceVideo video);
}
