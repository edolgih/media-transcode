namespace MediaTranscodeEngine.Runtime.Videos;

/// <summary>
/// Loads a source video from a file path and normalizes the metadata required by scenarios.
/// </summary>
public abstract class VideoInspector
{
    /// <summary>
    /// Reads a video file and returns its normalized metadata representation.
    /// </summary>
    /// <param name="filePath">Path to the source video file.</param>
    /// <returns>A normalized source video description.</returns>
    public abstract SourceVideo Load(string filePath);
}
