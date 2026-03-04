using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Resolutions;

public sealed record ResolutionPolicyResult(
    bool IsSupported,
    bool ApplyDownscale,
    string? SourceBucketName = null,
    QualitySettings? Settings = null,
    string? Error = null);
