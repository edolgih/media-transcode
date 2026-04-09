using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это request-модель сценария tomkvgpu.
Она хранит только scenario-specific domain-указания и не знает про raw CLI-аргументы.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToMkvGpu workflow.
/// </summary>
public sealed class ToMkvGpuRequest
{
    private static readonly int[] SupportedMaxFramesPerSecondValues = [50, 40, 30, 24];

    /*
    Это список поддерживаемых лимитов FPS для томквгпу.
    */
    /// <summary>
    /// Gets frame-rate cap values supported by the ToMkvGpu workflow.
    /// </summary>
    public static IReadOnlyList<int> SupportedMaxFramesPerSecond => SupportedMaxFramesPerSecondValues;

    /*
    Это создание scenario request с набором управляемых опций tomkvgpu.
    */
    /// <summary>
    /// Initializes scenario-specific directives for the ToMkvGpu workflow.
    /// </summary>
    /// <param name="overlayBackground">Whether background overlay should be applied during encoding.</param>
    /// <param name="synchronizeAudio">Whether the audio sync-safe path should be forced.</param>
    /// <param name="keepSource">Whether the source file should be preserved after execution.</param>
    /// <param name="forceEncode">Whether remux-compatible sources should still use the encode path at source resolution.</param>
    /// <param name="videoSettings">Reusable video-settings directives.</param>
    /// <param name="downscale">Explicit downscale intent when the scenario requests resized output.</param>
    /// <param name="nvencPreset">Explicit NVENC preset override.</param>
    /// <param name="maxFramesPerSecond">Optional frame-rate cap applied only when the source frame rate is higher.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        bool synchronizeAudio = false,
        bool keepSource = false,
        bool forceEncode = false,
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
                $"Supported values: {GetSupportedMaxFramesPerSecondDisplay()}.");
        }

        var normalizedNvencPreset = NormalizeName(nvencPreset);
        if (normalizedNvencPreset is not null && !NvencPresetOptions.IsSupportedPreset(normalizedNvencPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nvencPreset),
                nvencPreset,
                $"Supported values: {GetSupportedPresetsDisplay()}.");
        }

        OverlayBackground = overlayBackground;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        ForceEncode = forceEncode;
        VideoSettings = videoSettings;
        Downscale = downscale;
        NvencPreset = normalizedNvencPreset ?? NvencPresetOptions.DefaultPreset;
        MaxFramesPerSecond = maxFramesPerSecond;
    }

    /*
    Это флаг применения overlay background на видео при encode.
    */
    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during encoding.
    /// </summary>
    public bool OverlayBackground { get; }

    /*
    Это флаг принудительного encode-пути даже когда возможен remux.
    */
    /// <summary>
    /// Gets a value indicating whether remux-compatible sources should still be rebuilt through the encode path.
    /// </summary>
    public bool ForceEncode { get; }

    /*
    Это профильные video-настройки качества для encode-пути.
    */
    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /*
    Это явный запрос на downscale для итогового видео.
    */
    /// <summary>
    /// Gets explicit downscale intent when the scenario requests resized output.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /*
    Это принудительный переход в audio sync-safe режим.
    */
    /// <summary>
    /// Gets a value indicating whether the audio sync-safe path should be forced.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /*
    Это флаг сохранения исходного файла после выполнения.
    */
    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /*
    Это выбранный NVENC preset после нормализации и проверок.
    */
    /// <summary>
    /// Gets the normalized NVENC preset used by the scenario.
    /// </summary>
    public string NvencPreset { get; }

    /*
    Это ограничение FPS, которое применяется только если исходный FPS выше.
    */
    /// <summary>
    /// Gets the optional frame-rate cap applied only when the source exceeds it.
    /// </summary>
    public int? MaxFramesPerSecond { get; }

    /*
    Это проверка, поддерживается ли переданный лимит FPS сценарием.
    */
    /// <summary>
    /// Determines whether the supplied frame-rate cap is supported by the ToMkvGpu workflow.
    /// </summary>
    public static bool IsSupportedMaxFramesPerSecond(int value)
    {
        return Array.IndexOf(SupportedMaxFramesPerSecondValues, value) >= 0;
    }

    private static string GetSupportedMaxFramesPerSecondDisplay()
    {
        return string.Join(", ", SupportedMaxFramesPerSecondValues);
    }

    private static string GetSupportedPresetsDisplay()
    {
        return string.Join(", ", NvencPresetOptions.SupportedPresets);
    }

    private static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }
}
