using System.Globalization;
using Microsoft.Extensions.Logging;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToMkvGpu.Core;

/*
Это ffmpeg-адаптер сценария tomkvgpu.
Он рендерит mkv-ориентированное решение сценария в конкретные команды ffmpeg и post-steps для файлов.
*/
/// <summary>
/// Renders mkv-oriented scenario decisions into ffmpeg execution recipes.
/// </summary>
public sealed class ToMkvGpuFfmpegTool
{
    private readonly string _ffmpegPath;
    private readonly ILogger<ToMkvGpuFfmpegTool> _logger;

    /*
    Это создание tool-адаптера с путем к ffmpeg и логгером.
    */
    /// <summary>
    /// Initializes the mkv-oriented ffmpeg tool.
    /// </summary>
    public ToMkvGpuFfmpegTool(string ffmpegPath, ILogger<ToMkvGpuFfmpegTool> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentNullException.ThrowIfNull(logger);

        _ffmpegPath = ffmpegPath.Trim();
        _logger = logger;
    }

    /*
    Это стабильное имя инструмента для диагностики и интеграций.
    */
    /// <summary>
    /// Gets the stable tool name.
    /// </summary>
    public string Name => "ffmpeg";

    /*
    Это проверка, что decision поддерживается именно этим renderer-ом.
    */
    /// <summary>
    /// Determines whether the mkv-oriented ffmpeg tool can execute the supplied decision.
    /// </summary>
    internal bool CanHandle(ToMkvGpuDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Video is EncodeVideoIntent { UseFrameInterpolation: true })
        {
            return false;
        }

        if (decision.TargetContainer != TargetContainer.Mkv)
        {
            return false;
        }

        if (decision.Video is CopyVideoIntent)
        {
            return decision.VideoResolution is null;
        }

