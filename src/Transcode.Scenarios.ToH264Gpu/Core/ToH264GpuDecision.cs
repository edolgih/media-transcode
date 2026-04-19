using Transcode.Core.MediaIntent;
using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Scenarios.ToH264Gpu.Core;

/*
Это локальная rich model сценария toh264gpu.
Она держит уже принятые scenario-local решения: container, video/audio path,
output layout и concrete ffmpeg execution details.
*/
/// <summary>
/// Carries the resolved toh264gpu decision together with concrete ffmpeg execution details.
/// </summary>
internal sealed class ToH264GpuDecision
{
    /*
    Это создание полностью нормализованного решения toh264gpu для конкретного файла.
    */
    /// <summary>
    /// Initializes a fully resolved toh264gpu decision.
    /// </summary>
    public ToH264GpuDecision(
        TargetContainer targetContainer,
        VideoIntent videoIntent,
        AudioIntent audioIntent,
        bool keepSource,
        string outputPath,
        MuxExecution mux,
        VideoExecution? videoExecution = null,
        AudioExecution? audioExecution = null)
    {
        TargetContainer = targetContainer ?? throw new ArgumentNullException(nameof(targetContainer));
        Video = NormalizeVideoPlan(videoIntent);
        Audio = NormalizeAudioPlan(audioIntent);
        KeepSource = keepSource;
        OutputPath = NormalizeOutputPath(outputPath, nameof(outputPath));
        Mux = mux ?? throw new ArgumentNullException(nameof(mux));
        VideoExecutionDetails = videoExecution;
        AudioExecutionDetails = audioExecution;
    }

    /*
    Это нормализованный идентификатор целевого контейнера.
    */
    /// <summary>
    /// Gets the normalized target container identifier.
    /// </summary>
    public TargetContainer TargetContainer { get; }

    /*
    Это выбранный путь обработки видеопотока.
    */
    /// <summary>
    /// Gets the resolved video path.
    /// </summary>
    public VideoIntent Video { get; }

    /*
    Это выбранный путь обработки аудиопотока.
    */
    /// <summary>
    /// Gets the resolved audio path.
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
    Это параметры mux-части исполнения.
    */
    /// <summary>
    /// Gets mux-related execution details.
    /// </summary>
    public MuxExecution Mux { get; }

    /*
    Это детальные video encode-настройки, когда нужен encode.
    */
    /// <summary>
    /// Gets normalized video execution details when video encoding is required.
    /// </summary>
    public VideoExecution? VideoExecutionDetails { get; }

    /*
    Это детальные audio encode-настройки, когда нужен encode.
    */
    /// <summary>
    /// Gets normalized audio execution details when audio encoding is required.
    /// </summary>
    public AudioExecution? AudioExecutionDetails { get; }

    /*
    Это флаг copy-пути для видео.
    */
    /// <summary>
    /// Gets a value indicating whether the video stream should be copied.
    /// </summary>
    public bool CopyVideo => Video is CopyVideoIntent;

    /*
    Это флаг copy-пути для аудио.
    */
    /// <summary>
    /// Gets a value indicating whether the audio path copies compatible source streams.
    /// </summary>
    public bool CopyAudio => Audio is CopyAudioIntent;

    /*
    Это флаг использования sync-safe пути для аудио.
    */
    /// <summary>
    /// Gets a value indicating whether the decision uses the sync-safe audio path.
    /// </summary>
    public bool SynchronizeAudio => Audio is SynchronizeAudioIntent;

    /*
    Это флаг необходимости починки таймстампов.
    */
    /// <summary>
    /// Gets a value indicating whether timestamp normalization is required.
    /// </summary>
    public bool FixTimestamps => Audio is RepairAudioIntent;

    /*
    Это флаг, что видеопоток требуется перекодировать.
    */
    /// <summary>
    /// Gets a value indicating whether the decision requires video encoding.
    /// </summary>
    public bool RequiresVideoEncode => !CopyVideo;

    /*
    Это флаг, что аудиопоток требуется перекодировать.
    */
    /// <summary>
    /// Gets a value indicating whether the decision requires audio encoding.
    /// </summary>
    public bool RequiresAudioEncode => !CopyAudio;

    /*
    Это подмодель mux-поведения контейнера.
    */
    /// <summary>
    /// Represents normalized mux details.
    /// </summary>
    public sealed class MuxExecution
    {
        /*
        Это создание параметров mux-этапа.
        */
        /// <summary>
        /// Initializes mux-related execution details.
        /// </summary>
        public MuxExecution(
            bool optimizeForFastStart = false,
            bool mapPrimaryAudioOnly = false)
        {
            OptimizeForFastStart = optimizeForFastStart;
            MapPrimaryAudioOnly = mapPrimaryAudioOnly;
        }

