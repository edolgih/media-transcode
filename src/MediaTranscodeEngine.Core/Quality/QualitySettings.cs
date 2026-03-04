namespace MediaTranscodeEngine.Core.Quality;

public sealed record QualitySettings(
    int Cq,
    double Maxrate,
    double Bufsize,
    string DownscaleAlgo);
