using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Scenarios;

/// <summary>
/// Encapsulates domain rules that inspect a source video and produce a tool-agnostic transcode plan.
/// </summary>
public abstract class TranscodeScenario
{
    /// <summary>
    /// Gets the stable scenario name used by callers to select this behavior.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Builds a tool-agnostic transcode plan for the provided source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    /// <returns>A tool-agnostic transcode plan.</returns>
    public abstract TranscodePlan BuildPlan(SourceVideo video);
}
