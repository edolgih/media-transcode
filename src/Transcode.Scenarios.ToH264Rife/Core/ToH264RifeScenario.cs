using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Videos;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Represents the <c>toh264rife</c> interpolation scenario.
/// </summary>
public sealed class ToH264RifeScenario : TranscodeScenario
{
    private const double Ntcs24FramesPerSecond = 24000d / 1001d;
    private const double Ntcs30FramesPerSecond = 30000d / 1001d;
    private const double Ntcs60FramesPerSecond = 60000d / 1001d;
    private const double FrameRateNormalizationTolerance = 0.05d;
    private const double SkipToleranceFramesPerSecond = 5d;
    private static readonly ToH264RifeInfoFormatter InfoFormatter = new();
    private readonly ToH264RifeTool _tool;

    public ToH264RifeScenario()
        : this(new ToH264RifeRequest(), CreateDefaultTool())
    {
    }

    public ToH264RifeScenario(ToH264RifeRequest request)
        : this(request, CreateDefaultTool())
    {
    }

    public ToH264RifeScenario(ToH264RifeRequest request, ToH264RifeTool tool)
        : base("toh264rife")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    public ToH264RifeRequest Request { get; }

    internal ToH264RifeDecision BuildDecision(SourceVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var targetContainer = ResolveTargetContainer(video);
        var resolvedTargetFramesPerSecond = ResolveTargetFramesPerSecond(video);
        var userFacingTargetFramesPerSecond = (int)Math.Round(
            resolvedTargetFramesPerSecond,
            MidpointRounding.AwayFromZero);
        var requiresInterpolation = ShouldInterpolate(video.FramesPerSecond, resolvedTargetFramesPerSecond);
        VideoIntent videoIntent = requiresInterpolation
            ? new EncodeVideoIntent(
                TargetVideoCodec: "h264",
                PreferredBackend: "gpu",
                CompatibilityProfile: H264OutputProfile.H264High,
                TargetFramesPerSecond: resolvedTargetFramesPerSecond,
                UseFrameInterpolation: true,
                EncoderPreset: "p6")
            : new CopyVideoIntent();

        return new ToH264RifeDecision(
            targetContainer: targetContainer,
            video: videoIntent,
            audio: new CopyAudioIntent(),
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, targetContainer, requiresInterpolation, userFacingTargetFramesPerSecond),
            resolvedTargetFramesPerSecond: resolvedTargetFramesPerSecond,
            userFacingTargetFramesPerSecond: userFacingTargetFramesPerSecond);
    }

    protected override string FormatInfoCore(SourceVideo video)
    {
        return InfoFormatter.Format(video, BuildDecision(video));
    }

    protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
    {
        return _tool.BuildExecution(video, BuildDecision(video));
    }

    private static ToH264RifeTool CreateDefaultTool()
    {
        return new ToH264RifeTool("ffmpeg", "rife-ncnn-vulkan", NullLogger<ToH264RifeTool>.Instance);
    }

    private string ResolveTargetContainer(SourceVideo video)
    {
        if (!string.IsNullOrWhiteSpace(Request.OutputContainer))
        {
            return Request.OutputContainer!;
        }

        var sourceContainer = video.FileExtension.TrimStart('.');
        return ToH264RifeRequest.SupportedContainers.Contains(sourceContainer)
            ? sourceContainer
            : "mp4";
    }

    private double ResolveTargetFramesPerSecond(SourceVideo video)
    {
        if (Request.TargetFramesPerSecond.HasValue)
        {
            return Request.TargetFramesPerSecond.Value == 60 && UsesNtscCadence(video.FramesPerSecond)
                ? Ntcs60FramesPerSecond
                : Request.TargetFramesPerSecond.Value;
        }

        return video.FramesPerSecond * 2d;
    }

    private static bool UsesNtscCadence(double framesPerSecond)
    {
        return Math.Abs(framesPerSecond - Ntcs24FramesPerSecond) <= FrameRateNormalizationTolerance ||
               Math.Abs(framesPerSecond - Ntcs30FramesPerSecond) <= FrameRateNormalizationTolerance ||
               Math.Abs(framesPerSecond - Ntcs60FramesPerSecond) <= FrameRateNormalizationTolerance;
    }

    private static bool ShouldInterpolate(double sourceFramesPerSecond, double targetFramesPerSecond)
    {
        if (sourceFramesPerSecond >= targetFramesPerSecond)
        {
            return false;
        }

        return Math.Abs(sourceFramesPerSecond - targetFramesPerSecond) > SkipToleranceFramesPerSecond;
    }

    private string ResolveOutputPath(
        SourceVideo video,
        string targetContainer,
        bool requiresInterpolation,
        int userFacingTargetFramesPerSecond)
    {
        var directory = Path.GetDirectoryName(video.FilePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var outputPath = Path.Combine(directory, $"{video.FileNameWithoutExtension}.{targetContainer}");
        if (!Request.KeepSource ||
            !outputPath.Equals(video.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return outputPath;
        }

        if (requiresInterpolation)
        {
            return Path.Combine(
                directory,
                $"{FormatKeepSourceInterpolationFileName(video.FileNameWithoutExtension, userFacingTargetFramesPerSecond)}.{targetContainer}");
        }

        return Path.Combine(directory, $"{video.FileNameWithoutExtension}_out.{targetContainer}");
    }

    private static string FormatKeepSourceInterpolationFileName(string fileNameWithoutExtension, int userFacingTargetFramesPerSecond)
    {
        var suffix = $"{userFacingTargetFramesPerSecond.ToString(CultureInfo.InvariantCulture)}fps";
        if (fileNameWithoutExtension.EndsWith(")", StringComparison.Ordinal) &&
            fileNameWithoutExtension.LastIndexOf('(') >= 0)
        {
            return string.Concat(
                fileNameWithoutExtension.AsSpan(0, fileNameWithoutExtension.Length - 1),
                ", ",
                suffix,
                ")");
        }

        return $"{fileNameWithoutExtension} ({suffix})";
    }
}
