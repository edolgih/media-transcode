namespace MediaTranscodeEngine.Core.Classification;

public sealed class DefaultInputClassifier : IInputClassifier
{
    public InputClassification Classify(int? sourceHeight, double? sourceFps)
    {
        var resolutionBucket = ResolveResolutionBucket(sourceHeight);
        var fpsBucket = ResolveFpsBucket(sourceFps);

        return new InputClassification(
            SourceHeight: sourceHeight,
            SourceFps: sourceFps,
            ResolutionBucketKey: resolutionBucket,
            FpsBucketKey: fpsBucket);
    }

    private static string ResolveResolutionBucket(int? sourceHeight)
    {
        if (!sourceHeight.HasValue)
        {
            return "unknown_height";
        }

        if (sourceHeight.Value >= 1000)
        {
            return "fhd_1080";
        }

        if (sourceHeight.Value >= 650)
        {
            return "hd_720";
        }

        return "sd";
    }

    private static string ResolveFpsBucket(double? sourceFps)
    {
        if (!sourceFps.HasValue)
        {
            return "unknown_fps";
        }

        if (sourceFps.Value >= 50)
        {
            return "high_fps";
        }

        if (sourceFps.Value >= 24)
        {
            return "standard_fps";
        }

        return "low_fps";
    }
}
