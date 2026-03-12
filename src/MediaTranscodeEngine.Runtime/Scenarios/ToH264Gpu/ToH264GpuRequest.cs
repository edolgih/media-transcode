using System.Globalization;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;

/*
Это runtime-request для сценария toh264gpu.
Он хранит scenario-specific опции и сам разбирает raw CLI-аргументы, а вычисление итогового поведения остается внутри сценария.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToH264Gpu workflow and parses raw scenario arguments.
/// </summary>
public sealed class ToH264GpuRequest
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string DownscaleOptionName = "--downscale";
    private const string KeepFpsOptionName = "--keep-fps";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgoOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";
    private const string DenoiseOptionName = "--denoise";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string MkvOptionName = "--mkv";

    /// <summary>
    /// Initializes scenario-specific directives for the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest(
        bool keepSource = false,
        DownscaleRequest? downscale = null,
        bool keepFramesPerSecond = false,
        VideoSettingsRequest? videoSettings = null,
        string? nvencPreset = null,
        bool denoise = false,
        bool synchronizeAudio = false,
        bool outputMkv = false)
    {
        KeepSource = keepSource;
        Downscale = downscale;
        KeepFramesPerSecond = keepFramesPerSecond;
        VideoSettings = videoSettings?.HasValue == true ? videoSettings : null;
        NvencPreset = NormalizeName(nvencPreset);
        Denoise = denoise;
        SynchronizeAudio = synchronizeAudio;
        OutputMkv = outputMkv;
    }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets explicit downscale intent when the scenario requests resized output.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /// <summary>
    /// Gets a value indicating whether downscale mode should preserve the source FPS instead of capping it.
    /// </summary>
    public bool KeepFramesPerSecond { get; }

    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /// <summary>
    /// Gets the explicit NVENC preset override.
    /// </summary>
    public string? NvencPreset { get; }

    /// <summary>
    /// Gets a value indicating whether denoise should be enabled when normal encoding is used.
    /// </summary>
    public bool Denoise { get; }

    /// <summary>
    /// Gets a value indicating whether the sync-safe repair path was explicitly requested.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the target container should be MKV instead of MP4.
    /// </summary>
    public bool OutputMkv { get; }

    /// <summary>
    /// Tries to parse raw scenario arguments into a runtime request.
    /// </summary>
    public static bool TryParseArgs(
        IReadOnlyList<string> args,
        out ToH264GpuRequest request,
        out string? errorText)
    {
        request = null!;
        errorText = null;

        var keepSource = false;
        int? downscaleTargetHeight = null;
        var keepFramesPerSecond = false;
        string? downscaleAlgorithm = null;
        int? cq = null;
        decimal? maxrate = null;
        decimal? bufsize = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? nvencPreset = null;
        var denoise = false;
        var synchronizeAudio = false;
        var outputMkv = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            switch (token)
            {
                case KeepSourceOptionName:
                    keepSource = true;
                    break;

                case DownscaleOptionName:
                    if (!TryReadInt(args, ref index, DownscaleOptionName, $"--downscale must be one of: {DownscaleRequest.SupportedTargetHeightsDisplay}.", out downscaleTargetHeight, out errorText))
                    {
                        return false;
                    }

                    if (downscaleTargetHeight is not null &&
                        !DownscaleRequest.IsSupportedTargetHeight(downscaleTargetHeight.Value))
                    {
                        errorText = $"--downscale must be one of: {DownscaleRequest.SupportedTargetHeightsDisplay}.";
                        return false;
                    }

                    break;

                case KeepFpsOptionName:
                    keepFramesPerSecond = true;
                    break;

                case ContentProfileOptionName:
                    if (!TryReadString(args, ref index, ContentProfileOptionName, out contentProfile, out errorText))
                    {
                        return false;
                    }

                    break;

                case QualityProfileOptionName:
                    if (!TryReadString(args, ref index, QualityProfileOptionName, out qualityProfile, out errorText))
                    {
                        return false;
                    }

                    break;

                case AutoSampleModeOptionName:
                    if (!TryReadString(args, ref index, AutoSampleModeOptionName, out autoSampleMode, out errorText))
                    {
                        return false;
                    }

                    break;

                case DownscaleAlgoOptionName:
                    if (!TryReadString(args, ref index, DownscaleAlgoOptionName, out downscaleAlgorithm, out errorText))
                    {
                        return false;
                    }

                    if (NormalizeName(downscaleAlgorithm) is not ("bicubic" or "lanczos" or "bilinear"))
                    {
                        errorText = "--downscale-algo must be one of: bicubic, lanczos, bilinear.";
                        return false;
                    }

                    break;

                case CqOptionName:
                    if (!TryReadInt(args, ref index, CqOptionName, "--cq must be an integer from 1 to 51.", out cq, out errorText))
                    {
                        return false;
                    }

                    if (!cq.HasValue || cq.Value <= 0 || cq.Value > 51)
                    {
                        errorText = "--cq must be an integer from 1 to 51.";
                        return false;
                    }

                    break;

                case MaxrateOptionName:
                    if (!TryReadDecimal(args, ref index, MaxrateOptionName, "--maxrate must be a number.", out maxrate, out errorText))
                    {
                        return false;
                    }

                    if (!maxrate.HasValue || maxrate.Value <= 0m)
                    {
                        errorText = "--maxrate must be greater than zero.";
                        return false;
                    }

                    break;

                case BufsizeOptionName:
                    if (!TryReadDecimal(args, ref index, BufsizeOptionName, "--bufsize must be a number.", out bufsize, out errorText))
                    {
                        return false;
                    }

                    if (!bufsize.HasValue || bufsize.Value <= 0m)
                    {
                        errorText = "--bufsize must be greater than zero.";
                        return false;
                    }

                    break;

                case NvencPresetOptionName:
                    if (!TryReadString(args, ref index, NvencPresetOptionName, out nvencPreset, out errorText))
                    {
                        return false;
                    }

                    if (NormalizeName(nvencPreset) is not ("p1" or "p2" or "p3" or "p4" or "p5" or "p6" or "p7"))
                    {
                        errorText = "--nvenc-preset must be one of: p1, p2, p3, p4, p5, p6, p7.";
                        return false;
                    }

                    break;

                case DenoiseOptionName:
                    denoise = true;
                    break;

                case SynchronizeAudioOptionName:
                    synchronizeAudio = true;
                    break;

                case MkvOptionName:
                    outputMkv = true;
                    break;

                default:
                    errorText = $"Unexpected argument: {token}";
                    return false;
            }
        }

        if (!downscaleTargetHeight.HasValue && !string.IsNullOrWhiteSpace(downscaleAlgorithm))
        {
            errorText = "--downscale-algo requires --downscale.";
            return false;
        }

        try
        {
            var videoSettingsRequest = new VideoSettingsRequest(
                contentProfile: contentProfile,
                qualityProfile: qualityProfile,
                autoSampleMode: autoSampleMode,
                cq: cq,
                maxrate: maxrate,
                bufsize: bufsize);
            var downscaleRequest = downscaleTargetHeight.HasValue
                ? new DownscaleRequest(downscaleTargetHeight.Value, downscaleAlgorithm)
                : null;

            request = new ToH264GpuRequest(
                keepSource: keepSource,
                downscale: downscaleRequest,
                keepFramesPerSecond: keepFramesPerSecond,
                videoSettings: videoSettingsRequest.HasValue ? videoSettingsRequest : null,
                nvencPreset: nvencPreset,
                denoise: denoise,
                synchronizeAudio: synchronizeAudio,
                outputMkv: outputMkv);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = exception.ParamName switch
            {
                "targetHeight" => exception.ActualValue is int actualHeight && actualHeight > 0
                    ? $"--downscale must be one of: {DownscaleRequest.SupportedTargetHeightsDisplay}."
                    : "--downscale must be greater than zero.",
                "cq" => "--cq must be greater than zero.",
                "maxrate" => "--maxrate must be greater than zero.",
                "bufsize" => "--bufsize must be greater than zero.",
                _ => exception.Message
            };

            return false;
        }
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static bool TryReadString(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Count)
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        var token = args[valueIndex];
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        value = token.Trim().ToLowerInvariant();
        index = valueIndex;
        return true;
    }

    private static bool TryReadInt(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out int? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadString(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryReadDecimal(
        IReadOnlyList<string> args,
        ref int index,
        string optionName,
        string invalidValueError,
        out decimal? value,
        out string? errorText)
    {
        value = null;
        errorText = null;

        if (!TryReadString(args, ref index, optionName, out var token, out errorText))
        {
            return false;
        }

        if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue))
        {
            errorText = invalidValueError;
            return false;
        }

        value = parsedValue;
        return true;
    }
}
