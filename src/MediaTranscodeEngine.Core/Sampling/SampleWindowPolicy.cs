namespace MediaTranscodeEngine.Core.Sampling;

public sealed record SampleWindowPolicy(
    double LongVideoThresholdSeconds,
    double MediumVideoThresholdSeconds,
    IReadOnlyList<double> LongVideoAnchors,
    IReadOnlyList<double> MediumVideoAnchors,
    IReadOnlyList<double> ShortVideoAnchors)
{
    public static readonly SampleWindowPolicy Default = new(
        LongVideoThresholdSeconds: 5_400,
        MediumVideoThresholdSeconds: 1_800,
        LongVideoAnchors: [0.15, 0.50, 0.85],
        MediumVideoAnchors: [0.30, 0.70],
        ShortVideoAnchors: [0.50]);
}
