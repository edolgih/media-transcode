namespace MediaTranscodeEngine.Core.Profiles;

public sealed record ProfileRateModel(
    double CqStepToMaxrateStep,
    double BufsizeMultiplier);

public sealed record ProfileDefaultsModel(
    int Cq,
    double Maxrate,
    double Bufsize);

public sealed record ProfileLimitsModel(
    int CqMin,
    int CqMax,
    double MaxrateMin,
    double MaxrateMax);

public sealed record ContentProfileDefinition(
    string AlgoDefault,
    IReadOnlyDictionary<string, ProfileDefaultsModel> Defaults,
    IReadOnlyDictionary<string, ProfileLimitsModel> Limits);

public sealed record ReductionRangeDefinition(
    double? MinInclusive = null,
    double? MinExclusive = null,
    double? MaxInclusive = null,
    double? MaxExclusive = null);

public sealed record SourceBucketMatchDefinition(
    double? MinHeightInclusive = null,
    double? MinHeightExclusive = null,
    double? MaxHeightInclusive = null,
    double? MaxHeightExclusive = null);

public sealed record SourceBucketDefinition(
    string Name,
    SourceBucketMatchDefinition? Match = null,
    bool IsDefault = false,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRangeDefinition>>? ContentQualityRanges = null,
    IReadOnlyDictionary<string, ReductionRangeDefinition>? QualityRanges = null);

public sealed record AutoSamplingDefinition(
    bool EnabledByDefault = true,
    int MaxIterations = 8,
    string ModeDefault = "accurate",
    int HybridAccurateIterations = 2);

public sealed record TranscodeProfileDefinition(
    IReadOnlyDictionary<string, ContentProfileDefinition> ContentProfiles,
    ProfileRateModel RateModel,
    IReadOnlyDictionary<string, ReductionRangeDefinition>? QualityRanges = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRangeDefinition>>? ContentQualityRanges = null,
    IReadOnlyList<SourceBucketDefinition>? SourceBuckets = null,
    AutoSamplingDefinition? AutoSampling = null);
