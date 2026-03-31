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
    private const decimal X2InterpolationMaxrateUplift = 0.4m;
    private const decimal X3InterpolationMaxrateUplift = 0.8m;

    private static readonly VideoSettingsResolver VideoSettingsResolver = new(VideoSettingsProfiles.Default);
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
        var baseResolvedVideoSettings = ResolveVideoSettings(video, Request.VideoSettings);
        var resolvedVideoSettings = ApplyInterpolationRateUplift(
            baseResolvedVideoSettings,
            Request.VideoSettings,
            Request.FramesPerSecondMultiplier);
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

    private static ResolvedVideoSettingsDefaults ResolveVideoSettings(SourceVideo video, VideoSettingsRequest? request)
    {
        var resolution = VideoSettingsResolver.ResolveForEncode(
            request: request,
            outputHeight: Math.Max(1, video.Height),
            sourceHeight: video.Height);
        var sourceBitrate = SourceVideoBitrateResolver.ResolveVideoBitrateHintOrEstimate(video);
        var useFixedBucketQuality = FixedBucketVideoSettingsPolicy.ShouldUseFixedBucketQuality(
            VideoSettingsProfiles.Default,
            useDownscale: false,
            downscaleRequest: null,
            videoHeight: video.Height,
            request: request);
        var settings = useFixedBucketQuality
            ? FixedBucketVideoSettingsPolicy.ApplySourceBitrateCap(
                resolution.Settings,
                sourceBitrate,
                request,
                resolution.Profile.RateModel.BufsizeMultiplier)
            : resolution.Settings;

        return new ResolvedVideoSettingsDefaults(
            ContentProfile: resolution.EffectiveSelection.ContentProfile,
            QualityProfile: resolution.EffectiveSelection.QualityProfile,
            Cq: settings.Cq,
            Maxrate: settings.Maxrate,
            Bufsize: settings.Bufsize);
    }

    private static ResolvedVideoSettingsDefaults ApplyInterpolationRateUplift(
        ResolvedVideoSettingsDefaults baseSettings,
        VideoSettingsRequest? request,
        int framesPerSecondMultiplier)
    {
        if (request?.Maxrate.HasValue == true || request?.Bufsize.HasValue == true)
        {
            return baseSettings;
        }

        var maxrateUplift = framesPerSecondMultiplier switch
        {
            >= 3 => X3InterpolationMaxrateUplift,
            >= 2 => X2InterpolationMaxrateUplift,
            _ => 0m
        };

        if (maxrateUplift <= 0m)
        {
            return baseSettings;
        }

        var maxrate = baseSettings.Maxrate + maxrateUplift;
        var bufsize = baseSettings.Bufsize + (maxrateUplift * 2m);
        return baseSettings with { Maxrate = maxrate, Bufsize = bufsize };
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

        return Path.Combine(
            directory,
            $"{FormatInterpolationFileName(video.FileNameWithoutExtension, userFacingTargetFramesPerSecond)}.{targetContainer}");
    }

    private static string FormatInterpolationFileName(string fileNameWithoutExtension, int userFacingTargetFramesPerSecond)
    {
        var suffix = $"{userFacingTargetFramesPerSecond.ToString(CultureInfo.InvariantCulture)}fps";
        if (TryParseTrailingParenthesizedTokens(fileNameWithoutExtension, out var prefix, out var tokens))
        {
            ReplaceTokenPreservingPosition(tokens, IsFramesPerSecondSuffixToken, suffix);
            return $"{prefix} ({string.Join(", ", tokens)})";
        }

        return $"{fileNameWithoutExtension} ({suffix})";
    }

    private static void ReplaceTokenPreservingPosition(List<string> tokens, Predicate<string> match, string replacement)
    {
        var insertIndex = tokens.FindIndex(match);
        tokens.RemoveAll(match);
        if (insertIndex < 0 || insertIndex > tokens.Count)
        {
            tokens.Add(replacement);
            return;
        }

        tokens.Insert(insertIndex, replacement);
    }

    private static bool TryParseTrailingParenthesizedTokens(
        string fileNameWithoutExtension,
        out string prefix,
        out List<string> tokens)
    {
        prefix = fileNameWithoutExtension;
        tokens = [];

        if (!fileNameWithoutExtension.EndsWith(")", StringComparison.Ordinal))
        {
            return false;
        }

        var openParenthesis = fileNameWithoutExtension.LastIndexOf('(');
        if (openParenthesis <= 0)
        {
            return false;
        }

        prefix = fileNameWithoutExtension[..openParenthesis].TrimEnd();
        var tokenPayload = fileNameWithoutExtension.Substring(
            openParenthesis + 1,
            fileNameWithoutExtension.Length - openParenthesis - 2);
        tokens = tokenPayload
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return true;
    }

    private static bool IsFramesPerSecondSuffixToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.EndsWith("fps", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numericPart = token[..^3];
        return int.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) &&
               parsed > 0;
    }
}
