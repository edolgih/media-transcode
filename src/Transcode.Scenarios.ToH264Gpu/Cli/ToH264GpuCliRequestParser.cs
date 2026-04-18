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
    private const string ForceEncodeOptionName = "--force-encode";
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

    /*
    Это основной вход парсера raw аргументов сценария toh264gpu.
    */
    /// <summary>
    /// Parses raw scenario arguments into a normalized <see cref="ToH264GpuRequest"/>.
    /// </summary>
    /// <param name="args">Scenario-specific CLI arguments.</param>
    /// <param name="request">Parsed scenario request when parsing succeeds.</param>
    /// <param name="errorText">Validation or parsing error message.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
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

    /*
    Это обработка одного токена и обновление промежуточного состояния.
    */
    /// <summary>
    /// Processes a single CLI token and updates parser state.
    /// </summary>
    /// <param name="args">Scenario-specific CLI arguments.</param>
    /// <param name="index">Current token index, advanced for value-taking options.</param>
    /// <param name="token">Current token value.</param>
    /// <param name="state">Mutable parse state.</param>
    /// <param name="errorText">Validation or parsing error message.</param>
    /// <returns><see langword="true"/> when the token is valid; otherwise <see langword="false"/>.</returns>
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
                    $"--cq must be an integer from {VideoSettingsRequest.MinimumCq} to {VideoSettingsRequest.MaximumCq}.",
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

    /*
    Это финальная сборка domain request из parse-состояния.
    */
    /// <summary>
    /// Creates the scenario request from validated parse state.
    /// </summary>
    /// <param name="state">Validated parse state.</param>
    /// <param name="request">Scenario request when creation succeeds.</param>
    /// <param name="errorText">Creation error message.</param>
    /// <returns><see langword="true"/> when request creation succeeds; otherwise <see langword="false"/>.</returns>
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
                forceEncode: state.ForceEncode,
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

    /*
    Это перевод out-of-range ошибок доменной валидации в понятные CLI-тексты.
    */
    /// <summary>
    /// Maps domain out-of-range errors to user-facing CLI validation messages.
    /// </summary>
    /// <param name="exception">Validation exception raised while creating the request.</param>
    /// <returns>CLI-friendly validation message.</returns>
    private static string MapOutOfRangeError(ArgumentOutOfRangeException exception)
    {
        return exception.ParamName switch
        {
            "targetHeight" => exception.ActualValue is int actualHeight && actualHeight > 0
                ? BuildSupportedError("--downscale", DownscaleRequest.SupportedTargetHeights)
                : "--downscale must be greater than zero.",
            "algorithm" => BuildSupportedError("--downscale-algo", DownscaleRequest.SupportedAlgorithms),
            "cq" => $"--cq must be an integer from {VideoSettingsRequest.MinimumCq} to {VideoSettingsRequest.MaximumCq}.",
            "maxrate" => "--maxrate must be greater than zero.",
            "bufsize" => "--bufsize must be greater than zero.",
            "contentProfile" => BuildSupportedError("--content-profile", VideoSettingsRequest.SupportedContentProfiles),
            "qualityProfile" => BuildSupportedError("--quality-profile", VideoSettingsRequest.SupportedQualityProfiles),
            "nvencPreset" => BuildSupportedError("--nvenc-preset", NvencPreset.SupportedValues),
            _ => exception.Message
        };
    }

    /*
    Это helper для единообразного сообщения о поддерживаемых значениях опций.
    */
    /// <summary>
    /// Builds a standard "supported values" error for CLI options.
    /// </summary>
    /// <typeparam name="T">Supported value type.</typeparam>
    /// <param name="optionName">Option name shown in CLI output.</param>
    /// <param name="supportedValues">Supported values for the option.</param>
    /// <returns>Formatted validation error text.</returns>
    private static string BuildSupportedError<T>(string optionName, IReadOnlyList<T> supportedValues)
    {
        return $"{optionName} must be one of: {CliValueFormatter.FormatList(supportedValues)}.";
    }

    /*
    Это внутреннее mutable-состояние парсинга до создания финального request.
    */
    /// <summary>
    /// Holds intermediate option values while CLI tokens are being parsed.
    /// </summary>
    private sealed class ParseState
    {
        public bool KeepSource;
        public bool ForceEncode;
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
