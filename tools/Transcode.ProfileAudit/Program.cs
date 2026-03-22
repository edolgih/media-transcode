using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Transcode.Core.Inspection;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;

var options = AuditOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(AuditOptions.HelpText);
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.CsvPath)!);
Directory.CreateDirectory(options.TempDirectory);

var inspector = new VideoInspector(new FfprobeVideoProbe(options.FfprobePath));
var profiles = VideoSettingsProfiles.Default;
var resolver = new VideoSettingsResolver(profiles);
var sampleMeasurer = new FfmpegSampleMeasurer(options.FfmpegPath);
var files = EnumerateSourceFiles(options.SourceRoot).ToArray();

if (files.Length == 0)
{
    Console.Error.WriteLine($"No supported media files were found under '{options.SourceRoot}'.");
    return 1;
}

var results = new List<AuditCaseResult>();

foreach (var filePath in files)
{
    var relativePath = Path.GetRelativePath(options.SourceRoot, filePath);
    if (!options.MatchesIncludeFilters(relativePath))
    {
        continue;
    }

    var folderName = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
    if (!TryMapContentProfile(folderName, out var contentProfile))
    {
        results.Add(AuditCaseResult.CreateSkipped(relativePath, $"Unsupported content folder '{folderName}'."));
        continue;
    }

    Console.WriteLine($"Inspecting {relativePath}...");
    SourceVideo sourceVideo;
    try
    {
        sourceVideo = inspector.Load(filePath);
    }
    catch (Exception exception)
    {
        results.Add(AuditCaseResult.CreateSkipped(relativePath, $"Inspect failed: {exception.Message}"));
        continue;
    }

    var sourceProbe = ProbeMedia(options.FfprobePath, filePath);
    var request = VideoSettingsRequest.CreateOrNull(
        contentProfile: contentProfile,
        qualityProfile: options.QualityProfile,
        autoSampleMode: options.AutoSampleMode,
        cq: null,
        maxrate: null,
        bufsize: null);
    var nativeProfile = profiles.ResolveOutputProfile(sourceVideo.Height);
    results.Add(EvaluateCase(
        options,
        resolver,
        sampleMeasurer,
        sourceVideo,
        sourceProbe,
        relativePath,
        contentProfile,
        nativeProfile.TargetHeight,
        caseKind: "encode",
        request: request,
        downscaleRequest: null));

    foreach (var targetHeight in profiles.GetSupportedDownscaleTargetHeights().Where(targetHeight => targetHeight < sourceVideo.Height))
    {
        var targetProfile = profiles.GetRequiredProfile(targetHeight);
        var sourceBucketIssue = targetProfile.ResolveSourceBucketIssue(sourceVideo.Height);
        if (!string.IsNullOrWhiteSpace(sourceBucketIssue))
        {
            results.Add(AuditCaseResult.CreateSkipped(
                relativePath,
                $"Skipped downscale {targetHeight}: {sourceBucketIssue}",
                caseKind: "downscale",
                targetHeight: targetHeight,
                contentProfile: contentProfile));
            continue;
        }

        results.Add(EvaluateCase(
            options,
            resolver,
            sampleMeasurer,
            sourceVideo,
            sourceProbe,
            relativePath,
            contentProfile,
            targetHeight,
            caseKind: "downscale",
            request: request,
            downscaleRequest: new DownscaleRequest(targetHeight)));
    }
}

var markdown = BuildMarkdownReport(options, results);
var csv = BuildCsv(results);
File.WriteAllText(options.ReportPath, markdown, Encoding.UTF8);
File.WriteAllText(options.CsvPath, csv, new UTF8Encoding(false));
TryDeleteDirectory(options.TempDirectory);

Console.WriteLine($"Report saved to {options.ReportPath}");
Console.WriteLine($"Raw data saved to {options.CsvPath}");
return 0;

