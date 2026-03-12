using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Scenarios.ToMkvGpu;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это CLI-адаптер для сценария tomkvgpu.
Он знает свои опции, валидирует их, строит runtime-request и переводит ошибки в legacy-compatible вывод.
*/
/// <summary>
/// Implements the CLI contract for the legacy <c>tomkvgpu</c> application scenario.
/// </summary>
internal sealed class ToMkvGpuCliScenarioHandler : ICliScenarioHandler
{
    private readonly ToMkvGpuInfoFormatter _infoFormatter;

    /// <summary>
    /// Initializes the CLI handler for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="infoFormatter">Formatter used for info-mode output.</param>
    public ToMkvGpuCliScenarioHandler(ToMkvGpuInfoFormatter infoFormatter)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
    }

    public string Name => "tomkvgpu";

    public IReadOnlyList<string> LegacyCommandTokens { get; } = ["tomkvgpu"];

    public IReadOnlyList<CliHelpOption> HelpOptions { get; } =
    [
        new CliHelpOption($"--downscale <{DownscaleRequest.SupportedTargetHeightsHelpDisplay}>", "Downscale target height."),
        new CliHelpOption("--keep-source", "Keep source file and write output to a new path."),
        new CliHelpOption("--overlay-bg", "Apply overlay background path during encode."),
        new CliHelpOption("--max-fps <50|40|30|24>", "Optional frame-rate cap. Supported values: 50, 40, 30, 24."),
        new CliHelpOption("--sync-audio", "Force sync-safe audio path."),
        new CliHelpOption("--content-profile <anime|mult|film>", "Downscale profile content kind."),
        new CliHelpOption("--quality-profile <high|default|low>", "Downscale profile quality kind."),
        new CliHelpOption("--autosample-mode <accurate|fast|hybrid>", "Downscale autosample mode."),
        new CliHelpOption("--downscale-algo <bilinear|bicubic|lanczos>", "Explicit downscale algorithm override."),
        new CliHelpOption("--cq <int>", "Explicit NVENC CQ override."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s."),
        new CliHelpOption("--nvenc-preset <preset>", "Explicit NVENC preset override.")
    ];

    /// <summary>
    /// Gets command examples for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="exeName">Executable name used in rendered examples.</param>
    /// <returns>Scenario-specific command examples.</returns>
    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        return
        [
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\"",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --info",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --keep-source --downscale 720",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --overlay-bg --sync-audio",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --max-fps 50",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --downscale 720 --content-profile film --quality-profile default",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --downscale 576 --content-profile film --quality-profile default",
            $"{exeName} --scenario tomkvgpu --input \"C:\\video\\movie.mkv\" --downscale 480 --content-profile film --quality-profile default",
            $"Get-ChildItem -Recurse *.mp4 | ForEach-Object FullName | {exeName} --scenario tomkvgpu --info"
        ];
    }

    /// <summary>
    /// Validates raw scenario-specific arguments for the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="args">Raw scenario-specific arguments.</param>
    /// <param name="errorText">Error message on failure.</param>
    /// <returns><see langword="true"/> when the arguments are valid; otherwise <see langword="false"/>.</returns>
    public bool TryValidate(IReadOnlyList<string> args, out string? errorText)
    {
        return ToMkvGpuRequest.TryParseArgs(args, out _, out errorText);
    }

    /// <summary>
    /// Creates the runtime <see cref="ToMkvGpuScenario"/> instance for the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Runtime scenario instance.</returns>
    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        return new ToMkvGpuScenario(GetRuntimeRequest(request));
    }

    /// <summary>
    /// Formats info-mode output for a successfully built <c>tomkvgpu</c> plan.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="video">Inspected source video facts.</param>
    /// <param name="plan">Built transcode plan.</param>
    /// <returns>Info-mode output line.</returns>
    public string FormatInfo(CliTranscodeRequest request, SourceVideo video, TranscodePlan plan)
    {
        return _infoFormatter.Format(video, plan);
    }

    /// <summary>
    /// Maps a processing exception to legacy-compatible CLI failure output for <c>tomkvgpu</c>.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="exception">Exception raised during processing.</param>
    /// <returns>Scenario-specific failure description.</returns>
    public CliScenarioFailure DescribeFailure(CliTranscodeRequest request, Exception exception)
    {
        var runtimeRequest = GetRuntimeRequest(request);
        var failure = ClassifyFailure(runtimeRequest, exception);
        var fileName = Path.GetFileName(request.InputPath);
        var infoOutput = failure.Kind switch
        {
            HandledFailureKind.IoError => $"{fileName}: [i/o error]",
            HandledFailureKind.UnexpectedFailure => $"{fileName}: [unexpected failure]",
            _ => _infoFormatter.FormatFailure(request.InputPath, exception)
        };
        var nonInfoOutput = failure.Kind switch
        {
            HandledFailureKind.UnknownDimensionsOverlay => $"REM Unknown dimensions: {fileName}",
            HandledFailureKind.NoVideoStream => $"REM Нет видеопотока: {fileName}",
            HandledFailureKind.DownscaleSourceBucket => $"REM {exception.Message}",
            HandledFailureKind.ProbeFailure => $"REM ffprobe failed: {fileName}",
            HandledFailureKind.IoError => $"REM I/O error: {fileName}",
            HandledFailureKind.UnexpectedFailure => $"REM Unexpected failure: {fileName}",
            _ => throw new InvalidOperationException($"Unhandled failure kind '{failure.Kind}'.")
        };

        return new CliScenarioFailure(failure.Level, failure.LogToken, nonInfoOutput, infoOutput);
    }

    private static ToMkvGpuRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (ToMkvGpuRequest.TryParseArgs(request.ScenarioArgs, out var runtimeRequest, out var errorText))
        {
            return runtimeRequest;
        }

        throw new InvalidOperationException(
            $"CLI request for scenario '{request.ScenarioName}' is invalid: {errorText}");
    }

    private static HandledFailure ClassifyFailure(ToMkvGpuRequest request, Exception exception)
    {
        if (exception is IOException or UnauthorizedAccessException)
        {
            return new HandledFailure(HandledFailureKind.IoError, "io_error", LogLevel.Error);
        }

        var message = exception.Message;
        if (request.OverlayBackground &&
            (message.Contains("valid video width", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("valid video height", StringComparison.OrdinalIgnoreCase)))
        {
            return new HandledFailure(HandledFailureKind.UnknownDimensionsOverlay, "unknown_dimensions", LogLevel.Warning);
        }

        if (message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.NoVideoStream, "no_video_stream", LogLevel.Warning);
        }

        if (message.Contains("source bucket missing", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("source bucket invalid", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.DownscaleSourceBucket, "downscale_source_bucket", LogLevel.Warning);
        }

        if (message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("streams", StringComparison.OrdinalIgnoreCase))
        {
            return new HandledFailure(HandledFailureKind.ProbeFailure, "probe_failure", LogLevel.Warning);
        }

        return new HandledFailure(HandledFailureKind.UnexpectedFailure, "unexpected_failure", LogLevel.Warning);
    }

    private enum HandledFailureKind
    {
        UnknownDimensionsOverlay,
        NoVideoStream,
        DownscaleSourceBucket,
        ProbeFailure,
        IoError,
        UnexpectedFailure
    }

    private sealed class HandledFailure
    {
        public HandledFailure(HandledFailureKind kind, string logToken, LogLevel level)
        {
            Kind = kind;
            LogToken = logToken ?? throw new ArgumentNullException(nameof(logToken));
            Level = level;
        }

        public HandledFailureKind Kind { get; }

        public string LogToken { get; }

        public LogLevel Level { get; }
    }
}
