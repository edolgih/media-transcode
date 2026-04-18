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

    /*
    Это упрощенный конструктор для стандартного ffmpeg-пути.
    */
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

    /*
    Это полный конструктор CLI-обработчика с явным ffmpeg-tool.
    */
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

    /*
    Это каноническое имя сценария для выбора обработчика.
    */
    /// <summary>
    /// Gets the canonical scenario name handled by this CLI adapter.
    /// </summary>
    public string Name => "toh264gpu";

    /*
    Это legacy-командные токены, которые также активируют сценарий.
    */
    /// <summary>
    /// Gets legacy command tokens that map to this scenario.
    /// </summary>
    public IReadOnlyList<string> LegacyCommandTokens => ["toh264gpu"];

    /*
    Это список поддерживаемых scenario-опций для help-вывода CLI.
    */
    /// <summary>
    /// Gets scenario-specific CLI options displayed in help output.
    /// </summary>
    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input. Default: off."),
        new CliHelpOption("--force-encode", "Force full encode even for remux-compatible video, keeping the source resolution. Default: off."),
        new CliHelpOption($"--downscale <{CliValueFormatter.FormatAlternatives(DownscaleRequest.SupportedTargetHeights)}>", "GPU downscale when the source is higher than the target. Default: off."),
        new CliHelpOption("--keep-fps", "Keep the source FPS in downscale mode instead of capping to 30000/1001. Default: off."),
        new CliHelpOption($"--content-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedContentProfiles)}>", "Quality-oriented content profile. Default: film."),
        new CliHelpOption($"--quality-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedQualityProfiles)}>", "Quality-oriented quality profile. Default: default."),
        new CliHelpOption($"--downscale-algo <{CliValueFormatter.FormatAlternatives(DownscaleRequest.SupportedAlgorithms)}>", "Downscale interpolation algorithm. Default: profile default; built-in profiles currently bilinear."),
        new CliHelpOption("--cq <1..51>", "Explicit CQ override. Default: resolved profile value."),
        new CliHelpOption("--maxrate <number>", "Explicit VBV maxrate in Mbit/s. Default: resolved profile value."),
        new CliHelpOption("--bufsize <number>", "Explicit VBV bufsize in Mbit/s. Default: resolved profile value."),
        new CliHelpOption($"--nvenc-preset <{CliValueFormatter.FormatAlternatives(NvencPreset.SupportedValues)}>", $"Explicit NVENC preset override. Default: {NvencPreset.Default}."),
        new CliHelpOption("--denoise", "Enable denoise in normal encode mode. Default: off."),
        new CliHelpOption("--sync-audio", "Use the explicit audio-sync repair path. Default: off."),
        new CliHelpOption("--mkv", "Write MKV instead of MP4. Default: off (MP4).")
    ];

    /*
    Это примеры команд запуска сценария для help.
    */
    /// <summary>
    /// Builds scenario-specific command examples for help output.
    /// </summary>
    /// <param name="exeName">Executable name to use in examples.</param>
    /// <returns>Example command lines for this scenario.</returns>
    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        return
        [
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.m4v",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --keep-source --sync-audio",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --force-encode --content-profile film --quality-profile default",
            $"{exeName} --scenario toh264gpu --input C:\\video\\input.mkv --content-profile film --quality-profile default"
        ];
    }

    /*
    Это попытка разобрать raw args в scenario-specific input.
    */
    /// <summary>
    /// Parses raw scenario arguments into normalized scenario input.
    /// </summary>
    /// <param name="args">Scenario-specific raw arguments.</param>
    /// <param name="scenarioInput">Normalized scenario input on success.</param>
    /// <param name="errorText">Validation or parsing error text.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
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

    /*
    Это создание runtime-сценария на основе нормализованного CLI запроса.
    */
    /// <summary>
    /// Creates a runtime <see cref="ToH264GpuScenario"/> for the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI transcode request.</param>
    /// <returns>Configured scenario instance.</returns>
    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var runtimeRequest = GetRuntimeRequest(request);
        return new ToH264GpuScenario(runtimeRequest, _ffmpegTool);
    }

    /*
    Это классификация исключения в legacy-совместимый формат ошибок CLI.
    */
    /// <summary>
    /// Maps processing exceptions to scenario-specific CLI failure output.
    /// </summary>
    /// <param name="request">Per-input CLI transcode request.</param>
    /// <param name="exception">Exception raised while processing the input.</param>
    /// <returns>Scenario-specific CLI failure representation.</returns>
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

    /*
    Это извлечение strongly-typed scenario request из общего CLI контейнера.
    */
    /// <summary>
    /// Extracts the typed scenario request from generic CLI request payload.
    /// </summary>
    /// <param name="request">Per-input CLI transcode request.</param>
    /// <returns>Typed <see cref="ToH264GpuRequest"/> payload.</returns>
    private static ToH264GpuRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        return request.ScenarioInput as ToH264GpuRequest
               ?? throw new InvalidOperationException(
                   $"CLI request for scenario '{request.ScenarioName}' does not carry a valid toh264gpu input.");
    }

}
