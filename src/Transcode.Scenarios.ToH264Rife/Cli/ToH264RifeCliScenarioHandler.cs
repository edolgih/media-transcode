using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Parsing;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Failures;
using Transcode.Core.Scenarios;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/// <summary>
/// Implements the CLI contract for the <c>toh264rife</c> scenario.
/// </summary>
public sealed class ToH264RifeCliScenarioHandler : ICliScenarioHandler
{
    private readonly ToH264RifeInfoFormatter _infoFormatter;
    private readonly ToH264RifeTool _tool;

    public ToH264RifeCliScenarioHandler(ToH264RifeInfoFormatter infoFormatter, ToH264RifeTool tool)
    {
        _infoFormatter = infoFormatter ?? throw new ArgumentNullException(nameof(infoFormatter));
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    public string Name => "toh264rife";

    public IReadOnlyList<string> LegacyCommandTokens => ["toh264rife"];

    public IReadOnlyList<CliHelpOption> HelpOptions =>
    [
        new CliHelpOption("--keep-source", "Keep the source file instead of replacing it when output path matches the input. Default: off."),
        new CliHelpOption($"--target-fps <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedTargetFrameRates)}>", "Explicit target FPS. Default: 2x with scenario-side normalization to exact cadence."),
        new CliHelpOption($"--container <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedContainers)}>", "Explicit target container. Default: keep source container when it is mp4 or mkv; otherwise mp4.")
    ];

    public IReadOnlyList<string> GetHelpExamples(string exeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exeName);

        return
        [
            $"{exeName} --scenario toh264rife --input C:\\video\\input.mkv",
            $"{exeName} --scenario toh264rife --input C:\\video\\input.mkv --target-fps 60 --keep-source",
            $"{exeName} --scenario toh264rife --input C:\\video\\input.avi --container mp4"
        ];
    }

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

    public TranscodeScenario CreateScenario(CliTranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ToH264RifeScenario(GetRuntimeRequest(request), _tool);
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

    private static ToH264RifeRequest GetRuntimeRequest(CliTranscodeRequest request)
    {
        return request.ScenarioInput as ToH264RifeRequest
               ?? throw new InvalidOperationException(
                   $"CLI request for scenario '{request.ScenarioName}' does not carry a valid toh264rife input.");
    }
}