static AuditCaseResult EvaluateCase(
    AuditOptions options,
    VideoSettingsResolver resolver,
    FfmpegSampleMeasurer sampleMeasurer,
    SourceVideo sourceVideo,
    MediaProbeResult sourceProbe,
    string relativePath,
    string contentProfile,
    int targetHeight,
    string caseKind,
    VideoSettingsRequest? request,
    DownscaleRequest? downscaleRequest)
{
    var resolution = downscaleRequest is null
        ? resolver.ResolveForEncode(
            request: request,
            outputHeight: targetHeight,
            duration: sourceVideo.Duration,
            sourceBitrate: sourceVideo.Bitrate,
            hasAudio: sourceVideo.HasAudio,
            defaultAutoSampleMode: options.AutoSampleMode ?? "hybrid",
            accurateReductionProvider: (settings, windows) => sampleMeasurer.MeasureAverageReduction(sourceVideo.FilePath, targetHeight, settings, windows))
        : resolver.ResolveForDownscale(
            request: downscaleRequest,
            videoSettings: request,
            sourceHeight: sourceVideo.Height,
            duration: sourceVideo.Duration,
            sourceBitrate: sourceVideo.Bitrate,
            hasAudio: sourceVideo.HasAudio,
            defaultAutoSampleMode: options.AutoSampleMode ?? "hybrid",
            accurateReductionProvider: (settings, windows) => sampleMeasurer.MeasureAverageReduction(sourceVideo.FilePath, targetHeight, settings, windows));

    var shouldRunFullEncode = options.FullEncodeMode.Equals("all", StringComparison.OrdinalIgnoreCase) ||
                              options.FullEncodeMode.Equals("native-only", StringComparison.OrdinalIgnoreCase) &&
                              caseKind.Equals("encode", StringComparison.OrdinalIgnoreCase);
    FullEncodeMetrics? fullEncode = null;
    if (shouldRunFullEncode)
    {
        fullEncode = RunFullEncode(options, sourceVideo, sourceProbe, relativePath, caseKind, resolution.Settings, downscaleRequest);
    }

    return new AuditCaseResult(
        RelativePath: relativePath,
        SourceContainer: sourceVideo.Container,
        SourceVideoCodec: sourceVideo.VideoCodec,
        SourceHeight: sourceVideo.Height,
        SourceFramesPerSecond: sourceVideo.FramesPerSecond,
        SourceDurationSeconds: Math.Round(sourceVideo.Duration.TotalSeconds, 3, MidpointRounding.AwayFromZero),
        SourceTotalBitrateMbps: ToMbps(sourceVideo.Bitrate),
        SourceVideoBitrateMbps: ToMbps(sourceProbe.VideoBitrate),
        ContentProfile: contentProfile,
        QualityProfile: resolution.EffectiveSelection.QualityProfile,
        CaseKind: caseKind,
        TargetHeight: targetHeight,
        SupportsDownscale: downscaleRequest is not null,
        ProfileTargetHeight: resolution.Profile.TargetHeight,
        AutoSampleMode: resolution.AutoSample.Mode,
        AutoSamplePath: resolution.AutoSample.Path,
        AutoSampleReason: resolution.AutoSample.Reason,
        AutoSampleIterations: resolution.AutoSample.IterationCount,
        AutoSampleWindows: FormatWindows(resolution.AutoSample.Windows),
        Corridor: FormatCorridor(resolution.AutoSample.Corridor),
        LastReductionPercent: resolution.AutoSample.LastReductionPercent,
        InBounds: resolution.AutoSample.InBounds,
        BaseCq: resolution.BaseSettings.Cq,
        BaseMaxrateMbps: resolution.BaseSettings.Maxrate,
        BaseBufsizeMbps: resolution.BaseSettings.Bufsize,
        BaseAlgorithm: resolution.BaseSettings.Algorithm,
        ResolvedCq: resolution.Settings.Cq,
        ResolvedMaxrateMbps: resolution.Settings.Maxrate,
        ResolvedBufsizeMbps: resolution.Settings.Bufsize,
        ResolvedAlgorithm: resolution.Settings.Algorithm,
        FullEncode: fullEncode,
        SkipReason: null);
}

static FullEncodeMetrics? RunFullEncode(
    AuditOptions options,
    SourceVideo sourceVideo,
    MediaProbeResult sourceProbe,
    string relativePath,
    string caseKind,
    VideoSettingsDefaults settings,
    DownscaleRequest? downscaleRequest)
{
    var safeName = BuildSafeName(relativePath, caseKind, downscaleRequest?.TargetHeight ?? sourceVideo.Height);
    var outputPath = Path.Combine(options.TempDirectory, $"{safeName}.mkv");
    var arguments = BuildFfmpegArguments(options.FfmpegPath, sourceVideo.FilePath, outputPath, settings, downscaleRequest);

    try
    {
        var execution = ExecuteProcess(arguments);
        if (execution.ExitCode != 0)
        {
            return new FullEncodeMetrics(
                OutputVideoBitrateMbps: null,
                OutputTotalBitrateMbps: null,
                VideoReductionPercent: null,
                TotalReductionPercent: null,
                OutputSizeMegabytes: null,
                Failure: execution.StandardError);
        }

        var probe = ProbeMedia(options.FfprobePath, outputPath);
        var outputSizeBytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0L;

        return new FullEncodeMetrics(
            OutputVideoBitrateMbps: ToMbps(probe.VideoBitrate),
            OutputTotalBitrateMbps: ToMbps(probe.TotalBitrate),
            VideoReductionPercent: CalculateReductionPercent(sourceProbe.VideoBitrate, probe.VideoBitrate),
            TotalReductionPercent: CalculateReductionPercent(sourceProbe.TotalBitrate, probe.TotalBitrate),
            OutputSizeMegabytes: outputSizeBytes > 0 ? Math.Round(outputSizeBytes / 1024d / 1024d, 2, MidpointRounding.AwayFromZero) : null,
            Failure: null);
    }
    finally
    {
        TryDelete(outputPath);
    }
}

