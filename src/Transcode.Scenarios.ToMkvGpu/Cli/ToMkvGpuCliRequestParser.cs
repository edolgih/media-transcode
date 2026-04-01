using Transcode.Cli.Core.Parsing;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Scenarios.ToMkvGpu.Cli;

/*
Это scenario-local parser для tomkvgpu.
Он знает raw CLI option names и переводит их в scenario request без переноса argv-логики в Core.
*/
/// <summary>
/// Parses ToMkvGpu CLI tokens into a scenario request while keeping raw option names in the CLI layer.
/// </summary>
internal static class ToMkvGpuCliRequestParser
{
    private const string DownscaleOptionName = "--downscale";
    private const string KeepSourceOptionName = "--keep-source";
    private const string ForceEncodeOptionName = "--force-encode";
    private const string OverlayBackgroundOptionName = "--overlay-bg";
    private const string MaxFramesPerSecondOptionName = "--max-fps";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string DownscaleAlgorithmOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToMkvGpuRequest request,
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

        if (!ValidateOptionCombinations(state, out errorText))
        {
            return false;
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
            case ForceEncodeOptionName:
                state.ForceEncode = true;
                errorText = null;
                return true;
            case OverlayBackgroundOptionName:
                state.OverlayBackground = true;
                errorText = null;
                return true;
            case SynchronizeAudioOptionName:
                state.SynchronizeAudio = true;
                errorText = null;
                return true;
            case DownscaleOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    "--downscale must be an integer.",
                    out state.DownscaleTargetHeight,
                    out errorText))
                {
                    return false;
                }

                return true;
            case MaxFramesPerSecondOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    "--max-fps must be an integer.",
                    out state.MaxFramesPerSecond,
                    out errorText))
                {
                    return false;
                }

                return true;
            case CqOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    "--cq must be an integer.",
                    out state.Cq,
                    out errorText))
                {
                    return false;
                }

                return true;
            case MaxrateOptionName:
                if (!CliOptionReader.TryReadDecimal(
                    args,
                    ref index,
                    token,
                    "--maxrate must be a number.",
                    out state.Maxrate,
                    out errorText))
                {
                    return false;
                }

                return true;
            case BufsizeOptionName:
                if (!CliOptionReader.TryReadDecimal(
                    args,
                    ref index,
                    token,
                    "--bufsize must be a number.",
                    out state.Bufsize,
                    out errorText))
                {
                    return false;
                }

                return true;
            case ContentProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.ContentProfile, out errorText);
            case QualityProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.QualityProfile, out errorText);
            case DownscaleAlgorithmOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.Algorithm, out errorText);
            case NvencPresetOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.NvencPreset, out errorText);
            default:
                errorText = token.StartsWith("-", StringComparison.Ordinal)
                    ? $"Unknown option: {token}"
                    : $"Unexpected argument: {token}";
                return false;
        }
    }

    private static bool ValidateOptionCombinations(ParseState state, out string? errorText)
    {
        errorText = null;

        if (state.DownscaleTargetHeight is null && !string.IsNullOrWhiteSpace(state.Algorithm))
        {
            errorText = "--downscale-algo requires --downscale.";
            return false;
        }

        return true;
    }

    private static bool TryCreateRequest(ParseState state, out ToMkvGpuRequest request, out string? errorText)
    {
        request = default!;
        errorText = null;

        try
        {
            var videoSettingsRequest = VideoSettingsRequest.CreateOrNull(
                contentProfile: state.ContentProfile,
                qualityProfile: state.QualityProfile,
                cq: state.Cq,
                maxrate: state.Maxrate,
                bufsize: state.Bufsize);
            var downscaleRequest = state.DownscaleTargetHeight.HasValue
                ? new DownscaleRequest(state.DownscaleTargetHeight.Value, state.Algorithm)
                : null;

            request = new ToMkvGpuRequest(
                overlayBackground: state.OverlayBackground,
                synchronizeAudio: state.SynchronizeAudio,
                keepSource: state.KeepSource,
                forceEncode: state.ForceEncode,
                videoSettings: videoSettingsRequest,
                downscale: downscaleRequest,
                nvencPreset: state.NvencPreset,
                maxFramesPerSecond: state.MaxFramesPerSecond);
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
            "targetHeight" => exception.ActualValue is int actualHeight && actualHeight > 0
                ? BuildSupportedError("--downscale", DownscaleRequest.SupportedTargetHeights)
                : "--downscale must be greater than zero.",
            "algorithm" => BuildSupportedError("--downscale-algo", DownscaleRequest.SupportedAlgorithms),
            "cq" => "--cq must be greater than zero.",
            "maxrate" => "--maxrate must be greater than zero.",
            "bufsize" => "--bufsize must be greater than zero.",
            "maxFramesPerSecond" => BuildSupportedError("--max-fps", ToMkvGpuRequest.SupportedMaxFramesPerSecond),
            "contentProfile" => BuildSupportedError("--content-profile", VideoSettingsRequest.SupportedContentProfiles),
            "qualityProfile" => BuildSupportedError("--quality-profile", VideoSettingsRequest.SupportedQualityProfiles),
            "nvencPreset" => BuildSupportedError("--nvenc-preset", NvencPresetOptions.SupportedPresets),
            _ => exception.Message
        };
    }

    private static string BuildSupportedError<T>(string optionName, IReadOnlyList<T> supportedValues)
    {
        return $"{optionName} must be one of: {CliValueFormatter.FormatList(supportedValues)}.";
    }

    private sealed class ParseState
    {
        public bool OverlayBackground;
        public bool SynchronizeAudio;
        public bool KeepSource;
        public bool ForceEncode;
        public int? DownscaleTargetHeight;
        public int? MaxFramesPerSecond;
        public int? Cq;
        public decimal? Maxrate;
        public decimal? Bufsize;
        public string? ContentProfile;
        public string? QualityProfile;
        public string? Algorithm;
        public string? NvencPreset;
    }
}
