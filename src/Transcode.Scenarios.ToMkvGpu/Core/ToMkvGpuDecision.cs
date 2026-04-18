using Transcode.Core.MediaIntent;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это локальная rich model сценария tomkvgpu.
Она несет уже принятые scenario-local решения и resolved video-settings payload,
чтобы ffmpeg-rendering не ходил обратно в shared промежуточные слои.
*/
/// <summary>
/// Carries the resolved tomkvgpu decision together with video-settings execution details.
/// </summary>
internal sealed class ToMkvGpuDecision
{
    /*
    Это создание полностью нормализованного решения tomkvgpu для конкретного файла.
    */
    /// <summary>
    /// Initializes a fully resolved <c>tomkvgpu</c> decision.
    /// </summary>
    public ToMkvGpuDecision(
        TargetContainer targetContainer,
        VideoIntent video,
        AudioIntent audio,
        bool keepSource,
        string outputPath,
        bool applyOverlayBackground,
        int? nvdecMaxThreads,
        ProfileDrivenVideoSettingsResolution? videoResolution = null,
        ToMkvGpuResolvedSourceBitrate? sourceBitrate = null)
    {
        TargetContainer = targetContainer ?? throw new ArgumentNullException(nameof(targetContainer));
        Video = NormalizeVideoPlan(video);
        Audio = NormalizeAudioPlan(audio);
        KeepSource = keepSource;
        OutputPath = NormalizeOutputPath(outputPath, nameof(outputPath));
        ApplyOverlayBackground = applyOverlayBackground;
        NvdecMaxThreads = NormalizeNvdecMaxThreads(nvdecMaxThreads);
        VideoResolution = videoResolution;
        SourceBitrate = sourceBitrate;
    }

    /*
    Это нормализованный идентификатор целевого контейнера.
    */
    /// <summary>
    /// Gets the normalized target container identifier.
    /// </summary>
    public TargetContainer TargetContainer { get; }

    /*
    Это выбранный сценарий обработки видеопотока.
    */
    /// <summary>
    /// Gets the resolved video intent.
    /// </summary>
    public VideoIntent Video { get; }

    /*
    Это выбранный сценарий обработки аудиопотока.
    */
    /// <summary>
    /// Gets the resolved audio intent.
    /// </summary>
    public AudioIntent Audio { get; }

    /*
    Это флаг сохранения исходного файла после выполнения.
    */
    /// <summary>
    /// Gets a value indicating whether the source file should be kept.
    /// </summary>
    public bool KeepSource { get; }

    /*
    Это итоговый путь выходного файла, рассчитанный сценарием.
    */
    /// <summary>
    /// Gets the final output path chosen by the scenario.
    /// </summary>
    public string OutputPath { get; }

    /*
    Это флаг режима overlay background для encode-пути.
    */
    /// <summary>
    /// Gets a value indicating whether overlay-background mode is enabled.
    /// </summary>
    public bool ApplyOverlayBackground { get; }

    /// <summary>
    /// Gets the optional upper limit for NVDEC decode threads.
    /// When <see langword="null"/>, ffmpeg default threading is used.
    /// </summary>
    public int? NvdecMaxThreads { get; }

    /*
    Это resolved payload video-настроек для ffmpeg encode-рендеринга.
    */
    /// <summary>
    /// Gets profile-driven video-settings resolution details for encode mode.
    /// </summary>
    public ProfileDrivenVideoSettingsResolution? VideoResolution { get; }

    /*
    Это источник и значение bitrate, использованные при резолве профиля.
    */
    /// <summary>
    /// Gets resolved source bitrate metadata used for profile resolution and diagnostics.
    /// </summary>
    public ToMkvGpuResolvedSourceBitrate? SourceBitrate { get; }

    /*
    Это флаг, что видео можно копировать без перекодирования.
    */
    /// <summary>
    /// Gets a value indicating whether the video stream is copied.
    /// </summary>
    public bool CopyVideo => Video is CopyVideoIntent;

    /*
    Это флаг, что аудио копируется без перекодирования.
    */
    /// <summary>
    /// Gets a value indicating whether the audio stream is copied.
    /// </summary>
    public bool CopyAudio => Audio is CopyAudioIntent;

    /*
    Это флаг, что используется sync-safe путь аудио.
    */
    /// <summary>
    /// Gets a value indicating whether the decision uses audio synchronization mode.
    /// </summary>
    public bool SynchronizeAudio => Audio is SynchronizeAudioIntent;

    /*
    Это флаг, что требуется восстановление таймстампов.
    */
    /// <summary>
    /// Gets a value indicating whether timestamp repair is required.
    /// </summary>
    public bool FixTimestamps => Audio is RepairAudioIntent;

    /*
    Это флаг, что видео нужно кодировать.
    */
    /// <summary>
    /// Gets a value indicating whether video encoding is required.
    /// </summary>
    public bool RequiresVideoEncode => !CopyVideo;

    /*
    Это флаг, что аудио нужно кодировать.
    */
    /// <summary>
    /// Gets a value indicating whether audio encoding is required.
    /// </summary>
    public bool RequiresAudioEncode => !CopyAudio;

    private static string NormalizeOutputPath(string? outputPath, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath, paramName);
        return Path.GetFullPath(outputPath.Trim());
    }

    private static VideoIntent NormalizeVideoPlan(VideoIntent video)
    {
        ArgumentNullException.ThrowIfNull(video);
        return video switch
        {
            CopyVideoIntent => video,
            EncodeVideoIntent => video,
            _ => throw new ArgumentException($"Unsupported video plan type '{video.GetType().Name}'.", nameof(video))
        };
    }

    private static AudioIntent NormalizeAudioPlan(AudioIntent audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return audio switch
        {
            CopyAudioIntent => audio,
            SynchronizeAudioIntent => audio,
            RepairAudioIntent => audio,
            EncodeAudioIntent => audio,
            _ => throw new ArgumentException($"Unsupported audio plan type '{audio.GetType().Name}'.", nameof(audio))
        };
    }

    private static int? NormalizeNvdecMaxThreads(int? nvdecMaxThreads)
    {
        if (!nvdecMaxThreads.HasValue)
        {
            return null;
        }

        if (nvdecMaxThreads.Value < ToMkvGpuRequest.MinimumNvdecMaxThreads ||
            nvdecMaxThreads.Value > ToMkvGpuRequest.MaximumNvdecMaxThreads)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nvdecMaxThreads),
                nvdecMaxThreads.Value,
                $"Value must be in range {ToMkvGpuRequest.MinimumNvdecMaxThreads}..{ToMkvGpuRequest.MaximumNvdecMaxThreads}.");
        }

        return nvdecMaxThreads;
    }
}

/*
Это служебная запись о resolved bitrate источника и о том, откуда он получен.
*/
/// <summary>
/// Stores resolved source bitrate and its origin label used by <c>tomkvgpu</c>.
/// </summary>
/// <param name="Bitrate">Resolved bitrate in bits per second.</param>
/// <param name="Origin">Origin label describing how bitrate was resolved.</param>
internal sealed record ToMkvGpuResolvedSourceBitrate(long? Bitrate, string Origin);
