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
        new CliHelpOption($"--fps-multiplier <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedFramesPerSecondMultipliers)}>", "Frame-rate multiplier for interpolation. Supported values: 2 or 3. Default: 2."),
        new CliHelpOption($"--interp-quality <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedInterpolationQualityProfiles)}>", "Interpolation model quality profile. Default: default."),
        new CliHelpOption($"--content-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedContentProfiles)}>", "Quality-oriented content profile for the final NVENC encode. Default: film."),
        new CliHelpOption($"--quality-profile <{CliValueFormatter.FormatAlternatives(VideoSettingsRequest.SupportedQualityProfiles)}>", "Quality-oriented quality profile for the final NVENC encode. Default: default."),
        new CliHelpOption($"--container <{CliValueFormatter.FormatAlternatives(ToH264RifeRequest.SupportedContainers)}>", "Explicit target container. Default: keep source container when it is mp4 or mkv; otherwise mp4.")
    ];

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

    public IReadOnlyList<string> GetConfigurationDisplayRows(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return
        [
            $"{ToH264RifeCliConfigurationKeys.DockerImage} current: {GetRequiredValue(configuration, ToH264RifeCliConfigurationKeys.DockerImage)}"
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
