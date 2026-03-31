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
        var state = new ParseState();

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (!TryHandleToken(args, ref index, token, state, out errorText))
            {
                return false;
            }
        }

        return TryCreateRequest(state, out request, out errorText);
    }

    private static bool TryHandleToken(
        IReadOnlyList<string> args,
        ref int index,
        string token,
        ParseState state,
        out string? errorText)
    {
        var normalizedToken = token.ToLowerInvariant();
        switch (normalizedToken)
        {
            case KeepSourceOptionName:
                state.KeepSource = true;
                errorText = null;
                return true;
            case FpsMultiplierOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    BuildSupportedError("--fps-multiplier", ToH264RifeRequest.SupportedFramesPerSecondMultipliers),
                    out var framesPerSecondMultiplier,
                    out errorText))
                {
                    return false;
                }

                state.FramesPerSecondMultiplier = framesPerSecondMultiplier ?? state.FramesPerSecondMultiplier;
                return true;
            case InterpQualityOptionName:
                return CliOptionReader.TryReadRequiredValue(
                    args,
                    ref index,
                    token,
                    out state.InterpolationQualityProfile,
                    out errorText);
            case ContentProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(
                    args,
                    ref index,
                    token,
                    out state.ContentProfile,
                    out errorText);
            case QualityProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(
                    args,
                    ref index,
                    token,
                    out state.QualityProfile,
                    out errorText);
            case ContainerOptionName:
                return CliOptionReader.TryReadRequiredValue(
                    args,
                    ref index,
                    token,
                    out state.OutputContainer,
                    out errorText);
            default:
                errorText = $"Unexpected argument: {token}";
                return false;
        }
    }

    private static bool TryCreateRequest(ParseState state, out ToH264RifeRequest request, out string? errorText)
    {
        request = default!;
        errorText = null;

        try
        {
            var videoSettings = VideoSettingsRequest.CreateOrNull(
                contentProfile: state.ContentProfile,
                qualityProfile: state.QualityProfile);
            request = new ToH264RifeRequest(
                keepSource: state.KeepSource,
                framesPerSecondMultiplier: state.FramesPerSecondMultiplier,
                interpolationQualityProfile: state.InterpolationQualityProfile,
                outputContainer: state.OutputContainer,
                videoSettings: videoSettings);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = MapOutOfRangeError(exception);
            return false;
        }
    }

    private static string MapOutOfRangeError(ArgumentOutOfRangeException exception)
    {
        return exception.ParamName switch
        {
            "framesPerSecondMultiplier" => BuildSupportedError("--fps-multiplier", ToH264RifeRequest.SupportedFramesPerSecondMultipliers),
            "interpolationQualityProfile" => BuildSupportedError("--interp-quality", ToH264RifeRequest.SupportedInterpolationQualityProfiles),
            "contentProfile" => BuildSupportedError("--content-profile", ToH264RifeRequest.SupportedContentProfiles),
            "qualityProfile" => BuildSupportedError("--quality-profile", ToH264RifeRequest.SupportedQualityProfiles),
            "outputContainer" => BuildSupportedError("--container", ToH264RifeRequest.SupportedContainers),
            _ => exception.Message
        };
    }

    private static string BuildSupportedError<T>(string optionName, IReadOnlyList<T> supportedValues)
    {
        return $"{optionName} must be one of: {CliValueFormatter.FormatList(supportedValues)}.";
    }

    private sealed class ParseState
    {
        public bool KeepSource;
        public int FramesPerSecondMultiplier = 2;
        public string? InterpolationQualityProfile;
        public string? ContentProfile;
        public string? QualityProfile;
        public string? OutputContainer;
    }
}