        /*
        Это флаг fast-start оптимизации контейнера.
        */
        /// <summary>
        /// Gets a value indicating whether the output container should be optimized for progressive playback.
        /// </summary>
        public bool OptimizeForFastStart { get; }

        /*
        Это флаг маппинга только основной аудиодорожки.
        */
        /// <summary>
        /// Gets a value indicating whether only the primary audio stream should be mapped.
        /// </summary>
        public bool MapPrimaryAudioOnly { get; }
    }

    /*
    Это подмодель video encode-параметров.
    */
    /// <summary>
    /// Represents normalized video execution details.
    /// </summary>
    public sealed class VideoExecution
    {
        /*
        Это создание параметров video encode-этапа.
        */
        /// <summary>
        /// Initializes video execution details.
        /// </summary>
        public VideoExecution(
            bool useHardwareDecode,
            VideoRateControlExecution rateControl,
            AdaptiveQuantizationExecution? adaptiveQuantization = null,
            NvdecMaxThreads? nvdecMaxThreads = null,
            string? filter = null,
            string? pixelFormat = null)
        {
            RateControl = rateControl ?? throw new ArgumentNullException(nameof(rateControl));
            UseHardwareDecode = useHardwareDecode;
            AdaptiveQuantization = adaptiveQuantization;
            NvdecMaxThreads = useHardwareDecode ? nvdecMaxThreads : null;
            Filter = NormalizeOptionalText(filter);
            PixelFormat = NormalizeOptionalText(pixelFormat);
        }

        /*
        Это признак включения hardware decode.
        */
        /// <summary>
        /// Gets a value indicating whether hardware decode should be enabled.
        /// </summary>
        public bool UseHardwareDecode { get; }

        /// <summary>
        /// Gets the optional upper limit for NVDEC decode threads.
        /// When <see langword="null"/>, ffmpeg default threading is used.
        /// </summary>
        public NvdecMaxThreads? NvdecMaxThreads { get; }

        /*
        Это выбранная модель управления video bitrate/quality.
        */
        /// <summary>
        /// Gets normalized rate-control details.
        /// </summary>
        public VideoRateControlExecution RateControl { get; }

        /*
        Это настройки adaptive quantization при их наличии.
        */
        /// <summary>
        /// Gets normalized adaptive-quantization details when AQ is enabled.
        /// </summary>
        public AdaptiveQuantizationExecution? AdaptiveQuantization { get; }

        /*
        Это выражение video-фильтра ffmpeg.
        */
        /// <summary>
        /// Gets the plain ffmpeg video filter expression when one is required.
        /// </summary>
        public string? Filter { get; }

        /*
        Это явный pixel format токен для ffmpeg.
        */
        /// <summary>
        /// Gets the explicit pixel format token when one is required.
        /// </summary>
        public string? PixelFormat { get; }
    }

    /*
    Это базовый тип rate-control модели для видео.
    */
    /// <summary>
    /// Represents normalized video rate-control details.
    /// </summary>
    public abstract class VideoRateControlExecution
    {
    }

    /*
    Это VBR-представление параметров video rate-control.
    */
    /// <summary>
    /// Represents normalized VBR details.
    /// </summary>
    public sealed class VariableBitrateVideoRateControlExecution : VideoRateControlExecution
    {
        /*
        Это создание VBR-параметров.
        */
        /// <summary>
        /// Initializes VBR details.
        /// </summary>
        public VariableBitrateVideoRateControlExecution(
            int bitrateKbps,
            int maxrateKbps,
            int bufferSizeKbps)
        {
            BitrateKbps = NormalizePositiveInt(bitrateKbps, nameof(bitrateKbps));
            MaxrateKbps = NormalizePositiveInt(maxrateKbps, nameof(maxrateKbps));
            BufferSizeKbps = NormalizePositiveInt(bufferSizeKbps, nameof(bufferSizeKbps));
        }

        /*
        Это целевой bitrate VBR в Kbps.
        */
        /// <summary>
        /// Gets the target bitrate in kilobits per second.
        /// </summary>
        public int BitrateKbps { get; }

        /*
        Это целевой maxrate VBR в Kbps.
        */
        /// <summary>
        /// Gets the target maxrate in kilobits per second.
        /// </summary>
        public int MaxrateKbps { get; }

