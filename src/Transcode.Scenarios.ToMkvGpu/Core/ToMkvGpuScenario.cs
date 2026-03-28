using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.Failures;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using System.Globalization;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это прикладной сценарий tomkvgpu.
Он решает, достаточно ли remux в mkv, или нужно строить план GPU-кодирования в H.264/H.265.
*/
/// <summary>
/// Represents the legacy ToMkvGpu use case as an MKV-first compatibility scenario for conservative TV-style playback targets.
/// </summary>
public sealed class ToMkvGpuScenario : TranscodeScenario
{
    private static readonly HashSet<string> VideoCopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private static readonly HashSet<string> TimestampSensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wmv",
        ".asf"
    };

    private readonly VideoSettingsProfiles _videoSettingsProfiles;
    private readonly VideoSettingsResolver _videoSettingsResolver;
    private readonly ToMkvGpuFfmpegTool _ffmpegTool;
    private static readonly ToMkvGpuInfoFormatter InfoFormatter = new();

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    public ToMkvGpuScenario()
        : this(new ToMkvGpuRequest(), VideoSettingsProfiles.Default, CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request)
        : this(request, VideoSettingsProfiles.Default, CreateDefaultTool())
    {
    }

    /// <summary>
    /// Initializes a ToMkvGpu scenario with scenario-specific directives and a concrete ffmpeg renderer.
    /// </summary>
    /// <param name="request">Scenario-specific directives for the ToMkvGpu workflow.</param>
    /// <param name="ffmpegTool">Concrete ffmpeg renderer used by this scenario.</param>
    public ToMkvGpuScenario(ToMkvGpuRequest request, ToMkvGpuFfmpegTool ffmpegTool)
        : this(request, VideoSettingsProfiles.Default, ffmpegTool)
    {
    }

    internal ToMkvGpuScenario(ToMkvGpuRequest request, VideoSettingsProfiles videoSettingsProfiles)
        : this(request, videoSettingsProfiles, CreateDefaultTool())
    {
    }

    internal ToMkvGpuScenario(
        ToMkvGpuRequest request,
        VideoSettingsProfiles videoSettingsProfiles,
        ToMkvGpuFfmpegTool ffmpegTool)
        : base("tomkvgpu")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _videoSettingsProfiles = videoSettingsProfiles ?? throw new ArgumentNullException(nameof(videoSettingsProfiles));
        _videoSettingsResolver = new VideoSettingsResolver(_videoSettingsProfiles);
        _ffmpegTool = ffmpegTool ?? throw new ArgumentNullException(nameof(ffmpegTool));
    }

    /// <summary>
    /// Gets the scenario-specific directives carried by the ToMkvGpu workflow.
    /// </summary>
    public ToMkvGpuRequest Request { get; }

    /// <summary>
    /// Builds the resolved tomkvgpu decision from the supplied source video.
    /// </summary>
    /// <param name="video">Source video facts used by the scenario.</param>
    internal ToMkvGpuDecision BuildDecision(SourceVideo video)
    {
        return BuildDecision(video, includeExecutionPayload: true);
    }

    private ToMkvGpuDecision BuildDecision(SourceVideo video, bool includeExecutionPayload)
    {
        ArgumentNullException.ThrowIfNull(video);

        var applyDownscale = Request.Downscale is not null &&
                             video.Height > Request.Downscale.TargetHeight;
        ValidateDownscale(video, applyDownscale);

        var applyFrameRateCap = Request.MaxFramesPerSecond.HasValue &&
                                video.FramesPerSecond > Request.MaxFramesPerSecond.Value;
        var requiresTimestampFix = TimestampSensitiveExtensions.Contains(video.FileExtension);
        var copyVideo = VideoCopyCodecs.Contains(video.VideoCodec) &&
                        !requiresTimestampFix &&
                        !Request.OverlayBackground &&
                        !applyDownscale &&
                        !applyFrameRateCap;
        var copyAudio = !Request.SynchronizeAudio &&
                        copyVideo &&
                        AreAudioStreamsCopyCompatible(video.AudioCodecs);
        AudioIntent audioIntent = copyAudio
            ? new CopyAudioIntent()
            : Request.SynchronizeAudio
                ? new SynchronizeAudioIntent()
                : requiresTimestampFix
                    ? new RepairAudioIntent()
                    : new EncodeAudioIntent();
        VideoIntent videoIntent = copyVideo
            ? new CopyVideoIntent()
            : new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                TargetFramesPerSecond: applyFrameRateCap ? Request.MaxFramesPerSecond : null,
                UseFrameInterpolation: false,
                VideoSettings: Request.VideoSettings,
                Downscale: applyDownscale ? Request.Downscale : null,
                EncoderPreset: Request.NvencPreset);

        ProfileDrivenVideoSettingsResolution? videoResolution = null;
        ToMkvGpuResolvedSourceBitrate? sourceBitrate = null;
        if (includeExecutionPayload && videoIntent is EncodeVideoIntent encodeVideo)
        {
            var outputHeight = ResolveOutputHeight(video, videoIntent, Request.OverlayBackground, encodeVideo.Downscale);
            sourceBitrate = ResolveSourceBitrate(video);
            var useFixedBucketQuality = FixedBucketVideoSettingsPolicy.ShouldUseFixedBucketQuality(
                _videoSettingsProfiles,
                encodeVideo.Downscale is not null,
                encodeVideo.Downscale,
                outputHeight,
                encodeVideo.VideoSettings);
            videoResolution = encodeVideo.Downscale is not null
                ? _videoSettingsResolver.ResolveForDownscale(
                    request: encodeVideo.Downscale,
                    videoSettings: encodeVideo.VideoSettings,
                    sourceHeight: video.Height)
                : _videoSettingsResolver.ResolveForEncode(
                    request: encodeVideo.VideoSettings,
                    outputHeight: outputHeight,
                    sourceHeight: video.Height);

            if (useFixedBucketQuality)
            {
                videoResolution = videoResolution with
                {
                    Settings = FixedBucketVideoSettingsPolicy.ApplySourceBitrateCap(
                        videoResolution.Settings,
                        sourceBitrate.Bitrate,
                        encodeVideo.VideoSettings,
                        videoResolution.Profile.RateModel.BufsizeMultiplier)
                };
            }
        }

        return new ToMkvGpuDecision(
            targetContainer: "mkv",
            video: videoIntent,
            audio: audioIntent,
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, copyVideo, copyAudio),
            applyOverlayBackground: Request.OverlayBackground,
            videoResolution: videoResolution,
            sourceBitrate: sourceBitrate);
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

    private void ValidateDownscale(SourceVideo video, bool applyDownscale)
    {
        var targetHeight = Request.Downscale?.TargetHeight;
        if (!targetHeight.HasValue)
        {
            return;
        }

        if (!applyDownscale && video.Height > 0)
        {
            return;
        }

        if (!_videoSettingsProfiles.TryGetProfile(targetHeight.Value, out var profile))
        {
            return;
        }

        var issue = profile.ResolveSourceBucketIssue(video.Height);
        if (!string.IsNullOrWhiteSpace(issue))
        {
            throw RuntimeFailures.DownscaleSourceBucketIssue(issue);
        }
    }

    private static bool AreAudioStreamsCopyCompatible(IReadOnlyList<string> audioCodecs)
    {
        if (audioCodecs.Count == 0)
        {
            return true;
        }

        return audioCodecs.All(codec => codec.Equals("mp3", StringComparison.OrdinalIgnoreCase));
    }

    private string ResolveOutputPath(SourceVideo video, bool copyVideo, bool copyAudio)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var appliedDownscale = Request.Downscale is not null &&
                               video.Height > Request.Downscale.TargetHeight;
        if (appliedDownscale)
        {
            return Path.Combine(directory, $"{FormatKeepSourceDownscaleFileName(video.FileNameWithoutExtension, Request.Downscale!.TargetHeight)}.mkv");
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.mkv");
        if (!Request.KeepSource ||
            !video.Container.Equals("mkv", StringComparison.OrdinalIgnoreCase) ||
            (copyVideo && copyAudio))
        {
            return outputPath;
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.mkv");
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

    private static int ResolveOutputHeight(SourceVideo video, VideoIntent videoIntent, bool applyOverlayBackground, DownscaleRequest? downscale)
    {
        var (_, height) = ToMkvGpuVideoGeometry.ResolveOutputDimensions(video, videoIntent, applyOverlayBackground);
        if (height > 0)
        {
            return height;
        }

        if (downscale?.TargetHeight > 0)
        {
            return downscale.TargetHeight;
        }

        return Math.Max(1, video.Height);
    }

    private static ToMkvGpuResolvedSourceBitrate ResolveSourceBitrate(SourceVideo video)
    {
        if (video.PrimaryVideoBitrate.HasValue && video.PrimaryVideoBitrate.Value > 0)
        {
            return new ToMkvGpuResolvedSourceBitrate(video.PrimaryVideoBitrate.Value, "probe_video");
        }

        if (video.Bitrate.HasValue && video.Bitrate.Value > 0)
        {
            var resolved = SourceVideoBitrateResolver.ResolveVideoBitrateFromTotal(video.Bitrate.Value, video);
            return new ToMkvGpuResolvedSourceBitrate(
                resolved,
                resolved != video.Bitrate.Value ? "probe_minus_audio" : "probe");
        }

        if (video.Duration <= TimeSpan.Zero || string.IsNullOrWhiteSpace(video.FilePath) || !File.Exists(video.FilePath))
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        var fileSizeBytes = new FileInfo(video.FilePath).Length;
        if (fileSizeBytes <= 0)
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        var estimatedBitsPerSecond = Math.Round((fileSizeBytes * 8m) / (decimal)video.Duration.TotalSeconds, MidpointRounding.AwayFromZero);
        if (estimatedBitsPerSecond <= 0m || estimatedBitsPerSecond > long.MaxValue)
        {
            return new ToMkvGpuResolvedSourceBitrate(null, "missing");
        }

        var estimated = (long)estimatedBitsPerSecond;
        var resolvedEstimate = SourceVideoBitrateResolver.ResolveVideoBitrateFromTotal(estimated, video);
        return new ToMkvGpuResolvedSourceBitrate(
            resolvedEstimate,
            resolvedEstimate != estimated ? "file_size_estimate_minus_audio" : "file_size_estimate");
    }

    private static ToMkvGpuFfmpegTool CreateDefaultTool()
    {
        return new ToMkvGpuFfmpegTool("ffmpeg", NullLogger<ToMkvGpuFfmpegTool>.Instance);
    }
}
