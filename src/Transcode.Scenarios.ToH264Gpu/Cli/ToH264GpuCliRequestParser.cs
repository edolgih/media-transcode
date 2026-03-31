using Transcode.Cli.Core.Parsing;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Scenarios.ToH264Gpu.Cli;

/*
Это scenario-local parser для toh264gpu.
Он знает raw CLI option names и переводит их в scenario request без argv-знания в Core.
*/
/// <summary>
/// Parses ToH264Gpu CLI tokens into a scenario request while keeping raw option names in the CLI layer.
/// </summary>
internal static class ToH264GpuCliRequestParser
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string DownscaleOptionName = "--downscale";
    private const string KeepFpsOptionName = "--keep-fps";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string DownscaleAlgoOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";
    private const string DenoiseOptionName = "--denoise";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string MkvOptionName = "--mkv";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToH264GpuRequest request,
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
            case DownscaleOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    BuildSupportedError("--downscale", DownscaleRequest.SupportedTargetHeights),
                    out state.DownscaleTargetHeight,
                    out errorText))
                {
                    return false;
                }

                return true;
            case KeepFpsOptionName:
                state.KeepFramesPerSecond = true;
                errorText = null;
                return true;
            case ContentProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.ContentProfile, out errorText);
            case QualityProfileOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.QualityProfile, out errorText);
            case DownscaleAlgoOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.DownscaleAlgorithm, out errorText);
            case CqOptionName:
                if (!CliOptionReader.TryReadInt(
                    args,
                    ref index,
                    token,
                    "--cq must be an integer from 1 to 51.",
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
            case NvencPresetOptionName:
                return CliOptionReader.TryReadRequiredValue(args, ref index, token, out state.NvencPreset, out errorText);
            case DenoiseOptionName:
                state.Denoise = true;
                errorText = null;
                return true;
            case SynchronizeAudioOptionName:
                state.SynchronizeAudio = true;
                errorText = null;
                return true;
            case MkvOptionName:
                state.OutputMkv = true;
                errorText = null;
                return true;
            default:
                errorText = $"Unexpected argument: {token}";
                return false;
        }
    }

    private static bool ValidateOptionCombinations(ParseState state, out string? errorText)
    {
        errorText = null;

        if (!state.DownscaleTargetHeight.HasValue && !string.IsNullOrWhiteSpace(state.DownscaleAlgorithm))
        {
            errorText = "--downscale-algo requires --downscale.";
            return false;
        }

        return true;
    }

    private static bool TryCreateRequest(ParseState state, out ToH264GpuRequest request, out string? errorText)
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
                ? new DownscaleRequest(state.DownscaleTargetHeight.Value, state.DownscaleAlgorithm)
                : null;

            request = new ToH264GpuRequest(
                keepSource: state.KeepSource,
                downscale: downscaleRequest,
                keepFramesPerSecond: state.KeepFramesPerSecond,
                videoSettings: videoSettingsRequest,
                nvencPreset: state.NvencPreset,
                denoise: state.Denoise,
                synchronizeAudio: state.SynchronizeAudio,
                outputMkv: state.OutputMkv);
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
            "cq" => "--cq must be an integer from 1 to 51.",
            "maxrate" => "--maxrate must be greater than zero.",
            "bufsize" => "--bufsize must be greater than zero.",
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
        public bool KeepSource;
        public int? DownscaleTargetHeight;
        public bool KeepFramesPerSecond;
        public string? DownscaleAlgorithm;
        public int? Cq;
        public decimal? Maxrate;
        public decimal? Bufsize;
        public string? ContentProfile;
        public string? QualityProfile;
        public string? NvencPreset;
        public bool Denoise;
        public bool SynchronizeAudio;
        public bool OutputMkv;
    }
}