        /*
        Это целевой VBV-буфер в Kbps.
        */
        /// <summary>
        /// Gets the target buffer size in kilobits per second.
        /// </summary>
        public int BufferSizeKbps { get; }
    }

    /*
    Это CQ-представление параметров video rate-control.
    */
    /// <summary>
    /// Represents normalized CQ details.
    /// </summary>
    public sealed class ConstantQualityVideoRateControlExecution : VideoRateControlExecution
    {
        /*
        Это создание CQ-параметров.
        */
        /// <summary>
        /// Initializes CQ details.
        /// </summary>
        public ConstantQualityVideoRateControlExecution(
            int cq,
            int? maxrateKbps = null,
            int? bufferSizeKbps = null)
        {
            if (maxrateKbps.HasValue != bufferSizeKbps.HasValue)
            {
                throw new ArgumentException("CQ maxrate and buffer size must either both be specified or both be omitted.");
            }

            Cq = NormalizePositiveInt(cq, nameof(cq));
            MaxrateKbps = NormalizeOptionalPositiveInt(maxrateKbps, nameof(maxrateKbps));
            BufferSizeKbps = NormalizeOptionalPositiveInt(bufferSizeKbps, nameof(bufferSizeKbps));
        }

        /*
        Это целевое CQ-значение.
        */
        /// <summary>
        /// Gets the CQ value.
        /// </summary>
        public int Cq { get; }

        /*
        Это верхняя граница maxrate для bounded-CQ.
        */
        /// <summary>
        /// Gets the target maxrate in kilobits per second when bounded CQ mode is used.
        /// </summary>
        public int? MaxrateKbps { get; }

        /*
        Это размер VBV-буфера для bounded-CQ.
        */
        /// <summary>
        /// Gets the target buffer size in kilobits per second when bounded CQ mode is used.
        /// </summary>
        public int? BufferSizeKbps { get; }
    }

    /*
    Это подмодель adaptive quantization для encode-пути.
    */
    /// <summary>
    /// Represents normalized adaptive-quantization details.
    /// </summary>
    public sealed class AdaptiveQuantizationExecution
    {
        /*
        Это создание параметров adaptive quantization.
        */
        /// <summary>
        /// Initializes adaptive-quantization details.
        /// </summary>
        public AdaptiveQuantizationExecution(
            int rcLookahead,
            int? strength = null)
        {
            RcLookahead = NormalizePositiveInt(rcLookahead, nameof(rcLookahead));
            Strength = NormalizeOptionalPositiveInt(strength, nameof(strength));
        }

        /*
        Это размер окна lookahead для AQ.
        */
        /// <summary>
        /// Gets the lookahead window size.
        /// </summary>
        public int RcLookahead { get; }

        /*
        Это явная сила AQ, если задана.
        */
        /// <summary>
        /// Gets the explicit AQ strength when one is required.
        /// </summary>
        public int? Strength { get; }
    }

    /*
    Это подмодель audio encode-параметров.
    */
    /// <summary>
    /// Represents normalized audio execution details.
    /// </summary>
    public sealed class AudioExecution
    {
        /*
        Это создание параметров audio encode-этапа.
        */
        /// <summary>
        /// Initializes audio execution details.
        /// </summary>
        public AudioExecution(
            int bitrateKbps,
            int? sampleRate = null,
            int? channels = null,
            string? filter = null)
        {
            BitrateKbps = NormalizePositiveInt(bitrateKbps, nameof(bitrateKbps));
            SampleRate = NormalizeOptionalPositiveInt(sampleRate, nameof(sampleRate));
            Channels = NormalizeOptionalPositiveInt(channels, nameof(channels));
            Filter = NormalizeOptionalText(filter);
        }

        /*
        Это целевой bitrate аудио в Kbps.
        */
        /// <summary>
        /// Gets the target bitrate in kilobits per second.
        /// </summary>
        public int BitrateKbps { get; }

        /*
        Это явный sample rate аудио.
        */
        /// <summary>
        /// Gets the explicit sample rate when one is required.
        /// </summary>
        public int? SampleRate { get; }

        /*
        Это явное число аудиоканалов.
        */
        /// <summary>
        /// Gets the explicit channel count when one is required.
        /// </summary>
        public int? Channels { get; }

        /*
        Это выражение audio-фильтра ffmpeg.
        */
        /// <summary>
        /// Gets the plain ffmpeg audio filter expression when one is required.
        /// </summary>
        public string? Filter { get; }
    }

    private static int NormalizePositiveInt(int value, string paramName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(paramName, value, "Value must be greater than zero.");
    }

    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizePositiveInt(value.Value, paramName);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

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
}
