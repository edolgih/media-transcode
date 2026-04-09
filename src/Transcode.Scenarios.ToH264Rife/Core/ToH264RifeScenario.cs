using Microsoft.Extensions.Logging.Abstractions;
using Transcode.Core.MediaIntent;
using Transcode.Core.Scenarios;
using Transcode.Core.Videos;
using Transcode.Core.VideoSettings;
using System.Globalization;

namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это прикладной сценарий toh264rife.
Он решает параметры интерполяции кадров, финальные video-settings и путь результата, после чего передает решение в tool-слой.
*/
/// <summary>
/// Implements the frame-interpolation scenario <c>toh264rife</c>.
/// </summary>
public sealed class ToH264RifeScenario : TranscodeScenario
{
    private const decimal X2InterpolationMaxrateUplift = 0.4m;
    private const decimal X3InterpolationMaxrateUplift = 0.8m;

    private static readonly VideoSettingsResolver VideoSettingsResolver = new(VideoSettingsProfiles.Default);
    private static readonly ToH264RifeInfoFormatter InfoFormatter = new();
    private readonly ToH264RifeTool _tool;

    /*
    Это создание сценария с настройками по умолчанию.
    */
    /// <summary>
    /// Initializes the scenario with default request settings.
    /// </summary>
    public ToH264RifeScenario()
        : this(new ToH264RifeRequest(), CreateDefaultTool())
    {
    }

    /*
    Это создание сценария с заданным request и стандартным tool.
    */
    /// <summary>
    /// Initializes the scenario with a supplied request and default tool.
    /// </summary>
    /// <param name="request">Scenario-specific request directives.</param>
    public ToH264RifeScenario(ToH264RifeRequest request)
        : this(request, CreateDefaultTool())
    {
    }

    /*
    Это основной конструктор сценария.
    */
    /// <summary>
    /// Initializes the scenario with a supplied request and concrete tool adapter.
    /// </summary>
    /// <param name="request">Scenario-specific request directives.</param>
    /// <param name="tool">Tool adapter that renders execution commands.</param>
    public ToH264RifeScenario(ToH264RifeRequest request, ToH264RifeTool tool)
        : base("toh264rife")
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
    }

    /*
    Это входные параметры сценария.
    */
    /// <summary>
    /// Gets the scenario request used to build decisions.
    /// </summary>
    public ToH264RifeRequest Request { get; }

    /*
    Это сборка итогового решения сценария по фактам входного видео.
    */
    /// <summary>
    /// Builds the final scenario decision for the supplied source video.
    /// </summary>
    /// <param name="video">Normalized source video facts.</param>
    /// <returns>Resolved decision used by info and execution paths.</returns>
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

    /*
    Это выбор финальных video-settings из общей подсистемы профилей.
    */
    /// <summary>
    /// Resolves final video settings for the supplied source video and optional overrides.
    /// </summary>
    private static ToH264RifeVideoSettings ResolveVideoSettings(SourceVideo video, VideoSettingsRequest? request)
    {
        var resolution = VideoSettingsResolver.Resolve(new VideoSettingsResolutionContext(
            SourceHeight: video.Height,
            OutputHeight: Math.Max(1, video.Height),
            SourceBitrate: SourceVideoBitrateResolver.ResolveVideoBitrateHintOrEstimate(video),
            VideoSettings: request));

        return ToH264RifeVideoSettings.FromResolvedSettings(resolution.Settings);
    }

    /*
    Это дополнительный uplift для maxrate и bufsize при интерполяции.
    Он применяется только если пользователь не зафиксировал эти поля вручную.
    */
    /// <summary>
    /// Applies interpolation-specific uplift to maxrate and bufsize when they were not set explicitly.
    /// </summary>
    private static ToH264RifeVideoSettings ApplyInterpolationRateUplift(
        ToH264RifeVideoSettings baseSettings,
        VideoSettingsRequest? request,
        int framesPerSecondMultiplier)
    {
        // Явные значения maxrate и bufsize пользователя менять нельзя.
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

    /*
    Это info-вывод сценария.
    */
    /// <summary>
    /// Formats scenario info output for the supplied video.
    /// </summary>
    protected override string FormatInfoCore(SourceVideo video)
    {
        return InfoFormatter.Format(video, BuildDecision(video));
    }

    /*
    Это сборка команд выполнения сценария.
    */
    /// <summary>
    /// Builds execution commands for the supplied video.
    /// </summary>
    protected override ScenarioExecution BuildExecutionCore(SourceVideo video)
    {
        return _tool.BuildExecution(video, BuildDecision(video));
    }

    /*
    Это создание tool-адаптера по умолчанию.
    */
    /// <summary>
    /// Creates the default tool adapter.
    /// </summary>
    private static ToH264RifeTool CreateDefaultTool()
    {
        return new ToH264RifeTool(
            "media-transcode-rife-trt",
            NullLogger<ToH264RifeTool>.Instance);
    }

    /*
    Это выбор контейнера результата.
    Он учитывает явный override из запроса и fallback на контейнер источника либо mp4.
    */
    /// <summary>
    /// Resolves the target container from request overrides and source facts.
    /// </summary>
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

    /*
    Это построение итогового пути выходного файла.
    */
    /// <summary>
    /// Resolves the final output file path for interpolation output.
    /// </summary>
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

    /*
    Это добавление либо обновление суффикса вида "60fps" в имени файла.
    */
    /// <summary>
    /// Adds or updates a trailing fps token in the output file name.
    /// </summary>
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

    /*
    Это замена подходящего токена с сохранением его позиции среди соседних токенов.
    */
    /// <summary>
    /// Replaces a matching token while preserving its relative position.
    /// </summary>
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

    /*
    Это разбор завершающего блока в скобках как списка токенов.
    */
    /// <summary>
    /// Tries to parse trailing parenthesized tokens from a file name.
    /// </summary>
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

    /*
    Это проверка, является ли токен суффиксом FPS, например "60fps".
    */
    /// <summary>
    /// Determines whether a token is an fps suffix such as <c>60fps</c>.
    /// </summary>
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