        return decision.Video is EncodeVideoIntent encodeVideo &&
               decision.VideoResolution is not null &&
               encodeVideo.PreferredBackend == VideoBackend.Gpu &&
               (encodeVideo.TargetVideoCodec == TargetVideoCodec.H264 ||
                encodeVideo.TargetVideoCodec == TargetVideoCodec.H265);
    }

    /*
    Это сборка полного execution-плана: ffmpeg-команда и post-операции над файлами.
    */
    /// <summary>
    /// Builds an ffmpeg execution recipe for the supplied source video and decision.
    /// </summary>
    internal ScenarioExecution BuildExecution(SourceVideo video, ToMkvGpuDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        if (!CanHandle(decision))
        {
            throw new NotSupportedException("The supplied transcode decision is not supported by ToMkvGpu ffmpeg tool.");
        }

        if (IsNoOp(video, decision))
        {
            return new ScenarioExecution(Array.Empty<string>());
        }

        if (decision.VideoResolution is not null && decision.SourceBitrate is not null)
        {
            LogVideoSettingsResolution(
                video.FilePath,
                decision.VideoResolution.BaseSettings,
                decision.VideoResolution.Settings,
                decision.SourceBitrate);
        }

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(video.FilePath, video.FileNameWithoutExtension, decision.KeepSource, finalOutputPath);
        var ffmpegCommand = BuildFfmpegCommand(video, decision, workingOutputPath);
        var commands = new List<string> { ffmpegCommand };

        FfmpegExecutionLayout.AppendPostOperations(commands, video.FilePath, decision.KeepSource, workingOutputPath, finalOutputPath);

        return new ScenarioExecution(commands);
    }

    private static bool IsNoOp(SourceVideo video, ToMkvGpuDecision decision)
    {
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        return decision.CopyVideo &&
               decision.CopyAudio &&
               video.Container.Equals(decision.TargetContainer.Value, StringComparison.OrdinalIgnoreCase) &&
               finalOutputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFfmpegCommand(SourceVideo video, ToMkvGpuDecision decision, string outputPath)
    {
        var parts = new List<string>
        {
            _ffmpegPath,
            "-hide_banner"
        };

        var sanitizePart = BuildSanitizePart(video, decision);
        if (!string.IsNullOrWhiteSpace(sanitizePart))
        {
            parts.Add(sanitizePart);
        }

        if (decision.Video is EncodeVideoIntent encodeVideo &&
            encodeVideo.PreferredBackend == VideoBackend.Gpu)
        {
            parts.Add(decision.NvdecMaxThreads is not null
                ? $"-hwaccel cuda -hwaccel_output_format cuda -threads:v {decision.NvdecMaxThreads}"
                : "-hwaccel cuda -hwaccel_output_format cuda");
        }

        parts.Add("-i");
        parts.Add(FfmpegExecutionLayout.Quote(video.FilePath));
        parts.Add(BuildVideoPart(video, decision));
        parts.Add(BuildAudioPart(decision));
        parts.Add("-sn");
        parts.Add("-max_muxing_queue_size 4096");
        parts.Add(FfmpegExecutionLayout.Quote(outputPath));

        return string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildSanitizePart(SourceVideo video, ToMkvGpuDecision decision)
    {
        if (decision.FixTimestamps || UsesStrongSyncRemux(decision))
        {
            return "-fflags +genpts+igndts -avoid_negative_ts make_zero";
        }

        var needsContainerChange = !video.Container.Equals(decision.TargetContainer.Value, StringComparison.OrdinalIgnoreCase);
        if (decision.RequiresVideoEncode || decision.RequiresAudioEncode || needsContainerChange)
        {
            return "-avoid_negative_ts make_zero";
        }

        return string.Empty;
    }

    private string BuildVideoPart(SourceVideo video, ToMkvGpuDecision decision)
    {
        if (decision.Video is CopyVideoIntent)
        {
            return UsesStrongSyncRemux(decision)
                ? "-map 0:v:0 -c:v copy -copytb 1"
                : "-map 0:v:0 -c:v copy";
        }

        var encodeVideo = GetRequiredEncodeVideoIntent(decision);
        var videoResolution = GetRequiredVideoResolution(decision);
        var encoder = ResolveVideoEncoder(decision);
        var settings = videoResolution.Settings;
        var fpsToken = ResolveFrameRateToken(video, decision);
        var gop = ResolveGop(video, decision);
        var compatibilityPart = ResolveVideoCompatibilityPart(video, decision);
        var preset = encodeVideo.EncoderPreset
                     ?? throw new InvalidOperationException("Encoder preset must be resolved before tool rendering.");
        var frameRatePart = encodeVideo.TargetFramesPerSecond.HasValue
            ? $"-fps_mode:v cfr -r {fpsToken} "
            : string.Empty;
        var aqPart = "-spatial_aq 1 -temporal_aq 1 -rc-lookahead 32 ";
        var pixelFormatPart = encodeVideo.PreferredBackend == VideoBackend.Gpu
            ? string.Empty
            : "-pix_fmt yuv420p ";
        var rateControlPart = $"-rc vbr_hq -cq {settings.Cq} -b:v 0 -maxrate {FormatRate(settings.Maxrate)} -bufsize {FormatRate(settings.Bufsize)} ";
        var downscale = encodeVideo.Downscale;

        if (decision.ApplyOverlayBackground)
        {
            var filter = BuildOverlayFilter(video, downscale?.TargetHeight, settings.Algorithm);
            return $"-filter_complex {FfmpegExecutionLayout.Quote(filter)} -map \"[v]\" {frameRatePart}" +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{pixelFormatPart}{compatibilityPart}-g {gop}";
        }

        if (downscale is not null)
        {
            return $"-map 0:v:0 {frameRatePart}-vf \"scale_cuda=-2:{downscale.TargetHeight}:interp_algo={settings.Algorithm}:format=nv12\" " +
                   $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
                   $"{compatibilityPart}-g {gop}";
        }

        return $"-map 0:v:0 {frameRatePart}" +
               $"-c:v {encoder} -preset {preset} {rateControlPart}{aqPart}" +
               $"{pixelFormatPart}{compatibilityPart}-g {gop}";
    }

    private static string BuildAudioPart(ToMkvGpuDecision decision)
    {
        return decision.Audio switch
        {
            CopyAudioIntent => "-map 0:a? -c:a copy",
            SynchronizeAudioIntent => "-map 0:a? -c:a libmp3lame -q:a 2 -af \"aresample=async=1:first_pts=0\"",
            RepairAudioIntent => "-map 0:a? -c:a libmp3lame -q:a 2 -af \"aresample=async=1:first_pts=0\"",
            EncodeAudioIntent => "-map 0:a? -c:a libmp3lame -q:a 2",
            _ => throw new InvalidOperationException("Unsupported audio intent type.")
        };
    }

    private static bool UsesStrongSyncRemux(ToMkvGpuDecision decision)
    {
        return decision.Video is CopyVideoIntent &&
               decision.Audio is SynchronizeAudioIntent;
    }

    private static string ResolveVideoEncoder(ToMkvGpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoIntent(decision);
        return encodeVideo.TargetVideoCodec switch
        {
            var codec when codec == TargetVideoCodec.H264 => "h264_nvenc",
            var codec when codec == TargetVideoCodec.H265 => "hevc_nvenc",
            _ => throw new NotSupportedException($"Video codec '{encodeVideo.TargetVideoCodec}' is not supported by ToMkvGpu ffmpeg tool.")
        };
    }

    private static string ResolveFrameRateToken(SourceVideo video, ToMkvGpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoIntent(decision);
        if (encodeVideo.TargetFramesPerSecond.HasValue)
        {
            return encodeVideo.TargetFramesPerSecond.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return video.FramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static int ResolveGop(SourceVideo video, ToMkvGpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoIntent(decision);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        return (int)Math.Max(12, Math.Round(fps * 2.0));
    }

    private static string ResolveVideoCompatibilityPart(SourceVideo video, ToMkvGpuDecision decision)
    {
        var encodeVideo = GetRequiredEncodeVideoIntent(decision);
        var (width, height) = ToMkvGpuVideoGeometry.ResolveOutputDimensions(video, decision.Video, decision.ApplyOverlayBackground);
        var fps = encodeVideo.TargetFramesPerSecond ?? video.FramesPerSecond;
        var compatibilityPart = VideoCodecCompatibility.ResolveArguments(
            $"{encodeVideo.TargetVideoCodec}",
            encodeVideo.CompatibilityProfile,
            width,
            height,
            fps);
        return string.IsNullOrWhiteSpace(compatibilityPart)
            ? string.Empty
            : $"{compatibilityPart} ";
    }

    private static EncodeVideoIntent GetRequiredEncodeVideoIntent(ToMkvGpuDecision decision)
    {
        return decision.Video as EncodeVideoIntent
            ?? throw new InvalidOperationException("Video encode intent is required for this operation.");
    }

    private static ProfileDrivenVideoSettingsResolution GetRequiredVideoResolution(ToMkvGpuDecision decision)
    {
        return decision.VideoResolution
            ?? throw new InvalidOperationException("ToMkvGpu video resolution details are required for this operation.");
    }

    private static string FormatRate(decimal value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}M";
    }

    private void LogVideoSettingsResolution(
        string inputPath,
        ResolvedVideoSettings baseSettings,
        ResolvedVideoSettings resolvedSettings,
        ToMkvGpuResolvedSourceBitrate sourceBitrate)
    {
        _logger.LogInformation(
            "Video settings resolved. InputPath={InputPath} Profile={Profile} SourceBitrateOrigin={SourceBitrateOrigin} SourceBitrateMbps={SourceBitrateMbps} Base={BaseSettings} Resolved={ResolvedSettings}",
            inputPath,
            $"{baseSettings.ContentProfile}/{baseSettings.QualityProfile}",
            sourceBitrate.Origin,
            FormatBitrateMbps(sourceBitrate.Bitrate),
            FormatSettings(baseSettings),
            FormatSettings(resolvedSettings));
    }

    private static string BuildOverlayFilter(SourceVideo video, int? targetHeight, VideoScaleAlgorithm downscaleAlgorithm)
    {
        var (outputWidth, outputHeight) = ToMkvGpuVideoGeometry.ResolveOverlayOutputDimensions(video, targetHeight);

        if (targetHeight.HasValue)
        {
            return "[0:v]split=2[bg0][fg0];" +
                   $"[bg0]scale_cuda={outputWidth}:-2:interp_algo={downscaleAlgorithm}:format=nv12,hwdownload,format=nv12,crop={outputWidth}:{outputHeight},hwupload_cuda[bg];" +
                   $"[fg0]scale_cuda=-2:{outputHeight}:interp_algo={downscaleAlgorithm}:format=nv12[fg];" +
                   "[bg][fg]overlay_cuda=(W-w)/2:0[v]";
        }

        return $"[0:v]scale={outputWidth}:-1,crop={outputWidth}:{outputHeight}[bg];[0:v]scale=-1:{outputHeight}[fg];[bg][fg]overlay=(W-w)/2:0[v]";
    }

    private static string FormatSettings(ResolvedVideoSettings settings)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{settings.ContentProfile}/{settings.QualityProfile} cq{settings.Cq} max{settings.Maxrate:0.###} buf{settings.Bufsize:0.###}");
    }

    private static string? FormatBitrateMbps(long? bitrate)
    {
        if (!bitrate.HasValue || bitrate.Value <= 0)
        {
            return null;
        }

        var value = bitrate.Value / 1_000_000m;
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
