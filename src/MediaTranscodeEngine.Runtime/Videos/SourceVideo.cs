namespace MediaTranscodeEngine.Runtime.Videos;

/// <summary>
/// Represents a source video file with normalized facts that scenarios can use to make decisions.
/// </summary>
public sealed record SourceVideo(
    string FilePath,
    string Container,
    string VideoCodec,
    IReadOnlyList<string> AudioCodecs,
    int Width,
    int Height,
    double FramesPerSecond,
    TimeSpan Duration);
