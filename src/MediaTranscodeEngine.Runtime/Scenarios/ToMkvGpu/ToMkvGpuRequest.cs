using System.Globalization;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;

/*
Это request-модель сценария tomkvgpu.
Она хранит только пользовательские указания, специфичные для этого сценария.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToMkvGpu workflow.
/// </summary>
public sealed class ToMkvGpuRequest
{
    private const string DownscaleOptionName = "--downscale";
    private const string KeepSourceOptionName = "--keep-source";
    private const string OverlayBackgroundOptionName = "--overlay-bg";
    private const string MaxFramesPerSecondOptionName = "--max-fps";
    private const string SynchronizeAudioOptionName = "--sync-audio";
    private const string ContentProfileOptionName = "--content-profile";
    private const string QualityProfileOptionName = "--quality-profile";
    private const string AutoSampleModeOptionName = "--autosample-mode";
    private const string DownscaleAlgorithmOptionName = "--downscale-algo";
    private const string CqOptionName = "--cq";
    private const string MaxrateOptionName = "--maxrate";
    private const string BufsizeOptionName = "--bufsize";
    private const string NvencPresetOptionName = "--nvenc-preset";

    /// <summary>
    /// Supported frame-rate cap values exposed by the ToMkvGpu workflow.
    /// </summary>
    public const string SupportedMaxFramesPerSecondDisplay = "50, 40, 30, 24";

    /// <summary>
    /// Initializes scenario-specific directives for the ToMkvGpu workflow.
    /// </summary>
    /// <param name="overlayBackground">Whether background overlay should be applied during encoding.</param>
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    /// <param name="videoSettings">Reusable video-settings directives.</param>
    /// <param name="downscale">Explicit downscale intent when the scenario requests resized output.</param>
    /// <param name="nvencPreset">Explicit NVENC preset override.</param>
    /// <param name="maxFramesPerSecond">Optional frame-rate cap applied only when the source frame rate is higher.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        bool synchronizeAudio = false,
        bool keepSource = false,
        VideoSettingsRequest? videoSettings = null,
        DownscaleRequest? downscale = null,
        string? nvencPreset = null,
        int? maxFramesPerSecond = null)
    {
        if (maxFramesPerSecond.HasValue && !IsSupportedMaxFramesPerSecond(maxFramesPerSecond.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFramesPerSecond),
                maxFramesPerSecond.Value,
                $"Supported values: {SupportedMaxFramesPerSecondDisplay}.");
        }

        OverlayBackground = overlayBackground;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        VideoSettings = videoSettings?.HasValue == true ? videoSettings : null;
        Downscale = downscale;
        NvencPreset = NormalizeName(nvencPreset);
        MaxFramesPerSecond = maxFramesPerSecond;
    }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during encoding.
    /// </summary>
    public bool OverlayBackground { get; }

    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /// <summary>
    /// Gets explicit downscale intent when the scenario requests resized output.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /// <summary>
    /// Gets a value indicating whether the audio sync-safe path should be forced.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets the explicit NVENC preset override.
    /// </summary>
    public string? NvencPreset { get; }

    /// <summary>
    /// Gets the optional frame-rate cap applied only when the source exceeds it.
    /// </summary>
    public int? MaxFramesPerSecond { get; }

    /// <summary>
    /// Determines whether the supplied frame-rate cap is supported by the ToMkvGpu workflow.
    /// </summary>
    public static bool IsSupportedMaxFramesPerSecond(int value)
    {
        return value is 50 or 40 or 30 or 24;
    }

    /// <summary>
    /// Tries to parse raw scenario arguments into a runtime request.
    /// </summary>
    public static bool TryParseArgs(
        IReadOnlyList<string> args,
        out ToMkvGpuRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var overlayBackground = false;
        var synchronizeAudio = false;
        var keepSource = false;
        int? downscaleTargetHeight = null;
        int? maxFramesPerSecond = null;
        int? cq = null;
        decimal? maxrate = null;
        decimal? bufsize = null;
        string? contentProfile = null;
        string? qualityProfile = null;
        string? autoSampleMode = null;
        string? algorithm = null;
        string? nvencPreset = null;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, OverlayBackgroundOptionName, StringComparison.OrdinalIgnoreCase))
            {
                overlayBackground = true;
                continue;
            }

            if (string.Equals(token, SynchronizeAudioOptionName, StringComparison.OrdinalIgnoreCase))
            {
                synchronizeAudio = true;
                continue;
            }

            if (string.Equals(token, DownscaleOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref index, token, "--downscale must be an integer.", out downscaleTargetHeight, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxFramesPerSecondOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref index, token, "--max-fps must be an integer.", out maxFramesPerSecond, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, CqOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadInt(args, ref index, token, "--cq must be an integer.", out cq, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, MaxrateOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadDecimal(args, ref index, token, "--maxrate must be a number.", out maxrate, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, BufsizeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadDecimal(args, ref index, token, "--bufsize must be a number.", out bufsize, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, ContentProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref index, token, out contentProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, QualityProfileOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref index, token, out qualityProfile, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, AutoSampleModeOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref index, token, out autoSampleMode, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, DownscaleAlgorithmOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref index, token, out algorithm, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, NvencPresetOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadString(args, ref index, token, out nvencPreset, out errorText))
                {
                    return false;
                }

                continue;
            }

            errorText = token.StartsWith("-", StringComparison.Ordinal)
                ? $"Unknown option: {token}"
                : $"Unexpected argument: {token}";
            return false;
        }

        if (maxFramesPerSecond.HasValue &&
            !IsSupportedMaxFramesPerSecond(maxFramesPerSecond.Value))
        {
            errorText = $"--max-fps must be one of: {SupportedMaxFramesPerSecondDisplay}.";
            return false;
        }

        if (downscaleTargetHeight is > 0 &&
            !DownscaleRequest.IsSupportedTargetHeight(downscaleTargetHeight.Value))
        {
            errorText = $"--downscale must be one of: {DownscaleRequest.SupportedTargetHeightsDisplay}.";
            return false;
        }

        if (downscaleTargetHeight is null && !string.IsNullOrWhiteSpace(algorithm))
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
                ? new DownscaleRequest(downscaleTargetHeight.Value, algorithm)
                : null;

            request = new ToMkvGpuRequest(
                overlayBackground: overlayBackground,
                synchronizeAudio: synchronizeAudio,
                keepSource: keepSource,
                videoSettings: videoSettingsRequest.HasValue ? videoSettingsRequest : null,
                downscale: downscaleRequest,
                nvencPreset: nvencPreset,
                maxFramesPerSecond: maxFramesPerSecond);

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
                "maxFramesPerSecond" => $"--max-fps must be one of: {SupportedMaxFramesPerSecondDisplay}.",
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
        return TryReadRequiredValue(args, ref index, optionName, out value, out errorText);
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

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
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

        if (!TryReadRequiredValue(args, ref index, optionName, out var token, out errorText))
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

    private static bool TryReadRequiredValue(
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

        value = token;
        index = valueIndex;
        return true;
    }
}
