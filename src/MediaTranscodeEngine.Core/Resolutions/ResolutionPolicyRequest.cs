namespace MediaTranscodeEngine.Core.Resolutions;

public sealed record ResolutionPolicyRequest(
    ResolutionTransform Transform,
    string ContentProfile,
    string QualityProfile,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    string? DownscaleAlgo = null);
