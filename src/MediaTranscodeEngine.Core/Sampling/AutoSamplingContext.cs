using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Sampling;

public sealed record AutoSamplingContext(
    string ContentProfile,
    string QualityProfile,
    QualitySettings BaseSettings,
    int? SourceHeight,
    string Mode,
    Func<int, double, double, double?> AccurateReductionProvider,
    Func<int, double, double, double?> FastReductionProvider);
