using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Failures;
using Transcode.Core.Scenarios;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/*
Это CLI-адаптер сценария toh264rife.
Он связывает scenario-local parser, создание сценария и преобразование исключений в стабильный CLI-вывод.
*/
/// <summary>
/// Implements the CLI contract for the <c>toh264rife</c> scenario.
/// </summary>
public sealed class ToH264RifeCliScenarioHandler : ICliScenarioHandler
{
    private readonly ToH264RifeInfoFormatter _infoFormatter;
    private readonly ToH264RifeTool _tool;

    /*
    Это конструктор CLI-адаптера сценария.
    */
    /// <summary>
    /// Initializes the CLI scenario handler.
    /// </summary>
    /// <param name="infoFormatter">Formatter used for info and failure output.</param>
    /// <param name="tool">Tool adapter used by created scenarios.</param>
    public ToH264RifeCliScenarioHandler(ToH264RifeInfoFormatter infoFormatter, ToH264RifeTool tool)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    /*
    Это стабильное имя сценария для параметра --scenario.
    */
    /// <summary>
    /// Gets the scenario name used by CLI routing.
    /// </summary>
    public string Name => "toh264rife";

    /*
    Это список legacy-команд, которые больше не должны использоваться напрямую.
    */
    /// <summary>
    /// Gets legacy command tokens that redirect users to <c>--scenario</c>.
    /// </summary>
    public IReadOnlyList<string> LegacyCommandTokens => ["toh264rife"];

    /*
    Это help-опции, специфичные для toh264rife.
    */
    /// <summary>
    /// Gets scenario-specific help options for CLI output.
    /// </summary>
    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input. Default: off."),
        new CliHelpOption($"--fps-multiplier <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedFramesPerSecondMultipliers)}>", "Frame-rate multiplier for interpolation. Supported values: 2 or 3. Default: 2."),
        new CliHelpOption($"--interp-quality <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedInterpolationQualityProfiles)}>", "Interpolation model quality profile. Default: default."),
        new CliHelpOption($"--content-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedContentProfiles)}>", "Quality-oriented content profile for the final NVENC encode. Default: film."),
        new CliHelpOption($"--quality-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedQualityProfiles)}>", "Quality-oriented quality profile for the final NVENC encode. Default: default."),
        new CliHelpOption($"--container <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedContainers)}>", "Explicit target container. Default: keep source container when it is mp4 or mkv; otherwise mp4.")
    ];

    /*
    Это примеры запуска сценария для help-текста.
    */
    /// <summary>
    /// Returns scenario-specific command examples for help output.
    /// </summary>
    /// <param name="exeName">Executable name used in examples.</param>
    /// <returns>Example command lines.</returns>
    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        return
        [
            $"{exeName} --scenario toh264rife --input C:\\video\\input.mkv",
            $"{exeName} --scenario toh264rife --input C:\\video\\input.mkv --fps-multiplier 3 --keep-source",
            $"{exeName} --scenario toh264rife --input C:\\video\\input.mkv --interp-quality high --content-profile anime --quality-profile high",
            $"{exeName} --scenario toh264rife --input C:\\video\\input.avi --container mp4"
        ];
    }

    /*
    Это отображение runtime-конфигурации сценария для help-вывода.
    */
    /// <summary>
    /// Returns scenario-specific configuration rows for help output.
    /// </summary>
    /// <param name="configuration">Resolved CLI configuration.</param>
    /// <returns>Configuration display rows.</returns>
    public IReadOnlyList<string> GetConfigurationDisplayRows(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return
        [
            $"{ToH264RifeCliConfigurationKeys.DockerImage} current: {GetRequiredValue(configuration, ToH264RifeCliConfigurationKeys.DockerImage)}"
        ];
    }

    /*
    Это разбор scenario-специфичных CLI-аргументов в типизированный input.
    */
    /// <summary>
    /// Parses scenario-specific CLI arguments.
    /// </summary>
    /// <param name="args">Scenario-specific raw CLI tokens.</param>
    /// <param name="scenarioInput">Parsed scenario input on success.</param>
    /// <param name="errorText">Validation error message on failure.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public bool TryParse(IReadOnlyList<string> args, out object scenarioInput, out string? errorText)
    {
        if (ToH264RifeCliRequestParser.TryParse(args, out var runtimeRequest, out errorText))
        {
            scenarioInput = runtimeRequest;
            return true;
        }

        scenarioInput = null!;
        return false;
    }

    /*
    Это создание экземпляра сценария для обработки конкретного входного файла.
    */
    /// <summary>
    /// Creates a scenario instance for the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Configured scenario instance.</returns>
    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ToH264RifeScenario(GetRuntimeRequest(request), _tool);
    }

    /*
    Это преобразование исключения в стабильный CLI-результат.
    */
    /// <summary>
    /// Maps processing exceptions to scenario-specific CLI failure output.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <param name="exception">Exception raised during processing.</param>
    /// <returns>Scenario-specific failure descriptor.</returns>
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
    Это безопасное извлечение типизированного request из универсального контейнера CLI.
    */
    /// <summary>
    /// Extracts and validates the typed scenario request from a generic CLI request payload.
    /// </summary>
    private static ToH264RifeRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        return request.ScenarioInput as ToH264RifeRequest
               ?? throw new InvalidOperationException(
                   $"CLI request for scenario '{request.ScenarioName}' does not carry a valid toh264rife input.");
    }

    /*
    Это чтение обязательного значения конфигурации.
    */
    /// <summary>
    /// Reads a required configuration value or throws when it is missing.
    /// </summary>
    private static string GetRequiredValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration key '{key}' is required for toh264rife.");
        }

        return value;
    }
}
