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

    /// <summary>
    /// Gets the minimum allowed NVDEC decode thread limit.
    /// </summary>
    public static int MinimumNvdecMaxThreads => NvdecMaxThreads.Minimum;

    /// <summary>
    /// Gets the maximum allowed NVDEC decode thread limit.
    /// </summary>
    public static int MaximumNvdecMaxThreads => NvdecMaxThreads.Maximum;

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
    /// <param name="nvdecMaxThreads">Upper limit for NVDEC decode threads used in ffmpeg command rendering.</param>
    public ToMkvGpuRequest(
        bool overlayBackground = false,
        bool synchronizeAudio = false,
        bool keepSource = false,
        bool forceEncode = false,
        VideoSettingsRequest? videoSettings = null,
        DownscaleRequest? downscale = null,
        string? nvencPreset = null,
        int? maxFramesPerSecond = null,
        int? nvdecMaxThreads = null)
    {
        if (maxFramesPerSecond.HasValue && !IsSupportedMaxFramesPerSecond(maxFramesPerSecond.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFramesPerSecond),
                maxFramesPerSecond.Value,
                $"Supported values: {GetSupportedMaxFramesPerSecondDisplay()}.");
        }

        var resolvedNvencPreset = NvencPreset.ParseOptional(nvencPreset, nameof(nvencPreset));
        var resolvedNvdecMaxThreads = NvdecMaxThreads.ParseOptional(nvdecMaxThreads, nameof(nvdecMaxThreads));

        OverlayBackground = overlayBackground;
        SynchronizeAudio = synchronizeAudio;
        KeepSource = keepSource;
        ForceEncode = forceEncode;
        VideoSettings = videoSettings;
        Downscale = downscale;
        NvencPreset = resolvedNvencPreset ?? NvencPreset.Default;
        MaxFramesPerSecond = maxFramesPerSecond;
        NvdecMaxThreads = resolvedNvdecMaxThreads;
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
    public NvencPreset NvencPreset { get; }

    /*
    Это ограничение FPS, которое применяется только если исходный FPS выше.
    */
    /// <summary>
    /// Gets the optional frame-rate cap applied only when the source exceeds it.
    /// </summary>
    public int? MaxFramesPerSecond { get; }

    /// <summary>
    /// Gets the optional upper limit for NVDEC decode threads.
    /// When <see langword="null"/>, ffmpeg default threading is used.
    /// </summary>
    public NvdecMaxThreads? NvdecMaxThreads { get; }

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
}