static IReadOnlyList<string> BuildFfmpegArguments(
    string ffmpegPath,
    string inputPath,
    string outputPath,
    VideoSettingsDefaults settings,
    DownscaleRequest? downscaleRequest)
{
    var arguments = new List<string>
    {
        ffmpegPath,
        "-hide_banner",
        "-loglevel", "error",
        "-y",
        "-hwaccel", "cuda",
        "-hwaccel_output_format", "cuda",
        "-i", inputPath,
        "-map", "0:v:0"
    };

    if (downscaleRequest is not null)
    {
        arguments.Add("-vf");
        arguments.Add($"scale_cuda=-2:{downscaleRequest.TargetHeight}:interp_algo={settings.Algorithm}:format=nv12");
    }

    arguments.AddRange(
    [
        "-c:v", "h264_nvenc",
        "-preset", NvencPresetOptions.DefaultPreset,
        "-rc", "vbr_hq",
        "-cq", settings.Cq.ToString(CultureInfo.InvariantCulture),
        "-b:v", "0",
        "-maxrate", $"{settings.Maxrate.ToString("0.0##", CultureInfo.InvariantCulture)}M",
        "-bufsize", $"{settings.Bufsize.ToString("0.0##", CultureInfo.InvariantCulture)}M",
        "-spatial_aq", "1",
        "-temporal_aq", "1",
        "-rc-lookahead", "32",
        "-profile:v", "high",
        "-an",
        "-sn",
        outputPath
    ]);

    return arguments;
}

