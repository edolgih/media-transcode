using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это резолвер итоговых видеонастроек.
Он берет профиль по целевой высоте, подмешивает пользовательские override и выдает финальные значения для выполнения.
*/
/// <summary>
/// Resolves final video settings from configured profiles and user requests.
/// </summary>
sealed class VideoSettingsResolver
{
    private readonly VideoSettingsProfiles _profiles;

    /*
    Это конструктор резолвера с уже подготовленным набором профилей.
    */
    /// <summary>
    /// Initializes a resolver that works with the supplied profile set.
    /// </summary>
    public VideoSettingsResolver(VideoSettingsProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /*
    Это общий вход в резолв настроек.
    Метод сам понимает, нужен ли обычный encode или отдельный сценарий explicit downscale.
    */
    /// <summary>
    /// Resolves final video settings for the supplied resolution context.
    /// </summary>
    public ProfileDrivenVideoSettingsResolution Resolve(VideoSettingsResolutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolution = context.Downscale is not null
            ? ResolveForDownscale(
                request: context.Downscale,
                videoSettings: context.VideoSettings,
                sourceHeight: context.SourceHeight)
            : ResolveForEncode(
                request: context.VideoSettings,
                outputHeight: context.OutputHeight,
                sourceHeight: context.SourceHeight);

        return resolution.WithSettings(
            resolution.Settings.CapToSourceBitrate(
                context.SourceBitrate,
                context.VideoSettings,
            resolution.Profile.RateModel.BufsizeMultiplier));
    }

    /*
    Это выбор настроек для обычного кодирования без явного downscale-запроса.
    Профиль подбирается по высоте результата.
    */
    /// <summary>
    /// Resolves settings for regular encoding without an explicit downscale request.
    /// </summary>
    private ProfileDrivenVideoSettingsResolution ResolveForEncode(
        VideoSettingsRequest? request,
        int outputHeight,
        int? sourceHeight = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        var profile = _profiles.ResolveOutputProfile(outputHeight);
        var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, request);
        return ResolveCore(
            profile,
            effectiveSelection,
            request,
            algorithmOverride: null,
            sourceHeightForDefaults: sourceHeight);
    }

