namespace MediaTranscodeEngine.Core.Resolutions;

public sealed record ResolutionTransform(
    int? SourceHeight,
    int? TargetHeight);
