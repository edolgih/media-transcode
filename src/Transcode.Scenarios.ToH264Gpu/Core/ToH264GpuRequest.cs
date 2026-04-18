using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Gpu.Core;

/*
Это scenario request для сценария toh264gpu.
Он хранит только scenario-specific domain-опции и не знает про raw CLI-аргументы.
*/
/// <summary>
/// Captures scenario-specific directives for the legacy ToH264Gpu workflow.
/// </summary>
public sealed class ToH264GpuRequest
{
    private const int MinimumNvdecMaxThreadsValue = 1;
    private const int MaximumNvdecMaxThreadsValue = 32;

    /// <summary>
    /// Gets the minimum allowed NVDEC decode thread limit.
    /// </summary>
    public static int MinimumNvdecMaxThreads => MinimumNvdecMaxThreadsValue;

    /// <summary>
    /// Gets the maximum allowed NVDEC decode thread limit.
    /// </summary>
    public static int MaximumNvdecMaxThreads => MaximumNvdecMaxThreadsValue;

    /*
    Это создание scenario request с набором управляемых опций toh264gpu.
    */
    /// <summary>
    /// Initializes scenario-specific directives for the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest(
        bool keepSource = false,
        bool forceEncode = false,
        DownscaleRequest? downscale = null,
        bool keepFramesPerSecond = false,
        VideoSettingsRequest? videoSettings = null,
        string? nvencPreset = null,
        bool denoise = false,
        bool synchronizeAudio = false,
        bool outputMkv = false,
        int? nvdecMaxThreads = null)
    {
        if (nvdecMaxThreads.HasValue &&
            (nvdecMaxThreads.Value < MinimumNvdecMaxThreadsValue || nvdecMaxThreads.Value > MaximumNvdecMaxThreadsValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nvdecMaxThreads),
                nvdecMaxThreads.Value,
                $"Value must be in range {MinimumNvdecMaxThreadsValue}..{MaximumNvdecMaxThreadsValue}.");
        }

        var resolvedNvencPreset = NvencPreset.ParseOptional(nvencPreset, nameof(nvencPreset));

        KeepSource = keepSource;
        ForceEncode = forceEncode;
        Downscale = downscale;
        KeepFramesPerSecond = keepFramesPerSecond;
        VideoSettings = videoSettings;
        NvencPreset = resolvedNvencPreset ?? NvencPreset.Default;
        Denoise = denoise;
        SynchronizeAudio = synchronizeAudio;
        OutputMkv = outputMkv;
        NvdecMaxThreads = nvdecMaxThreads;
    }

    /*
    Это флаг сохранения исходного файла после выполнения.
    */
    /// <summary>
    /// Gets a value indicating whether the source file should be preserved after execution.
    /// </summary>
    public bool KeepSource { get; }

    /*
    Это флаг принудительного encode-пути даже когда достаточно remux.
    */
    /// <summary>
    /// Gets a value indicating whether remux-compatible sources should still be rebuilt through the encode path.
    /// </summary>
    public bool ForceEncode { get; }

    /*
    Это явный запрос на downscale для итогового видео.
    */
    /// <summary>
    /// Gets explicit downscale intent when the scenario requests resized output.
    /// </summary>
    public DownscaleRequest? Downscale { get; }

    /*
    Это флаг сохранения исходного FPS при downscale.
    */
    /// <summary>
    /// Gets a value indicating whether downscale mode should preserve the source FPS instead of capping it.
    /// </summary>
    public bool KeepFramesPerSecond { get; }

    /*
    Это профильные video-настройки качества для encode-пути.
    */
    /// <summary>
    /// Gets reusable video-settings directives when the scenario requests them.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; }

    /*
    Это выбранный NVENC preset после нормализации и проверок.
    */
    /// <summary>
    /// Gets the normalized NVENC preset used by the scenario.
    /// </summary>
    public NvencPreset NvencPreset { get; }

    /// <summary>
    /// Gets the optional upper limit for NVDEC decode threads.
    /// When <see langword="null"/>, ffmpeg default threading is used.
    /// </summary>
    public int? NvdecMaxThreads { get; }

    /*
    Это флаг включения denoise в обычном encode-режиме.
    */
    /// <summary>
    /// Gets a value indicating whether denoise should be enabled when normal encoding is used.
    /// </summary>
    public bool Denoise { get; }

    /*
    Это принудительный переход в audio sync-safe режим.
    */
    /// <summary>
    /// Gets a value indicating whether the sync-safe repair path was explicitly requested.
    /// </summary>
    public bool SynchronizeAudio { get; }

    /*
    Это выбор контейнера MKV вместо стандартного MP4.
    */
    /// <summary>
    /// Gets a value indicating whether the target container should be MKV instead of MP4.
    /// </summary>
    public bool OutputMkv { get; }
}
