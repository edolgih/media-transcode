namespace MediaTranscodeEngine.Core.Classification;

public sealed record InputClassification(
    int? SourceHeight,
    double? SourceFps,
    string ResolutionBucketKey,
    string FpsBucketKey);
