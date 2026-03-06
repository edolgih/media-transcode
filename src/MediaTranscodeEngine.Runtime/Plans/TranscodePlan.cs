namespace MediaTranscodeEngine.Runtime.Plans;

/// <summary>
/// Describes a tool-agnostic transcode intent produced by a scenario for a specific source video.
/// </summary>
public sealed record TranscodePlan(
    string TargetContainer,
    string? TargetVideoCodec,
    string? PreferredBackend,
    int? TargetHeight,
    double? TargetFramesPerSecond,
    bool UseFrameInterpolation,
    bool CopyVideo,
    bool CopyAudio,
    bool FixTimestamps,
    bool KeepSource,
    string? OutputPath = null);
