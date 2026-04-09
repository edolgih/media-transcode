using Microsoft.Extensions.Logging;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это tool-адаптер сценария toh264rife.
Он преобразует итоговое решение сценария в docker-команду запуска интерполяции и post-операции над файлами.
*/
/// <summary>
/// Builds execution commands for the <c>toh264rife</c> scenario.
/// </summary>
public sealed class ToH264RifeTool
{
    private const string TrtCacheVolumeName = "media-transcode-rife-trt-cache";
    private const string SourceCacheVolumeName = "media-transcode-rife-src-cache";
    private const string DockerCommandName = "docker";

    /*
    Это конструктор tool-адаптера.
    Он принимает образ docker, в котором выполняется интерполяция.
    */
    /// <summary>
    /// Initializes a tool adapter for docker-based frame interpolation.
    /// </summary>
    public ToH264RifeTool(
        string dockerImage,
        ILogger<ToH264RifeTool> logger)
    {
        DockerImage = dockerImage ?? throw new ArgumentNullException(nameof(dockerImage));
        _ = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /*
    Это имя docker-образа, который будет использован для запуска.
    */
    /// <summary>
    /// Gets the docker image name used to run interpolation.
    /// </summary>
    public string DockerImage { get; }

    /*
    Это сборка полного набора команд выполнения.
    На выходе одна docker-команда и, при необходимости, post-операции для переименования/замены файла.
    */
    /// <summary>
    /// Builds the full command sequence required to execute the scenario.
    /// </summary>
    internal ScenarioExecution BuildExecution(SourceVideo video, ToH264RifeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(
            video.FilePath,
            video.FileNameWithoutExtension,
            decision.KeepSource,
            finalOutputPath);

        var commands = new List<string>
        {
            BuildDockerCommand(video, decision, workingOutputPath)
        };

        FfmpegExecutionLayout.AppendPostOperations(commands, video.FilePath, decision.KeepSource, workingOutputPath, finalOutputPath);
        return new ScenarioExecution(commands);
    }

    /*
    Это рендер docker-команды интерполяции с нужными томами и параметрами.
    */
    /// <summary>
    /// Builds the docker command that runs the interpolation pipeline.
    /// </summary>
    private string BuildDockerCommand(SourceVideo video, ToH264RifeDecision decision, string outputPath)
    {
        var workingDirectory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            workingDirectory = ".";
        }

        var inputContainerPath = $"/workspace/work/{Path.GetFileName(video.FilePath)}";
        var outputContainerPath = $"/workspace/work/{Path.GetFileName(outputPath)}";
        var parts = new List<string>
        {
            DockerCommandName,
            "run --rm --gpus all",
            "-v",
            FfmpegExecutionLayout.Quote($"{workingDirectory}:/workspace/work"),
            "-v",
            $"{TrtCacheVolumeName}:/workspace/cache/trt",
            "-v",
            $"{SourceCacheVolumeName}:/workspace/cache/src",
            DockerImage,
            FfmpegExecutionLayout.Quote(inputContainerPath),
            FfmpegExecutionLayout.Quote(outputContainerPath),
            decision.FramesPerSecondMultiplier.ToString(),
            decision.TargetContainer,
            decision.InterpolationModelName,
            decision.ResolvedVideoSettings.Cq.ToString(CultureInfo.InvariantCulture),
            decision.ResolvedVideoSettings.MaxrateKbps.ToString(CultureInfo.InvariantCulture),
            decision.ResolvedVideoSettings.BufsizeKbps.ToString(CultureInfo.InvariantCulture)
        };

        return string.Join(" ", parts);
    }
}