static MediaProbeResult ProbeMedia(string ffprobePath, string filePath)
{
    var execution = ExecuteProcess(
    [
        ffprobePath,
        "-v", "error",
        "-print_format", "json",
        "-show_format",
        "-show_streams",
        filePath
    ]);

    if (execution.ExitCode != 0)
    {
        throw new InvalidOperationException($"ffprobe failed for '{filePath}': {execution.StandardError}");
    }

    using var document = JsonDocument.Parse(execution.StandardOutput);
    var root = document.RootElement;
    long? totalBitrate = null;
    long? videoBitrate = null;
    var audioStreamCount = 0;
    double? durationSeconds = null;

    if (root.TryGetProperty("format", out var formatElement) &&
        formatElement.TryGetProperty("bit_rate", out var formatBitrateElement) &&
        TryReadInt64(formatBitrateElement, out var parsedTotalBitrate))
    {
        totalBitrate = parsedTotalBitrate;
    }

    if (root.TryGetProperty("format", out formatElement) &&
        formatElement.TryGetProperty("duration", out var durationElement) &&
        TryReadDouble(durationElement, out var parsedDurationSeconds))
    {
        durationSeconds = parsedDurationSeconds;
    }

    if (root.TryGetProperty("streams", out var streamsElement))
    {
        foreach (var stream in streamsElement.EnumerateArray())
        {
            if (!stream.TryGetProperty("codec_type", out var codecTypeElement))
            {
                continue;
            }

            var codecType = codecTypeElement.GetString();
            if (string.Equals(codecType, "audio", StringComparison.OrdinalIgnoreCase))
            {
                audioStreamCount++;
            }

            if (!string.Equals(codecType, "video", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (stream.TryGetProperty("bit_rate", out var videoBitrateElement) &&
                TryReadInt64(videoBitrateElement, out var parsedVideoBitrate))
            {
                videoBitrate = parsedVideoBitrate;
            }

            break;
        }
    }

    if (!totalBitrate.HasValue && durationSeconds.HasValue && durationSeconds.Value > 0d && File.Exists(filePath))
    {
        totalBitrate = (long)Math.Round((new FileInfo(filePath).Length * 8d) / durationSeconds.Value, MidpointRounding.AwayFromZero);
    }

    if (!videoBitrate.HasValue && audioStreamCount == 0)
    {
        videoBitrate = totalBitrate;
    }

    return new MediaProbeResult(totalBitrate, videoBitrate, durationSeconds, audioStreamCount);
}

static ProcessResult ExecuteProcess(IReadOnlyList<string> arguments)
{
    using var process = new Process();
    process.StartInfo = new ProcessStartInfo
    {
        FileName = arguments[0],
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    foreach (var argument in arguments.Skip(1))
    {
        process.StartInfo.ArgumentList.Add(argument);
    }

    process.Start();
    var standardOutput = process.StandardOutput.ReadToEnd();
    var standardError = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return new ProcessResult(process.ExitCode, standardOutput, standardError);
}

static IEnumerable<string> EnumerateSourceFiles(string sourceRoot)
{
    var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".avi",
        ".asf",
        ".flv",
        ".mkv",
        ".mov",
        ".mp4",
        ".ts",
        ".wmv"
    };

    return Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
        .Where(path => extensions.Contains(Path.GetExtension(path)));
}

static bool TryMapContentProfile(string folderName, out string contentProfile)
{
    contentProfile = folderName.Trim().ToLowerInvariant() switch
    {
        "anime" => "anime",
        "films" => "film",
        "film" => "film",
        "mult" => "mult",
        _ => string.Empty
    };

    return !string.IsNullOrWhiteSpace(contentProfile);
}

static string BuildMarkdownReport(AuditOptions options, IReadOnlyList<AuditCaseResult> results)
{
    var completedResults = results.Where(static result => string.IsNullOrWhiteSpace(result.SkipReason)).ToArray();
    var skippedResults = results.Where(static result => !string.IsNullOrWhiteSpace(result.SkipReason)).ToArray();
    var summaries = completedResults
        .GroupBy(result => new SummaryGroupKey(result.CaseKind, result.TargetHeight, result.ContentProfile ?? string.Empty))
        .OrderBy(group => group.Key.TargetHeight)
        .ThenBy(group => group.Key.CaseKind, StringComparer.Ordinal)
        .ThenBy(group => group.Key.ContentProfile, StringComparer.Ordinal)
        .Select(BuildSummaryRow)
        .ToArray();

    var builder = new StringBuilder();
    builder.AppendLine("# Profile Audit");
    builder.AppendLine();
    builder.AppendLine($"- Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    builder.AppendLine($"- Source root: `{options.SourceRoot}`");
    builder.AppendLine($"- Quality profile: `{options.QualityProfile}`");
    builder.AppendLine($"- Autosample mode: `{options.AutoSampleMode ?? "scenario-default"}`");
    builder.AppendLine($"- Full encode mode: `{options.FullEncodeMode}`");
    if (options.IncludeFilters.Count > 0)
    {
        builder.AppendLine($"- Include filters: `{string.Join("`, `", options.IncludeFilters)}`");
    }
    builder.AppendLine($"- Source files: `{completedResults.Select(static result => result.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()}`");
    builder.AppendLine($"- Audit cases: `{completedResults.Length}`");
    builder.AppendLine($"- Skipped cases: `{skippedResults.Length}`");
    builder.AppendLine();
    builder.AppendLine("## Summary");
    builder.AppendLine();
    builder.AppendLine("| Case | Target | Content | Runs | In bounds | Estimate-only | Sample-backed | Avg last reduction % | Avg maxrate Mbps | Avg full video reduction % | Notes |");
    builder.AppendLine("| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

    foreach (var summary in summaries)
    {
        builder.AppendLine(
            $"| {summary.CaseKind} | {summary.TargetHeight} | {summary.ContentProfile} | {summary.RunCount} | {summary.InBoundsCount}/{summary.RunCount} | {summary.EstimateOnlyCount} | {summary.SampleBackedCount} | {FormatNullable(summary.AverageLastReductionPercent)} | {FormatNullable(summary.AverageResolvedMaxrateMbps)} | {FormatNullable(summary.AverageFullVideoReductionPercent)} | {summary.Note} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Findings");
    builder.AppendLine();

    if (summaries.Length == 0)
    {
        builder.AppendLine("No completed cases.");
    }
    else
    {
        foreach (var summary in summaries.Where(summary => !string.IsNullOrWhiteSpace(summary.Note)))
        {
            builder.AppendLine($"- `{summary.CaseKind}` `{summary.TargetHeight}` `{summary.ContentProfile}`: {summary.Note}");
        }

        if (summaries.All(summary => string.IsNullOrWhiteSpace(summary.Note)))
        {
            builder.AppendLine("- No obvious corridor or maxrate outliers were detected in this corpus.");
        }
    }

    if (skippedResults.Length > 0)
    {
        builder.AppendLine();
        builder.AppendLine("## Skipped");
        builder.AppendLine();

        foreach (var skipped in skippedResults)
        {
            builder.AppendLine($"- `{skipped.RelativePath}`: {skipped.SkipReason}");
        }
    }

    builder.AppendLine();
    builder.AppendLine("## Raw Cases");
    builder.AppendLine();
    builder.AppendLine("| File | Case | Target | Content | Mode | Path | Corridor | Last reduction % | CQ | Maxrate | Algo | Full video reduction % | Full output video Mbps | Failure |");
    builder.AppendLine("| --- | --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: | --- | ---: | ---: | --- |");

    foreach (var result in completedResults.OrderBy(static result => result.RelativePath, StringComparer.OrdinalIgnoreCase).ThenBy(static result => result.TargetHeight))
    {
        builder.AppendLine(
            $"| `{result.RelativePath}` | {result.CaseKind} | {result.TargetHeight} | {result.ContentProfile} | {result.AutoSampleMode} | {result.AutoSamplePath} | {EscapeTable(result.Corridor)} | {FormatNullable(result.LastReductionPercent)} | {result.ResolvedCq} | {FormatNullable(result.ResolvedMaxrateMbps)} | {result.ResolvedAlgorithm} | {FormatNullable(result.FullEncode?.VideoReductionPercent)} | {FormatNullable(result.FullEncode?.OutputVideoBitrateMbps)} | {EscapeTable(result.FullEncode?.Failure)} |");
    }

    return builder.ToString();
}

static string BuildCsv(IReadOnlyList<AuditCaseResult> results)
{
    var builder = new StringBuilder();
    builder.AppendLine("relative_path,case_kind,target_height,content_profile,quality_profile,source_height,source_total_bitrate_mbps,source_video_bitrate_mbps,autosample_mode,autosample_path,corridor,last_reduction_percent,in_bounds,resolved_cq,resolved_maxrate_mbps,resolved_bufsize_mbps,resolved_algorithm,full_output_video_bitrate_mbps,full_output_total_bitrate_mbps,full_video_reduction_percent,full_total_reduction_percent,skip_reason");

    foreach (var result in results)
    {
        builder.AppendLine(string.Join(",",
            QuoteCsv(result.RelativePath),
            QuoteCsv(result.CaseKind),
            result.TargetHeight.ToString(CultureInfo.InvariantCulture),
            QuoteCsv(result.ContentProfile),
            QuoteCsv(result.QualityProfile),
            result.SourceHeight.ToString(CultureInfo.InvariantCulture),
            QuoteCsv(FormatNullable(result.SourceTotalBitrateMbps)),
            QuoteCsv(FormatNullable(result.SourceVideoBitrateMbps)),
            QuoteCsv(result.AutoSampleMode),
            QuoteCsv(result.AutoSamplePath),
            QuoteCsv(result.Corridor),
            QuoteCsv(FormatNullable(result.LastReductionPercent)),
            QuoteCsv(result.InBounds?.ToString()),
            QuoteCsv(result.ResolvedCq?.ToString(CultureInfo.InvariantCulture)),
            QuoteCsv(FormatNullable(result.ResolvedMaxrateMbps)),
            QuoteCsv(FormatNullable(result.ResolvedBufsizeMbps)),
            QuoteCsv(result.ResolvedAlgorithm),
            QuoteCsv(FormatNullable(result.FullEncode?.OutputVideoBitrateMbps)),
            QuoteCsv(FormatNullable(result.FullEncode?.OutputTotalBitrateMbps)),
            QuoteCsv(FormatNullable(result.FullEncode?.VideoReductionPercent)),
            QuoteCsv(FormatNullable(result.FullEncode?.TotalReductionPercent)),
            QuoteCsv(result.SkipReason)));
    }

    return builder.ToString();
}

static SummaryRow BuildSummaryRow(IGrouping<SummaryGroupKey, AuditCaseResult> group)
{
    var inBoundsCount = group.Count(result => result.InBounds == true);
    var estimateOnlyCount = group.Count(result => string.Equals(result.AutoSamplePath, "estimate", StringComparison.OrdinalIgnoreCase));
    var sampleBackedCount = group.Count(result => !string.IsNullOrWhiteSpace(result.AutoSamplePath) &&
                                                  !string.Equals(result.AutoSamplePath, "estimate", StringComparison.OrdinalIgnoreCase) &&
                                                  !string.Equals(result.AutoSamplePath, "skip", StringComparison.OrdinalIgnoreCase));
    var averageLastReductionPercent = Average(group.Select(result => result.LastReductionPercent));
    var averageResolvedMaxrateMbps = Average(group.Select(result => result.ResolvedMaxrateMbps));
    var averageFullVideoReductionPercent = Average(group.Select(result => result.FullEncode?.VideoReductionPercent));
    var averageFullBitrateRatio = Average(group
        .Where(result => result.FullEncode?.OutputVideoBitrateMbps.HasValue == true && result.ResolvedMaxrateMbps.HasValue)
        .Select(result => (decimal?)(result.FullEncode!.OutputVideoBitrateMbps!.Value / result.ResolvedMaxrateMbps!.Value)));

    string? note = null;
    if (group.Count() >= 2 && inBoundsCount < group.Count())
    {
        note = $"corridor miss on {group.Count() - inBoundsCount} of {group.Count()} runs";
    }
    else if (averageFullBitrateRatio.HasValue && averageFullBitrateRatio.Value < 0.55m)
    {
        note = "full-file output bitrate stays far below resolved maxrate";
    }
    else if (averageFullBitrateRatio.HasValue && averageFullBitrateRatio.Value > 0.95m)
    {
        note = "full-file output bitrate is very close to resolved maxrate";
    }

    return new SummaryRow(
        CaseKind: group.Key.CaseKind,
        TargetHeight: group.Key.TargetHeight,
        ContentProfile: group.Key.ContentProfile,
        RunCount: group.Count(),
        InBoundsCount: inBoundsCount,
        EstimateOnlyCount: estimateOnlyCount,
        SampleBackedCount: sampleBackedCount,
        AverageLastReductionPercent: averageLastReductionPercent,
        AverageResolvedMaxrateMbps: averageResolvedMaxrateMbps,
        AverageFullVideoReductionPercent: averageFullVideoReductionPercent,
        Note: note ?? string.Empty);
}

static decimal? Average(IEnumerable<decimal?> values)
{
    var materialized = values.Where(static value => value.HasValue).Select(static value => value!.Value).ToArray();
    if (materialized.Length == 0)
    {
        return null;
    }

    return Math.Round(materialized.Average(), 2, MidpointRounding.AwayFromZero);
}

static string FormatWindows(IReadOnlyList<VideoSettingsSampleWindow> windows)
{
    return windows.Count == 0
        ? "-"
        : string.Join(";", windows.Select(window => $"{window.StartSeconds}+{window.DurationSeconds}"));
}

static string FormatCorridor(VideoSettingsRange? corridor)
{
    if (corridor is null)
    {
        return "-";
    }

    var min = corridor.MinInclusive.HasValue
        ? $">={corridor.MinInclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
        : corridor.MinExclusive.HasValue
            ? $">{corridor.MinExclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : "-inf";
    var max = corridor.MaxInclusive.HasValue
        ? $"<={corridor.MaxInclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
        : corridor.MaxExclusive.HasValue
            ? $"<{corridor.MaxExclusive.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
            : "+inf";
    return $"{min}..{max}";
}

static decimal? ToMbps(long? bitrate)
{
    return bitrate.HasValue && bitrate.Value > 0
        ? Math.Round(bitrate.Value / 1_000_000m, 3, MidpointRounding.AwayFromZero)
        : null;
}

static decimal? CalculateReductionPercent(long? sourceBitrate, long? outputBitrate)
{
    if (!sourceBitrate.HasValue || !outputBitrate.HasValue || sourceBitrate.Value <= 0 || outputBitrate.Value <= 0)
    {
        return null;
    }

    var reduction = (1m - (outputBitrate.Value / (decimal)sourceBitrate.Value)) * 100m;
    return Math.Round(reduction, 2, MidpointRounding.AwayFromZero);
}

static string BuildSafeName(string relativePath, string caseKind, int targetHeight)
{
    var rawName = $"{relativePath}-{caseKind}-{targetHeight}";
    var invalidChars = Path.GetInvalidFileNameChars();
    var builder = new StringBuilder(rawName.Length);
    foreach (var character in rawName)
    {
        builder.Append(invalidChars.Contains(character) || character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar
            ? '_'
            : character);
    }

    return builder.ToString();
}

static bool TryReadInt64(JsonElement element, out long value)
{
    value = 0;
    return element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetInt64(out value),
        JsonValueKind.String => long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value),
        _ => false
    };
}

static bool TryReadDouble(JsonElement element, out double value)
{
    value = 0d;
    return element.ValueKind switch
    {
        JsonValueKind.Number => element.TryGetDouble(out value),
        JsonValueKind.String => double.TryParse(element.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value),
        _ => false
    };
}

static string FormatNullable(decimal? value)
{
    return value.HasValue
        ? value.Value.ToString("0.##", CultureInfo.InvariantCulture)
        : "-";
}

static string EscapeTable(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? "-"
        : value.Replace("|", "\\|", StringComparison.Ordinal);
}

static string QuoteCsv(string? value)
{
    var normalized = value ?? string.Empty;
    return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

static void TryDelete(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    try
    {
        File.Delete(path);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

static void TryDeleteDirectory(string path)
{
    if (!Directory.Exists(path))
    {
        return;
    }

    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

internal sealed record AuditOptions(
    bool ShowHelp,
    string SourceRoot,
    string ReportPath,
    string CsvPath,
    string TempDirectory,
    string FfmpegPath,
    string FfprobePath,
    string QualityProfile,
    string? AutoSampleMode,
    IReadOnlyList<string> IncludeFilters,
    string FullEncodeMode)
{
    private const string DefaultSourceRoot = @"C:\Users\Evgeny\Desktop\downscale\tests\src";
    public const string HelpText =
        """
        Transcode.ProfileAudit

        Usage:
          dotnet run --project tools\Transcode.ProfileAudit\Transcode.ProfileAudit.csproj -- [options]

        Options:
          --source-root <path>       Media corpus root. Default: C:\Users\Evgeny\Desktop\downscale\tests\src
          --report <path>            Markdown report path. Default: docs\audits\profile-audit-YYYY-MM-DD.md
          --csv <path>               CSV data path. Default: docs\audits\profile-audit-YYYY-MM-DD.csv
          --temp-dir <path>          Temporary encode directory. Default: artifacts\profile-audit\tmp
          --ffmpeg <path>            ffmpeg executable path. Default: ffmpeg
          --ffprobe <path>           ffprobe executable path. Default: ffprobe
          --quality-profile <name>   Quality profile. Supported: high, default, low. Default: default
          --autosample-mode <mode>   accurate | fast | hybrid. Default: scenario-default
          --include <text>           Relative-path substring filter. Can be repeated.
          --full-encode <mode>       none | native-only | all. Default: native-only
          --help                     Show this help and exit.

        Notes:
          - The tool inspects source files, resolves video-settings diagnostics directly via Transcode.Core,
            writes report files into the repo, and deletes temporary encode outputs after metric collection.
          - Source folder names are mapped as: anime -> anime, films -> film, mult -> mult.
        """;

    public static AuditOptions Parse(IReadOnlyList<string> args)
    {
        var showHelp = false;
        string? sourceRoot = null;
        string? reportPath = null;
        string? csvPath = null;
        string? tempDirectory = null;
        string ffmpegPath = "ffmpeg";
        string ffprobePath = "ffprobe";
        var qualityProfile = "default";
        string? autoSampleMode = null;
        var includeFilters = new List<string>();
        var fullEncodeMode = "native-only";

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            switch (token)
            {
                case "--source-root":
                    sourceRoot = ReadValue(args, ref index, token);
                    break;
                case "--report":
                    reportPath = ReadValue(args, ref index, token);
                    break;
                case "--csv":
                    csvPath = ReadValue(args, ref index, token);
                    break;
                case "--temp-dir":
                    tempDirectory = ReadValue(args, ref index, token);
                    break;
                case "--ffmpeg":
                    ffmpegPath = ReadValue(args, ref index, token);
                    break;
                case "--ffprobe":
                    ffprobePath = ReadValue(args, ref index, token);
                    break;
                case "--quality-profile":
                    qualityProfile = ReadValue(args, ref index, token).Trim().ToLowerInvariant();
                    break;
                case "--autosample-mode":
                    autoSampleMode = ReadValue(args, ref index, token).Trim().ToLowerInvariant();
                    break;
                case "--include":
                    includeFilters.Add(ReadValue(args, ref index, token));
                    break;
                case "--full-encode":
                    fullEncodeMode = ReadValue(args, ref index, token).Trim().ToLowerInvariant();
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {token}");
            }
        }

        if (showHelp)
        {
            return new AuditOptions(true, string.Empty, string.Empty, string.Empty, string.Empty, ffmpegPath, ffprobePath, qualityProfile, autoSampleMode, includeFilters, fullEncodeMode);
        }

        sourceRoot ??= DefaultSourceRoot;
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var todayStamp = DateTimeOffset.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        reportPath ??= Path.Combine(repoRoot, "docs", "audits", $"profile-audit-{todayStamp}.md");
        csvPath ??= Path.Combine(repoRoot, "docs", "audits", $"profile-audit-{todayStamp}.csv");
        tempDirectory ??= Path.Combine(repoRoot, "artifacts", "profile-audit", "tmp");

        sourceRoot = Path.GetFullPath(sourceRoot);
        reportPath = Path.GetFullPath(reportPath);
        csvPath = Path.GetFullPath(csvPath);
        tempDirectory = Path.GetFullPath(tempDirectory);

        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Source root not found: {sourceRoot}");
        }

        if (!VideoSettingsRequest.IsSupportedQualityProfile(qualityProfile))
        {
            throw new ArgumentOutOfRangeException(nameof(qualityProfile), $"Supported values: {string.Join(", ", VideoSettingsRequest.SupportedQualityProfiles)}.");
        }

        if (autoSampleMode is not null &&
            !VideoSettingsRequest.IsSupportedAutoSampleMode(autoSampleMode))
        {
            throw new ArgumentOutOfRangeException(nameof(autoSampleMode), $"Supported values: {string.Join(", ", VideoSettingsRequest.SupportedAutoSampleModes)}.");
        }

        if (fullEncodeMode is not ("none" or "native-only" or "all"))
        {
            throw new ArgumentOutOfRangeException(nameof(fullEncodeMode), "Supported values: none, native-only, all.");
        }

        return new AuditOptions(false, sourceRoot, reportPath, csvPath, tempDirectory, ffmpegPath, ffprobePath, qualityProfile, autoSampleMode, includeFilters, fullEncodeMode);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
        {
            throw new ArgumentException($"{optionName} requires a value.");
        }

        index++;
        return args[index];
    }

    public bool MatchesIncludeFilters(string relativePath)
    {
        if (IncludeFilters.Count == 0)
        {
            return true;
        }

        return IncludeFilters.Any(filter => relativePath.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record AuditCaseResult(
    string RelativePath,
    string? SourceContainer,
    string? SourceVideoCodec,
    int SourceHeight,
    double? SourceFramesPerSecond,
    double? SourceDurationSeconds,
    decimal? SourceTotalBitrateMbps,
    decimal? SourceVideoBitrateMbps,
    string? ContentProfile,
    string? QualityProfile,
    string CaseKind,
    int TargetHeight,
    bool SupportsDownscale,
    int? ProfileTargetHeight,
    string? AutoSampleMode,
    string? AutoSamplePath,
    string? AutoSampleReason,
    int? AutoSampleIterations,
    string? AutoSampleWindows,
    string Corridor,
    decimal? LastReductionPercent,
    bool? InBounds,
    int? BaseCq,
    decimal? BaseMaxrateMbps,
    decimal? BaseBufsizeMbps,
    string? BaseAlgorithm,
    int? ResolvedCq,
    decimal? ResolvedMaxrateMbps,
    decimal? ResolvedBufsizeMbps,
    string? ResolvedAlgorithm,
    FullEncodeMetrics? FullEncode,
    string? SkipReason)
{
    public static AuditCaseResult CreateSkipped(
        string relativePath,
        string skipReason,
        string caseKind = "skip",
        int targetHeight = 0,
        string? contentProfile = null)
    {
        return new AuditCaseResult(
            RelativePath: relativePath,
            SourceContainer: null,
            SourceVideoCodec: null,
            SourceHeight: 0,
            SourceFramesPerSecond: null,
            SourceDurationSeconds: null,
            SourceTotalBitrateMbps: null,
            SourceVideoBitrateMbps: null,
            ContentProfile: contentProfile,
            QualityProfile: null,
            CaseKind: caseKind,
            TargetHeight: targetHeight,
            SupportsDownscale: false,
            ProfileTargetHeight: null,
            AutoSampleMode: null,
            AutoSamplePath: null,
            AutoSampleReason: null,
            AutoSampleIterations: null,
            AutoSampleWindows: null,
            Corridor: "-",
            LastReductionPercent: null,
            InBounds: null,
            BaseCq: null,
            BaseMaxrateMbps: null,
            BaseBufsizeMbps: null,
            BaseAlgorithm: null,
            ResolvedCq: null,
            ResolvedMaxrateMbps: null,
            ResolvedBufsizeMbps: null,
            ResolvedAlgorithm: null,
            FullEncode: null,
            SkipReason: skipReason);
    }
}

internal sealed record FullEncodeMetrics(
    decimal? OutputVideoBitrateMbps,
    decimal? OutputTotalBitrateMbps,
    decimal? VideoReductionPercent,
    decimal? TotalReductionPercent,
    double? OutputSizeMegabytes,
    string? Failure);

internal sealed record MediaProbeResult(long? TotalBitrate, long? VideoBitrate, double? DurationSeconds, int AudioStreamCount);

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed record SummaryGroupKey(string CaseKind, int TargetHeight, string ContentProfile);

internal sealed record SummaryRow(
    string CaseKind,
    int TargetHeight,
    string ContentProfile,
    int RunCount,
    int InBoundsCount,
    int EstimateOnlyCount,
    int SampleBackedCount,
    decimal? AverageLastReductionPercent,
    decimal? AverageResolvedMaxrateMbps,
    decimal? AverageFullVideoReductionPercent,
    string Note);
