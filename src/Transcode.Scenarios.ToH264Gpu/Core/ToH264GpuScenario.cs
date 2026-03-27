using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Gpu.Core;

/*
Это прикладной сценарий toh264gpu.
Он решает, когда достаточно remux-only, когда нужно полное NVENC-перекодирование,
и какие узкие ffmpeg-настройки нужны для сохранения legacy-поведения без раздувания общей модели.
*/
/// <summary>
/// Represents the legacy ToH264Gpu use case as an MP4/H.264-first scenario for general-purpose playback on full OS and web-friendly targets.
/// </summary>
public sealed class ToH264GpuScenario : TranscodeScenario
{
    private const double FrameRateTolerance = 0.0001;
    private const double DownscaleFrameRateCap = 30000d / 1001d;
    private const int DefaultAudioBitrateKbps = 192;
    private const int MinAudioBitrateKbps = 48;
    private const int MaxAudioBitrateKbps = 320;
    private static readonly VideoSettingsResolver VideoSettingsResolver = new(VideoSettingsProfiles.Default);
    private static readonly ToH264GpuInfoFormatter InfoFormatter = new();
    private readonly ToH264GpuFfmpegTool _ffmpegTool;

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives.
    /// </summary>
    public ToH264GpuScenario()
        : this(new ToH264GpuRequest(), CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives.
    /// </summary>
    public ToH264GpuScenario(ToH264GpuRequest request)
        : this(request, CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToH264Gpu scenario with scenario-specific directives and a concrete ffmpeg renderer.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToH264Gpu workflow.</param>
    /// <param name="ffmpegTool">Concrete ffmpeg renderer used by this scenario.</param>
    public ToH264GpuScenario(ToH264GpuRequest request, ToH264GpuFfmpegTool ffmpegTool)
        : base("toh264gpu")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _ffmpegTool = ffmpegTool ?? throw new ArgumentNullException(nameof(ffmpegTool));
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToH264Gpu workflow.
    /// </summary>
    public ToH264GpuRequest Request { get; }

    /// <summary>
    /// Builds the resolved toh264gpu decision for the supplied source video.
    /// </summary>
    internal ToH264GpuDecision BuildDecision(SourceVideo video)
    {
        return BuildDecision(video, includeExecutionPayload: true);
    }

    private ToH264GpuDecision BuildDecision(SourceVideo video, bool includeExecutionPayload)
    {
        ArgumentNullException.ThrowIfNull(video);

        var targetContainer = Request.OutputMkv ? "mkv" : "mp4";
        var downscaleRequest = Request.Downscale;
        var useDownscale = downscaleRequest is not null && video.Height > downscaleRequest.TargetHeight;
        var synchronizeAudio = Request.SynchronizeAudio || RequiresAutomaticTimestampRepair(video);
        var videoCopyCompatible = CanCopyVideo(video, useDownscale);
        var copyVideo = videoCopyCompatible;
        var copyAudio = !synchronizeAudio && CanCopyAudio(video);
        AudioIntent audioIntent = copyAudio
            ? new CopyAudioIntent()
            : synchronizeAudio
                ? new SynchronizeAudioIntent()
                : new EncodeAudioIntent();
        var videoSettingsRequest = copyVideo
            ? null
            : Request.VideoSettings;
        var targetFramesPerSecond = copyVideo
            ? (double?)null
            : ResolveTargetFramesPerSecond(video, useDownscale);
        var videoSettings = copyVideo
            ? null
            : includeExecutionPayload
                ? ResolveVideoSettings(video, useDownscale, downscaleRequest, videoSettingsRequest)
                : null;
        var resolvedDownscale = useDownscale
            ? includeExecutionPayload
                ? downscaleRequest?.WithDefaultAlgorithm(
                    videoSettings?.Algorithm ?? throw new InvalidOperationException("Downscale algorithm must be resolved for encode path."))
                : downscaleRequest
            : null;
        VideoIntent videoIntent = copyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                TargetFramesPerSecond: targetFramesPerSecond,
                UseFrameInterpolation: false,
                VideoSettings: videoSettingsRequest,
                Downscale: resolvedDownscale,
                EncoderPreset: Request.NvencPreset);
        var mux = new ToH264GpuDecision.MuxExecution(
            optimizeForFastStart: targetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase),
            mapPrimaryAudioOnly: true);
        var videoExecution = !includeExecutionPayload || copyVideo || videoSettings is null
            ? null
            : BuildVideoExecution(videoSettings, useDownscale);
        var audioExecution = !includeExecutionPayload || copyAudio
            ? null
            : BuildAudioExecution(video, audioIntent);

        return new ToH264GpuDecision(
            targetContainer: targetContainer,
            videoIntent: videoIntent,
            audioIntent: audioIntent,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, targetContainer),
            mux: mux,
            videoExecution: videoExecution,
            audioExecution: audioExecution);
    }

    /// <inheritdoc />
    protected override string FormatInfoCore(SourceVideo video)
    {
        return InfoFormatter.Format(video, BuildDecision(video, includeExecutionPayload: false));
    }

    /// <inheritdoc />
    protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
    {
        return _ffmpegTool.BuildExecution(video, BuildDecision(video));
    }

    private bool CanCopyVideo(SourceVideo video, bool useDownscale)
    {
        if (Request.Denoise || useDownscale)
        {
            return false;
        }

        if (!video.VideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HasVariableFrameRateSignal(video))
        {
            return false;
        }

        return true;
    }

    private bool CanCopyAudio(SourceVideo video)
    {
        if (!video.HasAudio)
        {
            return !video.HasAudio;
        }

        var codec = video.PrimaryAudioCodec;
        return codec is not null &&
               (codec.Equals("aac", StringComparison.OrdinalIgnoreCase) ||
                codec.Equals("mp3", StringComparison.OrdinalIgnoreCase));
    }

    private double ResolveTargetFramesPerSecond(SourceVideo video, bool useDownscale)
    {
        if (useDownscale &&
            !Request.KeepFramesPerSecond &&
            video.FramesPerSecond > 30.0)
        {
            return DownscaleFrameRateCap;
        }

        return video.FramesPerSecond;
    }

    private ToH264GpuDecision.VideoExecution BuildVideoExecution(VideoSettingsDefaults videoSettings, bool useDownscale)
    {
        return new ToH264GpuDecision.VideoExecution(
            useHardwareDecode: useDownscale,
            rateControl: new ToH264GpuDecision.ConstantQualityVideoRateControlExecution(
                cq: videoSettings.Cq,
                maxrateKbps: ToKbps(videoSettings.Maxrate),
                bufferSizeKbps: ToKbps(videoSettings.Bufsize)),
            adaptiveQuantization: new ToH264GpuDecision.AdaptiveQuantizationExecution(rcLookahead: 32),
            filter: useDownscale || !Request.Denoise
                ? null
                : "hqdn3d=1.2:1.2:6:6",
            pixelFormat: useDownscale ? null : "yuv420p");
    }

    private static ToH264GpuDecision.AudioExecution BuildAudioExecution(SourceVideo video, AudioIntent audioIntent)
    {
        var usesAmrAudio = IsAmrNb(video.PrimaryAudioCodec);
        var requiresRepair = audioIntent is SynchronizeAudioIntent or RepairAudioIntent;

        return new ToH264GpuDecision.AudioExecution(
            bitrateKbps: ResolveAudioBitrateKbps(video),
            sampleRate: usesAmrAudio || requiresRepair ? 48000 : null,
            channels: usesAmrAudio ? 1 : requiresRepair ? 2 : null,
            filter: BuildAudioFilter(usesAmrAudio, requiresRepair));
    }

    private static string? BuildAudioFilter(bool usesAmrAudio, bool requiresRepair)
    {
        if (usesAmrAudio)
        {
            return "aresample=48000:async=1:first_pts=0";
        }

        return requiresRepair
            ? "aresample=async=1:first_pts=0"
            : null;
    }

    private VideoSettingsDefaults ResolveVideoSettings(
        SourceVideo video,
        bool useDownscale,
        DownscaleRequest? downscaleRequest,
        VideoSettingsRequest? request)
    {
        var sourceBitrate = ResolveSourceBitrate(video);
        var useFixedBucketQuality = FixedBucketVideoSettingsPolicy.ShouldUseFixedBucketQuality(
            VideoSettingsProfiles.Default,
            useDownscale,
            downscaleRequest,
            video.Height,
            request);
        ProfileDrivenVideoSettingsResolution resolution;

        if (useDownscale)
        {
            resolution = VideoSettingsResolver.ResolveForDownscale(
                downscaleRequest ?? throw new InvalidOperationException("Downscale request is required when downscale mode is active."),
                videoSettings: request,
                sourceHeight: video.Height);
        }
        else
        {
            resolution = VideoSettingsResolver.ResolveForEncode(
                request: request,
                outputHeight: Math.Max(1, video.Height),
                sourceHeight: video.Height);
        }

        return useFixedBucketQuality
            ? FixedBucketVideoSettingsPolicy.ApplySourceBitrateCap(
                resolution.Settings,
                sourceBitrate,
                request,
                resolution.Profile.RateModel.BufsizeMultiplier)
            : resolution.Settings;
    }

    private static int ResolveAudioBitrateKbps(SourceVideo video)
    {
        if (!video.PrimaryAudioBitrate.HasValue || video.PrimaryAudioBitrate.Value <= 0)
        {
            return DefaultAudioBitrateKbps;
        }

        var audioBitrateKbps = (int)Math.Round(video.PrimaryAudioBitrate.Value / 1000.0, MidpointRounding.AwayFromZero);
        return Math.Min(MaxAudioBitrateKbps, Math.Max(MinAudioBitrateKbps, audioBitrateKbps));
    }

    private static int ToKbps(decimal value)
    {
        return (int)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    }

    private static long? ResolveSourceBitrate(SourceVideo video)
    {
        var resolvedMetadataBitrate = SourceVideoBitrateResolver.ResolveVideoBitrateHint(video);
        if (resolvedMetadataBitrate.HasValue && resolvedMetadataBitrate.Value > 0)
        {
            return resolvedMetadataBitrate.Value;
        }

        if (video.Duration <= TimeSpan.FromSeconds(0.1) ||
            string.IsNullOrWhiteSpace(video.FilePath) ||
            !File.Exists(video.FilePath))
        {
            return null;
        }

        var fileSizeBits = new FileInfo(video.FilePath).Length * 8m;
        if (fileSizeBits <= 0m)
        {
            return null;
        }

        var totalBitrate = Math.Round(fileSizeBits / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        return totalBitrate > 0m && totalBitrate <= long.MaxValue
            ? SourceVideoBitrateResolver.ResolveVideoBitrateFromTotal((long)totalBitrate, video)
            : null;
    }

    private static bool RequiresAutomaticTimestampRepair(SourceVideo video)
    {
        if (video.FileExtension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
            video.FileExtension.Equals(".asf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(video.FormatName) &&
               video.FormatName.Contains("asf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVariableFrameRateSignal(SourceVideo video)
    {
        if (!video.RawFramesPerSecond.HasValue || !video.AverageFramesPerSecond.HasValue)
        {
            return false;
        }

        return Math.Abs(video.RawFramesPerSecond.Value - video.AverageFramesPerSecond.Value) > FrameRateTolerance;
    }

    private static bool IsAmrNb(string? codec)
    {
        return codec is not null &&
               (codec.Equals("amr_nb", StringComparison.OrdinalIgnoreCase) ||
                codec.Equals("amrnb", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOutputPath(SourceVideo video, string targetContainer)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.{targetContainer}");
        if (!Request.KeepSource)
        {
            return outputPath;
        }

        var appliedDownscale = Request.Downscale is not null &&
                               video.Height > Request.Downscale.TargetHeight;
        if (appliedDownscale)
        {
            return Path.Combine(directory, $"{FormatKeepSourceDownscaleFileName(video.FileNameWithoutExtension, Request.Downscale!.TargetHeight)}.{targetContainer}");
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.{targetContainer}");
    }

    private static string FormatKeepSourceDownscaleFileName(string fileNameWithoutExtension, int targetHeight)
    {
        var suffix = $"{targetHeight}p";
        if (fileNameWithoutExtension.EndsWith(")", StringComparison.Ordinal) &&
            fileNameWithoutExtension.LastIndexOf('(') >= 0)
        {
            return string.Concat(fileNameWithoutExtension.AsSpan(0, fileNameWithoutExtension.Length - 1), ", ", suffix, ")");
        }

        return $"{fileNameWithoutExtension} ({suffix})";
    }

    private static ToH264GpuFfmpegTool CreateDefaultTool()
    {
        return new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance);
    }
}
