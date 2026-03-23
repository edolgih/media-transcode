using Microsoft.Extensions.Logging;
using Transcode.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.Videos;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Builds executable commands for the <c>toh264rife</c> scenario.
/// </summary>
public sealed class ToH264RifeTool
{
    private const string BatchFramePattern = "%%08d.png";
    private readonly ILogger<ToH264RifeTool> _logger;

    public ToH264RifeTool(string ffmpegPath, string? rifeNcnnPath, ILogger<ToH264RifeTool> logger)
    {
        FfmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
        RifeNcnnPath = rifeNcnnPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string FfmpegPath { get; }

    public string? RifeNcnnPath { get; }

    internal ScenarioExecution BuildExecution(SourceVideo video, ToH264RifeDecision decision)
    {
        ArgumentNullException.ThrowIfNull(video);
        ArgumentNullException.ThrowIfNull(decision);

        var sourceContainer = video.FileExtension.TrimStart('.');
        var finalOutputPath = FfmpegExecutionLayout.ResolveFinalOutputPath(decision.OutputPath);
        var workingOutputPath = FfmpegExecutionLayout.ResolveWorkingOutputPath(
            video.FilePath,
            video.FileNameWithoutExtension,
            decision.KeepSource,
            finalOutputPath);

        if (!decision.RequiresInterpolation &&
            sourceContainer.Equals(decision.TargetContainer, StringComparison.OrdinalIgnoreCase))
        {
            return new ScenarioExecution(Array.Empty<string>());
        }

        var commands = new List<string>();
        if (!decision.RequiresInterpolation)
        {
            commands.Add(BuildRemuxCommand(video, decision, workingOutputPath));
            FfmpegExecutionLayout.AppendPostOperations(commands, video.FilePath, decision.KeepSource, workingOutputPath, finalOutputPath);
            return new ScenarioExecution(commands);
        }

        if (string.IsNullOrWhiteSpace(RifeNcnnPath))
        {
            throw new InvalidOperationException($"{Cli.ToH264RifeCliConfigurationKeys.RifeNcnnPath} must be configured for toh264rife execution.");
        }

        var inputFramesDirectory = Path.Combine(
            Path.GetDirectoryName(workingOutputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(workingOutputPath)}_rife_in");
        var outputFramesDirectory = Path.Combine(
            Path.GetDirectoryName(workingOutputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(workingOutputPath)}_rife_out");

        commands.Add($"rmdir /s /q {FfmpegExecutionLayout.Quote(inputFramesDirectory)} 2>nul || ver > nul");
        commands.Add($"rmdir /s /q {FfmpegExecutionLayout.Quote(outputFramesDirectory)} 2>nul || ver > nul");
        commands.Add($"mkdir {FfmpegExecutionLayout.Quote(inputFramesDirectory)}");
        commands.Add($"mkdir {FfmpegExecutionLayout.Quote(outputFramesDirectory)}");
        commands.Add(BuildExtractFramesCommand(video, inputFramesDirectory));
        commands.Add(BuildRifeCommand(video, decision, inputFramesDirectory, outputFramesDirectory));
        commands.Add(BuildEncodeCommand(video, decision, outputFramesDirectory, workingOutputPath));
        commands.Add($"rmdir /s /q {FfmpegExecutionLayout.Quote(inputFramesDirectory)}");
        commands.Add($"rmdir /s /q {FfmpegExecutionLayout.Quote(outputFramesDirectory)}");
        FfmpegExecutionLayout.AppendPostOperations(commands, video.FilePath, decision.KeepSource, workingOutputPath, finalOutputPath);
        return new ScenarioExecution(commands);
    }

    private string BuildRemuxCommand(SourceVideo video, ToH264RifeDecision decision, string outputPath)
    {
        var parts = new List<string>
        {
            FfmpegExecutionLayout.CommandToken(FfmpegPath),
            "-hide_banner",
            "-i",
            FfmpegExecutionLayout.Quote(video.FilePath),
            "-map 0:v:0",
            "-c:v copy",
            "-map 0:a?",
            "-c:a copy",
            "-sn"
        };

        if (decision.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-movflags +faststart");
        }

        parts.Add(FfmpegExecutionLayout.Quote(outputPath));
        return string.Join(" ", parts);
    }

    private string BuildExtractFramesCommand(SourceVideo video, string inputFramesDirectory)
    {
        return string.Join(" ",
            FfmpegExecutionLayout.CommandToken(FfmpegPath),
            "-hide_banner",
            "-i",
            FfmpegExecutionLayout.Quote(video.FilePath),
            "-map 0:v:0",
            FfmpegExecutionLayout.Quote(Path.Combine(inputFramesDirectory, BatchFramePattern)));
    }

    private string BuildRifeCommand(SourceVideo video, ToH264RifeDecision decision, string inputFramesDirectory, string outputFramesDirectory)
    {
        var targetFrameCount = Math.Max(
            2,
            (int)Math.Round(video.Duration.TotalSeconds * decision.ResolvedTargetFramesPerSecond, MidpointRounding.AwayFromZero));

        return string.Join(" ",
            FfmpegExecutionLayout.CommandToken(RifeNcnnPath!),
            "-i",
            FfmpegExecutionLayout.Quote(inputFramesDirectory),
            "-o",
            FfmpegExecutionLayout.Quote(outputFramesDirectory),
            "-n",
            targetFrameCount.ToString(CultureInfo.InvariantCulture),
            "-m rife-v4",
            $"-f {BatchFramePattern}");
    }

    private string BuildEncodeCommand(SourceVideo video, ToH264RifeDecision decision, string outputFramesDirectory, string outputPath)
    {
        var parts = new List<string>
        {
            FfmpegExecutionLayout.CommandToken(FfmpegPath),
            "-hide_banner",
            "-framerate",
            decision.ResolvedTargetFramesPerSecond.ToString("0.###", CultureInfo.InvariantCulture),
            "-i",
            FfmpegExecutionLayout.Quote(Path.Combine(outputFramesDirectory, BatchFramePattern)),
            "-i",
            FfmpegExecutionLayout.Quote(video.FilePath),
            "-map 0:v:0",
            "-c:v h264_nvenc",
            "-preset p6",
            "-pix_fmt yuv420p",
            "-map 1:a?",
            "-c:a copy",
            "-sn",
            "-max_muxing_queue_size 4096"
        };

        if (decision.TargetContainer.Equals("mp4", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-movflags +faststart");
        }

        parts.Add(FfmpegExecutionLayout.Quote(outputPath));
        return string.Join(" ", parts);
    }
}
