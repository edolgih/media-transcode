using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Failures;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Scenarios.ToH264Gpu.Cli;

/*
Это CLI-адаптер для сценария toh264gpu.
Он использует scenario-local parser для raw argv, строит scenario request и переводит ошибки в короткие legacy-style маркеры.
*/
/// <summary>
/// Implements the CLI contract for the legacy <c>toh264gpu</c> application scenario.
/// </summary>
public sealed class ToH264GpuCliScenarioHandler : ICliScenarioHandler
{
    private readonly ToH264GpuInfoFormatter _infoFormatter;
    private readonly ToH264GpuFfmpegTool _ffmpegTool;

    /// <summary>
    /// Initializes the CLI handler for the <c>toh264gpu</c> scenario.
    /// </summary>
    /// <param name="infoFormatter">Formatter used for failure markers.</param>
    public ToH264GpuCliScenarioHandler(ToH264GpuInfoFormatter infoFormatter)
        : this(
            infoFormatter,
            new ToH264GpuFfmpegTool("ffmpeg", NullLogger<ToH264GpuFfmpegTool>.Instance))
    {
    }

    /// <summary>
    /// Initializes the CLI handler for the <c>toh264gpu</c> scenario.
    /// </summary>
    /// <param name="infoFormatter">Formatter used for failure markers.</param>
    /// <param name="ffmpegTool">Concrete ffmpeg renderer passed into created scenarios.</param>
    public ToH264GpuCliScenarioHandler(
        ToH264GpuInfoFormatter infoFormatter,
        ToH264GpuFfmpegTool ffmpegTool)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
        _ffmpegTool = ffmpegTool ?? throw new ArgumentNullException(nameof(ffmpegTool));
    }

    public string Name => "toh264gpu";

    public IReadOnlyList<string> LegacyCommandTokens => ["toh264gpu"];

    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input. Default: off."),
        new CliHelpOption($"--downscale <{CliValueFormatter.FormatAlternatives(DownscaleRequest.SupportedTargetHeights)}>", "GPU downscale when the source is higher than the target. Default: off."),
        new CliHelpOption("--keep-fps", "Keep the source FPS in downscale mode instead of capping to 30000/1001. Default: off."),
        new CliHelpOption($"--content-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedContentProfiles)}>", "Quality-oriented content profile. Default: film."),
        new CliHelpOption($"--quality-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedQualityProfiles)}>", "Quality-oriented quality profile. Default: default."),
        new CliHelpOption($"--downscale-algo <{CliValueFormatter.FormatAlternatives(DownscaleRequest.SupportedAlgorithms)}>", "Downscale interpolation algorithm. Default: profile default; built-in profiles currently bilinear."),
        new CliHelpOption("--cq <1..51>", "Explicit CQ override. Default: resolved profile value."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s. Default: resolved profile value."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s. Default: resolved profile value."),
        new CliHelpOption($"--nvenc-preset <{CliValueFormatter.FormatAlternatives(NvencPresetOptions.SupportedPresets)}>", $"Explicit NVENC preset override. Default: {NvencPresetOptions.DefaultPreset}."),
        new CliHelpOption("--denoise", "Enable denoise in normal encode mode. Default: off."),
        new CliHelpOption("--sync-audio", "Use the explicit audio-sync repair path. Default: off."),
        new CliHelpOption("--mkv", "Write MKV instead of MP4. Default: off (MP4).")
    ];

    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        return
        [
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.m4v",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --keep-source --sync-audio",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --content-profile film --quality-profile default"
        ];
    }

    public bool TryParse(IReadOnlyList<string> args, out object scenarioInput, out string? errorText)
    {
        if (ToH264GpuCliRequestParser.TryParse(args, out var runtimeRequest, out errorText))
        {
            scenarioInput = runtimeRequest;
            return true;
        }

        scenarioInput = null!;
        return false;
    }

    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var runtimeRequest = GetRuntimeRequest(request);
        return new ToH264GpuScenario(runtimeRequest, _ffmpegTool);
    }

    public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(exception);

        var fileName = Path.GetFileName(request.InputPath);
        if (exception is IOException or UnauthorizedAccessException)
        {
            return new CliScenarioFailure(
                LogLevel.Error,
                "io_error",
                $"REM I/O error: {fileName}",
                $"{fileName}: [i/o error]");
        }

        if (exception is RuntimeFailureException runtimeFailure &&
            runtimeFailure.Code == RuntimeFailureCode.NoVideoStream)
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "no_video_stream",
                $"REM Нет видеопотока: {fileName}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        if (exception is RuntimeFailureException downscaleFailure &&
            downscaleFailure.Code == RuntimeFailureCode.DownscaleSourceBucketIssue)
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "downscale_source_bucket",
                $"REM {downscaleFailure.Message}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        if (exception is RuntimeFailureException probeFailure &&
            probeFailure.Code.IsProbeFailure())
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "probe_failure",
                $"REM ffprobe failed: {fileName}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        return new CliScenarioFailure(
            LogLevel.Warning,
            "unexpected_failure",
            $"REM Unexpected failure: {fileName}",
            $"{fileName}: [unexpected failure]");
    }

    private static ToH264GpuRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        return request.ScenarioInput as ToH264GpuRequest
               ?? throw new InvalidOperationException(
                   $"CLI request for scenario '{request.ScenarioName}' does not carry a valid toh264gpu input.");
    }

}
