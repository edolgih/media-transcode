using Transcode.Cli.Core.Parsing;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/// <summary>
/// Parses <c>toh264rife</c> CLI tokens into a scenario request.
/// </summary>
internal static class ToH264RifeCliRequestParser
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string FpsMultiplierOptionName = "--fps-multiplier";
    private const string InterpQualityOptionName = "--interp-quality";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string ContainerOptionName = "--container";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToH264RifeRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var keepSource = false;
        var framesPerSecondMultiplier = 2;
        string? interpolationQualityProfile = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? outputContainer = null;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, FpsMultiplierOptionName, StringComparison.OrdinalIgnoreCase))
            {
                int? parsedMultiplier;
                if (!CliOptionReader.TryReadInt(
                        args,
                        ref index,
                        token,
                        $"--fps-multiplier must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedFramesPerSecondMultipliers)}.",
                        out parsedMultiplier,
                        out errorText))
                {
                    return false;
                }

                framesPerSecondMultiplier = parsedMultiplier ?? framesPerSecondMultiplier;
                continue;
            }

            if (string.Equals(token, InterpQualityOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out interpolationQualityProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, ContentProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out contentProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, QualityProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out qualityProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, ContainerOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out outputContainer, out errorText))
                {
                    return false;
                }

                continue;
            }

            errorText = $"Unexpected argument: {token}";
            return false;
        }

        try
        {
            var videoSettings = VideoSettingsRequest.CreateOrNull(
                contentProfile: contentProfile,
                qualityProfile: qualityProfile);
            request = new ToH264RifeRequest(
                keepSource: keepSource,
                framesPerSecondMultiplier: framesPerSecondMultiplier,
                interpolationQualityProfile: interpolationQualityProfile,
                outputContainer: outputContainer,
                videoSettings: videoSettings);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = exception.ParamName switch
            {
                "framesPerSecondMultiplier" => $"--fps-multiplier must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedFramesPerSecondMultipliers)}.",
                "interpolationQualityProfile" => $"--interp-quality must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedInterpolationQualityProfiles)}.",
                "contentProfile" => $"--content-profile must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedContentProfiles)}.",
                "qualityProfile" => $"--quality-profile must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedQualityProfiles)}.",
                "outputContainer" => $"--container must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedContainers)}.",
                _ => exception.Message
            };
            return false;
        }
    }
}