    /*
    Это выбор настроек для явного downscale.
    Здесь профиль определяется строго по целевой высоте downscale, а алгоритм может прийти прямо из запроса.
    */
    /// <summary>
    /// Resolves settings for an explicit downscale request.
    /// </summary>
    private ProfileDrivenVideoSettingsResolution ResolveForDownscale(
        DownscaleRequest request,
        VideoSettingsRequest? videoSettings,
        int sourceHeight)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        var profile = _profiles.GetRequiredProfile(request.TargetHeight);
        var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, videoSettings);
        var algorithmOverride = request.Algorithm;
        return ResolveCore(
            profile,
            effectiveSelection,
            videoSettings,
            algorithmOverride,
            sourceHeightForDefaults: sourceHeight);
    }

    /*
    Это внутренний общий пайплайн резолва.
    Он получает базовые defaults из профиля и затем применяет поверх них все override.
    */
    /// <summary>
    /// Builds default settings first and then applies all effective overrides.
    /// </summary>
    private ProfileDrivenVideoSettingsResolution ResolveCore(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection effectiveSelection,
        VideoSettingsRequest? request,
        VideoScaleAlgorithm? algorithmOverride,
        int? sourceHeightForDefaults)
    {
        var baseSettings = profile.ResolveDefaults(sourceHeightForDefaults, effectiveSelection);
        var settings = ApplyOverrides(baseSettings, effectiveSelection, request, profile, sourceHeightForDefaults, algorithmOverride);
        return new ProfileDrivenVideoSettingsResolution(profile, effectiveSelection, baseSettings, settings);
    }

    /*
    Это определение фактически выбранных профилей контента и качества.
    После этого шага пустых значений уже не остается: либо пришел override, либо взят профильный default.
    */
    /// <summary>
    /// Determines the effective content and quality profile selection.
    /// </summary>
    private static EffectiveVideoSettingsSelection BuildEffectiveVideoSettingsSelection(
        VideoSettingsProfile profile,
        VideoSettingsRequest? request)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new EffectiveVideoSettingsSelection(
            request?.ContentProfile is null
                ? profile.DefaultContentProfile
                : request.ContentProfile,
            request?.QualityProfile is null
                ? profile.DefaultQualityProfile
                : request.QualityProfile);
    }

    /*
    Это применение пользовательских override к базовым значениям профиля.
    Здесь же при необходимости пересчитываются rate-границы и bufsize.
    */
    /// <summary>
    /// Applies user overrides and recalculates rate bounds when required.
    /// </summary>
    private static ResolvedVideoSettings ApplyOverrides(
        ResolvedVideoSettings defaults,
        EffectiveVideoSettingsSelection effectiveSelection,
        VideoSettingsRequest? request,
        VideoSettingsProfile profile,
        int? sourceHeightForDefaults,
        VideoScaleAlgorithm? algorithmOverride)
    {
        var cq = request?.Cq ?? defaults.Cq;
        var maxrate = request?.Maxrate;
        var hasManualCq = request?.Cq.HasValue == true;
        var hasManualMaxrate = request?.Maxrate.HasValue == true;
        var resolvedMaxrateMin = defaults.MaxrateMin;
        var resolvedMaxrateMax = defaults.MaxrateMax;

        // При ручном CQ границы maxrate сдвигаются вместе с ним.
        if (hasManualCq)
        {
            var resolvedRateBounds = ResolveDirectionalRateBounds(
                profile,
                effectiveSelection,
                defaults,
                sourceHeightForDefaults,
                cq);
            resolvedMaxrateMin = resolvedRateBounds.MaxrateMin;
            resolvedMaxrateMax = resolvedRateBounds.MaxrateMax;

            if (!maxrate.HasValue)
            {
                maxrate = Clamp(resolvedRateBounds.Maxrate, resolvedMaxrateMin, resolvedMaxrateMax);
            }
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request?.Bufsize;
        if (!bufsize.HasValue && (hasManualMaxrate || hasManualCq))
        {
            bufsize = maxrate.Value * profile.RateModel.BufsizeMultiplier;
        }

        bufsize ??= defaults.Bufsize;

        return defaults.ApplyOverrides(
            cq: cq,
            maxrate: maxrate.Value,
            bufsize: bufsize.Value,
            algorithm: algorithmOverride,
            maxrateMin: resolvedMaxrateMin,
            maxrateMax: resolvedMaxrateMax);
    }

    /*
    Это пересчет maxrate и допустимых границ под новое значение CQ.
    Он старается сохранить логику соседних quality profile, а не просто использовать фиксированный шаг.
    */
    /// <summary>
    /// Recalculates maxrate and its bounds for a new CQ value.
    /// </summary>
    private static DirectionalRateBounds ResolveDirectionalRateBounds(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection effectiveSelection,
        ResolvedVideoSettings defaults,
        int? sourceHeightForDefaults,
        int targetCq)
    {
        var delta = defaults.Cq - targetCq;
        if (delta == 0)
        {
            return new DirectionalRateBounds(defaults.Maxrate, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        var rateModel = ResolveDirectionalRateModel(
            profile,
            effectiveSelection,
            defaults,
            sourceHeightForDefaults,
            delta > 0);
        var resolvedMaxrate = defaults.Maxrate + (delta * rateModel.MaxrateStep);
        var resolvedMaxrateMin = defaults.MaxrateMin + (delta * rateModel.MaxrateMinStep);
        var resolvedMaxrateMax = defaults.MaxrateMax + (delta * rateModel.MaxrateMaxStep);

        if (resolvedMaxrateMin > resolvedMaxrateMax)
        {
            (resolvedMaxrateMin, resolvedMaxrateMax) = (resolvedMaxrateMax, resolvedMaxrateMin);
        }

        return new DirectionalRateBounds(resolvedMaxrate, resolvedMaxrateMin, resolvedMaxrateMax);
    }

    /*
    Это выбор модели шага для пересчета bitrate.
    Сначала метод пытается опереться на соседний quality profile, а если это невозможно, берет базовый коэффициент профиля.
    */
    /// <summary>
    /// Resolves the directional step model used to recalculate bitrate values.
    /// </summary>
    private static DirectionalRateModel ResolveDirectionalRateModel(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection effectiveSelection,
        ResolvedVideoSettings defaults,
        int? sourceHeightForDefaults,
        bool towardsBetterQuality)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(effectiveSelection);
        ArgumentNullException.ThrowIfNull(defaults);

        var fallbackStep = profile.RateModel.CqStepToMaxrateStep;
        var fallbackModel = new DirectionalRateModel(
            MaxrateStep: fallbackStep,
            MaxrateMinStep: fallbackStep,
            MaxrateMaxStep: fallbackStep);

        // Сначала пробуем взять соседний профиль качества и посчитать шаг по реальным значениям.
        if (!effectiveSelection.QualityProfile.TryResolveNeighbor(towardsBetterQuality, out var neighborQualityProfile) ||
            !profile.TryResolveDefaults(sourceHeightForDefaults, effectiveSelection.ContentProfile, neighborQualityProfile, out var neighborDefaults))
        {
            return fallbackModel;
        }

        var cqDistance = Math.Abs(defaults.Cq - neighborDefaults.Cq);
        if (cqDistance <= 0)
        {
            return fallbackModel;
        }

        return new DirectionalRateModel(
            MaxrateStep: ResolveDirectionalStep(defaults.Maxrate, neighborDefaults.Maxrate, cqDistance),
            MaxrateMinStep: ResolveDirectionalStep(defaults.MaxrateMin, neighborDefaults.MaxrateMin, cqDistance),
            MaxrateMaxStep: ResolveDirectionalStep(defaults.MaxrateMax, neighborDefaults.MaxrateMax, cqDistance));
    }

    /*
    Это расчет шага для одного конкретного числового поля.
    */
    /// <summary>
    /// Calculates the step between current and neighboring values for one field.
    /// </summary>
    private static decimal ResolveDirectionalStep(decimal currentValue, decimal neighborValue, int cqDistance)
    {
        var distance = Math.Abs(currentValue - neighborValue);
        return distance > 0m
            ? distance / cqDistance
            : 0m;
    }

    /*
    Это простое ограничение значения заданным диапазоном.
    */
    /// <summary>
    /// Clamps a value to the supplied range.
    /// </summary>
    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    /*
    Это вспомогательная запись с уже пересчитанными rate-границами.
    */
    /// <summary>
    /// Stores recalculated maxrate bounds for one CQ change.
    /// </summary>
    private sealed record DirectionalRateBounds(decimal Maxrate, decimal MaxrateMin, decimal MaxrateMax);

    /*
    Это набор шагов, по которым пересчитываются maxrate и его границы.
    */
    /// <summary>
    /// Stores the directional step model used for rate recalculation.
    /// </summary>
    private sealed record DirectionalRateModel(decimal MaxrateStep, decimal MaxrateMinStep, decimal MaxrateMaxStep);
}

/*
Это полный результат резолва видеонастроек.
Он сохраняет и использованный профиль, и базовые значения, и окончательный результат после всех override.
*/
/// <summary>
/// Represents the full result of resolving video settings.
/// </summary>
sealed record ProfileDrivenVideoSettingsResolution
{
    /*
    Это конструктор полного результата резолва.
    */
    /// <summary>
    /// Initializes a full resolution result.
    /// </summary>
    public ProfileDrivenVideoSettingsResolution(
        VideoSettingsProfile profile,
        EffectiveVideoSettingsSelection effectiveSelection,
        ResolvedVideoSettings baseSettings,
        ResolvedVideoSettings settings)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        EffectiveSelection = effectiveSelection ?? throw new ArgumentNullException(nameof(effectiveSelection));
        BaseSettings = baseSettings ?? throw new ArgumentNullException(nameof(baseSettings));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /*
    Это профиль целевой высоты, который участвовал в выборе настроек.
    */
    /// <summary>
    /// Gets the target-height profile used during resolution.
    /// </summary>
    public VideoSettingsProfile Profile { get; init; }

    /*
    Это фактически выбранные профиль контента и профиль качества.
    */
    /// <summary>
    /// Gets the effective content and quality profile selection.
    /// </summary>
    public EffectiveVideoSettingsSelection EffectiveSelection { get; init; }

    /*
    Это базовые настройки профиля до применения пользовательских override.
    */
    /// <summary>
    /// Gets the base settings resolved from the profile before overrides are applied.
    /// </summary>
    public ResolvedVideoSettings BaseSettings { get; init; }

    /*
    Это итоговые настройки после всех override и автоматических ограничений.
    */
    /// <summary>
    /// Gets the final settings that should be executed.
    /// </summary>
    public ResolvedVideoSettings Settings { get; init; }

    /*
    Это способ получить копию результата с новым итоговым набором настроек.
    Удобно, когда после основного резолва добавляется еще одно ограничение.
    */
    /// <summary>
    /// Returns a copy of the result with updated final settings.
    /// </summary>
    public ProfileDrivenVideoSettingsResolution WithSettings(ResolvedVideoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return this with { Settings = settings };
    }
}

/*
Это уже выбранные профили контента и качества.
После этого этапа неопределенности больше нет: резолвер знает, какие defaults нужно брать.
*/
/// <summary>
/// Represents the effective content and quality profile selection.
/// </summary>
sealed record EffectiveVideoSettingsSelection(
    VideoContentProfile ContentProfile,
    VideoQualityProfile QualityProfile)
{
    /*
    Это конструктор выбора из строковых значений.
    Он нужен там, где профили приходят из внешнего ввода, а дальше в системе хочется работать типизированно.
    */
    /// <summary>
    /// Initializes the selection from string profile identifiers.
    /// </summary>
    public EffectiveVideoSettingsSelection(string contentProfile, string qualityProfile)
        : this(
            VideoContentProfile.Parse(contentProfile, nameof(contentProfile)),
            VideoQualityProfile.Parse(qualityProfile, nameof(qualityProfile)))
    {
    }

    /*
    Это выбранный профиль контента без пустого значения.
    */
    /// <summary>
    /// Gets the resolved content profile.
    /// </summary>
    public VideoContentProfile ContentProfile { get; init; } = ContentProfile ?? throw new ArgumentNullException(nameof(ContentProfile));

    /*
    Это выбранный профиль качества без пустого значения.
    */
    /// <summary>
    /// Gets the resolved quality profile.
    /// </summary>
    public VideoQualityProfile QualityProfile { get; init; } = QualityProfile ?? throw new ArgumentNullException(nameof(QualityProfile));
}

/*
Это входной контекст для выбора видеонастроек.
Он объединяет параметры источника, параметры результата и пользовательские запросы, чтобы резолвер видел всю картину целиком.
*/
/// <summary>
/// Represents the input data required to resolve final video settings.
/// </summary>
sealed record VideoSettingsResolutionContext
{
    /*
    Это конструктор полного контекста резолва.
    */
    /// <summary>
    /// Initializes a new resolution context.
    /// </summary>
    public VideoSettingsResolutionContext(
        int SourceHeight,
        int OutputHeight,
        long? SourceBitrate,
        VideoSettingsRequest? VideoSettings = null,
        DownscaleRequest? Downscale = null)
    {
        this.SourceHeight = SourceHeight;
        this.OutputHeight = OutputHeight > 0
            ? OutputHeight
            : throw new ArgumentOutOfRangeException(nameof(OutputHeight), OutputHeight, "Output height must be greater than zero.");
        this.SourceBitrate = SourceBitrate;
        this.VideoSettings = VideoSettings;
        this.Downscale = Downscale;
    }

    /*
    Это высота исходного видео.
    Она нужна для выбора source bucket и локальных ограничений профиля.
    */
    /// <summary>
    /// Gets the source video height.
    /// </summary>
    public int SourceHeight { get; init; }

    /*
    Это ожидаемая высота результата.
    Для обычного encode по ней выбирается ближайший профиль.
    */
    /// <summary>
    /// Gets the requested output height.
    /// </summary>
    public int OutputHeight { get; init; }

    /*
    Это битрейт исходного видео, если его удалось определить.
    Он нужен для автоматического ограничения итогового maxrate.
    */
    /// <summary>
    /// Gets the source video bitrate, if known.
    /// </summary>
    public long? SourceBitrate { get; init; }

    /*
    Это пользовательские override качества и rate-параметров.
    */
    /// <summary>
    /// Gets the optional video settings overrides requested by the user.
    /// </summary>
    public VideoSettingsRequest? VideoSettings { get; init; }

    /*
    Это явный запрос на downscale, если сценарий отдельно зафиксировал новую высоту.
    */
    /// <summary>
    /// Gets the optional explicit downscale request.
    /// </summary>
    public DownscaleRequest? Downscale { get; init; }
}
