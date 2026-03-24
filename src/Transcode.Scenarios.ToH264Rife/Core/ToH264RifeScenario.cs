using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/// <summary>
/// Represents the <c>toh264rife</c> interpolation scenario.
/// </summary>
public sealed class ToH264RifeScenario : TranscodeScenario
{
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
        var resolvedVideoSettings = VideoSettingsDefaultsResolver.ResolveEncodeDefaults(
            outputHeight: video.Height,
            sourceHeight: video.Height,
            request: Request.VideoSettings);
        var interpolationModelName = ToH264RifeRequest.ResolveInterpolationModelName(Request.InterpolationQualityProfile);
        var resolvedTargetFramesPerSecond = video.FramesPerSecond * Request.FramesPerSecondMultiplier;
        var userFacingTargetFramesPerSecond = (int)Math.Round(
            resolvedTargetFramesPerSecond,
            MidpointRounding.AwayFromZero);
        VideoIntent videoIntent = new EncodeVideoIntent(
            TargetVideoCodec: "h264",
            PreferredBackend: "gpu",
            CompatibilityProfile: H264OutputProfile.H264High,
            TargetFramesPerSecond: resolvedTargetFramesPerSecond,
            UseFrameInterpolation: true,
            VideoSettings: Request.VideoSettings,
            EncoderPreset: "p6");

        return new ToH264RifeDecision(
            targetContainer: targetContainer,
            video: videoIntent,
            audio: new CopyAudioIntent(),
            keepSource: Request.KeepSource,
            outputPath: ResolveOutputPath(video, targetContainer, userFacingTargetFramesPerSecond),
            interpolationQualityProfile: Request.InterpolationQualityProfile,
            interpolationModelName: interpolationModelName,
            resolvedVideoSettings: resolvedVideoSettings,
            resolvedTargetFramesPerSecond: resolvedTargetFramesPerSecond,
            userFacingTargetFramesPerSecond: userFacingTargetFramesPerSecond,
            framesPerSecondMultiplier: Request.FramesPerSecondMultiplier);
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
        return new ToH264RifeTool(
            "media-transcode-rife-trt",
            NullLogger<ToH264RifeTool>.Instance);
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

    private string ResolveOutputPath(
        SourceVideo video,
        string targetContainer,
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

        return Path.Combine(
            directory,
            $"{FormatKeepSourceInterpolationFileName(video.FileNameWithoutExtension, userFacingTargetFramesPerSecond)}.{targetContainer}");
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
