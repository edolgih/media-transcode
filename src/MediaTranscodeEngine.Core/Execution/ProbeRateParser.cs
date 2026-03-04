using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

internal static class ProbeRateParser
{
    public static double? ResolveSourceFps(ProbeStream video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var primaryToken = string.IsNullOrWhiteSpace(video.RFrameRate) || video.RFrameRate == "0/0"
            ? video.AvgFrameRate
            : video.RFrameRate;
        if (string.IsNullOrWhiteSpace(primaryToken) || primaryToken == "0/0")
        {
            return null;
        }

        return ParseFpsToken(primaryToken);
    }

    private static double? ParseFpsToken(string token)
    {
        var parts = token.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fps)
            ? fps
            : null;
    }
}
