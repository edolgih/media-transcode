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
    public SourceVideo Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = Path.GetFullPath(filePath.Trim());
        var video = LoadCore(normalizedPath);
        return video ?? throw new InvalidOperationException("Video inspector returned null source video.");
    }

    /// <summary>
    /// Loads a source video from a normalized file path.
    /// </summary>
    /// <param name="filePath">Normalized full path to the source video file.</param>
    /// <returns>A normalized source video description.</returns>
    protected abstract SourceVideo LoadCore(string filePath);
}
