using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using System.Globalization;

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
        return BuildDecisionForExecution(video);
    }

    private ToH264GpuDecision BuildDecisionForExecution(SourceVideo video)
    {
        return BuildDecisionCore(video, DecisionPayloadMode.Execution);
    }

    private ToH264GpuDecision BuildDecisionForInfo(SourceVideo video)
    {
        return BuildDecisionCore(video, DecisionPayloadMode.Info);
    }

    private ToH264GpuDecision BuildDecisionCore(SourceVideo video, DecisionPayloadMode payloadMode)
    {
        ArgumentNullException.ThrowIfNull(video);
        var includeExecutionPayload = payloadMode == DecisionPayloadMode.Execution;

        var options = ResolveOptions(video);
        var audioIntent = BuildAudioIntent(options.AudioMode);
        var videoSettings = options.CopyVideo
            ? null
            : includeExecutionPayload
                ? ResolveVideoSettings(video, options.UseDownscale, options.Downscale, options.VideoSettings)
                : null;
        var resolvedDownscale = options.Downscale is not null
            ? includeExecutionPayload
                ? options.Downscale.WithDefaultAlgorithm(
                    videoSettings?.Algorithm ?? throw new InvalidOperationException("Downscale algorithm must be resolved for encode path."))
                : options.Downscale
            : null;
        VideoIntent videoIntent = options.CopyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                TargetFramesPerSecond: options.TargetFramesPerSecond,
                UseFrameInterpolation: false,
                VideoSettings: options.VideoSettings,
                Downscale: resolvedDownscale,
                EncoderPreset: options.NvencPreset);
        var mux = new ToH264GpuDecision.MuxExecution(
            optimizeForFastStart: options.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase),
            mapPrimaryAudioOnly: true);
        var videoExecution = !includeExecutionPayload || options.CopyVideo || videoSettings is null
            ? null
            : BuildVideoExecution(videoSettings, options.UseDownscale, options.UseDenoise);
        var audioExecution = !includeExecutionPayload || options.CopyAudio
            ? null
            : BuildAudioExecution(video, audioIntent);

        return new ToH264GpuDecision(
            targetContainer: options.TargetContainer,
            videoIntent: videoIntent,
            audioIntent: audioIntent,
            keepSource: options.KeepSource,
            outputPath: ResolveOutputPath(video, options.TargetContainer, options.KeepSource, options.Downscale),
            mux: mux,
            videoExecution: videoExecution,
            audioExecution: audioExecution);
    }

    /// <inheritdoc />
    protected override string FormatInfoCore(SourceVideo video)
    {
        return InfoFormatter.Format(video, BuildDecisionForInfo(video));
    }

    /// <inheritdoc />
    protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
    {
        return _ffmpegTool.BuildExecution(video, BuildDecisionForExecution(video));
    }

    private enum DecisionPayloadMode
    {
        Info,
        Execution
    }

    private enum AudioPathMode
    {
        Copy,
        Synchronize,
        Encode
    }

    private sealed record ResolvedScenarioOptions(
        string TargetContainer,
        bool KeepSource,
        bool CopyVideo,
        AudioPathMode AudioMode,
        bool UseDownscale,
        DownscaleRequest? Downscale,
        double? TargetFramesPerSecond,
        VideoSettingsRequest? VideoSettings,
        string NvencPreset,
        bool UseDenoise)
    {
        public bool CopyAudio => AudioMode == AudioPathMode.Copy;
    }

    private ResolvedScenarioOptions ResolveOptions(SourceVideo video)
    {
        var targetContainer = Request.OutputMkv ? "mkv" : "mp4";
        var requestedDownscale = Request.Downscale;
        var useDownscale = requestedDownscale is not null && video.Height > requestedDownscale.TargetHeight;
        var copyVideo = CanCopyVideo(video, useDownscale, Request.Denoise);

        return new ResolvedScenarioOptions(
            TargetContainer: targetContainer,
            KeepSource: Request.KeepSource,
            CopyVideo: copyVideo,
            AudioMode: ResolveAudioMode(video),
            UseDownscale: useDownscale,
            Downscale: useDownscale ? requestedDownscale : null,
            TargetFramesPerSecond: copyVideo
                ? null
                : ResolveTargetFramesPerSecond(video, useDownscale, Request.KeepFramesPerSecond),
            VideoSettings: copyVideo ? null : Request.VideoSettings,
            NvencPreset: Request.NvencPreset,
            UseDenoise: Request.Denoise && !useDownscale);
    }

    private AudioPathMode ResolveAudioMode(SourceVideo video)
    {
        if (Request.SynchronizeAudio || RequiresAutomaticTimestampRepair(video))
        {
            return AudioPathMode.Synchronize;
        }

        return CanCopyAudio(video)
            ? AudioPathMode.Copy
            : AudioPathMode.Encode;
    }

    private static AudioIntent BuildAudioIntent(AudioPathMode audioMode)
    {
        return audioMode switch
        {
            AudioPathMode.Copy => new CopyAudioIntent(),
            AudioPathMode.Synchronize => new SynchronizeAudioIntent(),
            AudioPathMode.Encode => new EncodeAudioIntent(),
            _ => throw new ArgumentOutOfRangeException(nameof(audioMode), audioMode, "Unsupported audio path mode.")
        };
    }

    private bool CanCopyVideo(SourceVideo video, bool useDownscale, bool useDenoise)
    {
        if (useDenoise || useDownscale)
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

    private static bool CanCopyAudio(SourceVideo video)
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

    private static double ResolveTargetFramesPerSecond(SourceVideo video, bool useDownscale, bool keepFramesPerSecond)
    {
        if (useDownscale &&
            !keepFramesPerSecond &&
            video.FramesPerSecond > 30.0)
        {
            return DownscaleFrameRateCap;
        }

        return video.FramesPerSecond;
    }

    private static ToH264GpuDecision.VideoExecution BuildVideoExecution(VideoSettingsDefaults videoSettings, bool useDownscale, bool useDenoise)
    {
        return new ToH264GpuDecision.VideoExecution(
            useHardwareDecode: useDownscale,
            rateControl: new ToH264GpuDecision.ConstantQualityVideoRateControlExecution(
                cq: videoSettings.Cq,
                maxrateKbps: ToKbps(videoSettings.Maxrate),
                bufferSizeKbps: ToKbps(videoSettings.Bufsize)),
            adaptiveQuantization: new ToH264GpuDecision.AdaptiveQuantizationExecution(rcLookahead: 32),
            filter: useDownscale || !useDenoise
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
        var sourceBitrate = SourceVideoBitrateResolver.ResolveVideoBitrateHintOrEstimate(video);
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

    private static string ResolveOutputPath(SourceVideo video, string targetContainer, bool keepSource, DownscaleRequest? downscale)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        if (downscale is not null)
        {
            return Path.Combine(directory, $"{FormatKeepSourceDownscaleFileName(video.FileNameWithoutExtension, downscale.TargetHeight)}.{targetContainer}");
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.{targetContainer}");
        if (!keepSource)
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.{targetContainer}");
    }

    private static string FormatKeepSourceDownscaleFileName(string fileNameWithoutExtension, int targetHeight)
    {
        var suffix = $"{targetHeight}p";
        if (TryParseTrailingParenthesizedTokens(fileNameWithoutExtension, out var prefix, out var tokens))
        {
            ReplaceTokenPreservingPosition(tokens, IsHeightSuffixToken, suffix);
            return $"{prefix} ({string.Join(", ", tokens)})";
        }

        return $"{fileNameWithoutExtension} ({suffix})";
    }

    private static void ReplaceTokenPreservingPosition(List<string> tokens, Predicate<string> match, string replacement)
    {
        var insertIndex = tokens.FindIndex(match);
        tokens.RemoveAll(match);
        if (insertIndex < 0 || insertIndex > tokens.Count)
        {
            tokens.Add(replacement);
            return;
        }

        tokens.Insert(insertIndex, replacement);
    }

    private static bool TryParseTrailingParenthesizedTokens(
        string fileNameWithoutExtension,
        out string prefix,
        out List<string> tokens)
    {
        prefix = fileNameWithoutExtension;
        tokens = [];

        if (!fileNameWithoutExtension.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var openParenthesis = fileNameWithoutExtension.LastIndexOf('(');
        if (openParenthesis <= 0)
        {
            return false;
        }

        prefix = fileNameWithoutExtension[..openParenthesis].TrimEnd();
        var tokenPayload = fileNameWithoutExtension.Substring(
            openParenthesis + 1,
            fileNameWithoutExtension.Length - openParenthesis - 2);
        tokens = tokenPayload
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return true;
    }

    private static bool IsHeightSuffixToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.EndsWith("p", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numericPart = token[..^1];
        return int.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
               parsed > 0;
    }

    private static ToH264GpuFfmpegTool CreateDefaultTool()
    {
        return new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance);
    }
}
