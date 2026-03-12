using MediaTranscodeEngine.Cli.Parsing;
using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Scenarios.ToH264Gpu;
using MediaTranscodeEngine.Runtime.VideoSettings;
using MediaTranscodeEngine.Runtime.Videos;
using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Scenarios;

/*
Это CLI-адаптер для сценария toh264gpu.
Он валидирует и интерпретирует свои аргументы, строит runtime-request и переводит ошибки в короткие legacy-style маркеры.
*/
/// <summary>
/// Implements the CLI contract for the legacy <c>toh264gpu</c> application scenario.
/// </summary>
internal sealed class ToH264GpuCliScenarioHandler : ICliScenarioHandler
{
    private readonly ToH264GpuInfoFormatter _infoFormatter;

    public ToH264GpuCliScenarioHandler(ToH264GpuInfoFormatter infoFormatter)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
    }

    public string Name => "toh264gpu";

    public IReadOnlyList<string> LegacyCommandTokens => ["toh264gpu"];

    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input."),
        new CliHelpOption($"--downscale <{DownscaleRequest.SupportedTargetHeightsHelpDisplay}>", "GPU downscale when the source is higher than the target."),
        new CliHelpOption("--keep-fps", "Keep the source FPS in downscale mode instead of capping to 30000/1001."),
        new CliHelpOption("--content-profile <anime|mult|film>", "Quality-oriented content profile."),
        new CliHelpOption("--quality-profile <high|default|low>", "Quality-oriented quality profile."),
        new CliHelpOption("--autosample-mode <accurate|fast|hybrid>", "Autosample mode."),
        new CliHelpOption("--downscale-algo <bicubic|lanczos|bilinear>", "Downscale interpolation algorithm."),
        new CliHelpOption("--cq <1..51>", "Explicit CQ override."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s."),
        new CliHelpOption("--nvenc-preset <p1..p7>", "Explicit NVENC preset override."),
        new CliHelpOption("--denoise", "Enable denoise in normal encode mode."),
        new CliHelpOption("--sync-audio", "Use the explicit audio-sync repair path."),
        new CliHelpOption("--mkv", "Write MKV instead of MP4.")
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

    public bool TryValidate(IReadOnlyList<string> args, out string? errorText)
    {
        return ToH264GpuRequest.TryParseArgs(args, out _, out errorText);
    }

    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ToH264GpuRequest.TryParseArgs(request.ScenarioArgs, out var runtimeRequest, out var errorText))
        {
            throw new InvalidOperationException(errorText ?? "Invalid toh264gpu arguments.");
        }

        return new ToH264GpuScenario(runtimeRequest);
    }

    public string FormatInfo(CliTranscodeRequest request, SourceVideo video, TranscodePlan plan)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(plan);

        return _infoFormatter.Format(video, plan);
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

        if (exception.Message.Contains("video stream", StringComparison.OrdinalIgnoreCase))
        {
            return new CliScenarioFailure(
                LogLevel.Warning,
                "no_video_stream",
                $"REM Нет видеопотока: {fileName}",
                _infoFormatter.FormatFailure(request.InputPath, exception));
        }

        if (exception.Message.Contains("ffprobe", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("video probe", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("streams", StringComparison.OrdinalIgnoreCase))
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

}
