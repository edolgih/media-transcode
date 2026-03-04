namespace MediaTranscodeEngine.Core.Quality;

public sealed record QualitySelectionContext(
    string ContentProfile,
    string QualityProfile,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    string? DownscaleAlgo = null);
