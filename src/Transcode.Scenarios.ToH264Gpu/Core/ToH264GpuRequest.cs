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
        bool outputMkv = false)
    {
        var normalizedNvencPreset = NormalizeName(nvencPreset);
        if (normalizedNvencPreset is not null && !NvencPresetOptions.IsSupportedPreset(normalizedNvencPreset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nvencPreset),
                nvencPreset,
                $"Supported values: {GetSupportedPresetsDisplay()}.");
        }

        if (videoSettings?.Cq is > 51)
        {
            throw new ArgumentOutOfRangeException("cq", videoSettings.Cq.Value, "CQ must be between 1 and 51.");
        }

        KeepSource = keepSource;
        ForceEncode = forceEncode;
        Downscale = downscale;
        KeepFramesPerSecond = keepFramesPerSecond;
        VideoSettings = videoSettings;
        NvencPreset = normalizedNvencPreset ?? NvencPresetOptions.DefaultPreset;
        Denoise = denoise;
        SynchronizeAudio = synchronizeAudio;
        OutputMkv = outputMkv;
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
    public string NvencPreset { get; }

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
