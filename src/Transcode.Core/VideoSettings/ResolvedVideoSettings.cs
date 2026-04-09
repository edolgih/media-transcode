namespace Transcode.Core.VideoSettings;

/*
Это итоговый набор видеонастроек после выбора профиля и применения override.
Именно эти значения потом идут в исполнение и в построение ffmpeg-параметров.
*/
/// <summary>
/// Represents the final video settings that should be used for execution.
/// </summary>
sealed record ResolvedVideoSettings(
    VideoContentProfile ContentProfile,
    VideoQualityProfile QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize,
    VideoScaleAlgorithm Algorithm,
    int CqMin,
    int CqMax,
    decimal MaxrateMin,
    decimal MaxrateMax)
{
    private const decimal MinimumPositiveMaxrateMbps = 0.001m;

    /*
    Это конструктор из строковых идентификаторов.
    Он удобен для тестов и описания значений без ручного создания value object'ов.
    */
    /// <summary>
    /// Initializes resolved settings from string identifiers.
    /// </summary>
    public ResolvedVideoSettings(
        string ContentProfile,
        string QualityProfile,
        int Cq,
        decimal Maxrate,
        decimal Bufsize,
        string Algorithm,
        int CqMin,
        int CqMax,
        decimal MaxrateMin,
        decimal MaxrateMax)
        : this(
            VideoContentProfile.Parse(ContentProfile, nameof(ContentProfile)),
            VideoQualityProfile.Parse(QualityProfile, nameof(QualityProfile)),
            Cq,
            Maxrate,
            Bufsize,
            VideoScaleAlgorithm.Parse(Algorithm, nameof(Algorithm)),
            CqMin,
            CqMax,
            MaxrateMin,
            MaxrateMax)
    {
    }

    /*
    Это фактически выбранный профиль контента.
    */
    /// <summary>
    /// Gets the resolved content profile.
    /// </summary>
    public VideoContentProfile ContentProfile { get; init; } = ContentProfile ?? throw new ArgumentNullException(nameof(ContentProfile));

    /*
    Это фактически выбранный профиль качества.
    */
    /// <summary>
    /// Gets the resolved quality profile.
    /// </summary>
    public VideoQualityProfile QualityProfile { get; init; } = QualityProfile ?? throw new ArgumentNullException(nameof(QualityProfile));

    /*
    Это итоговый CQ, с которым пойдет кодирование.
    */
    /// <summary>
    /// Gets the resolved CQ value.
    /// </summary>
    public int Cq { get; init; } = Cq > 0
        ? Cq
        : throw new ArgumentOutOfRangeException(nameof(Cq), Cq, "CQ must be greater than zero.");

    /*
    Это итоговый maxrate в мегабитах в секунду.
    */
    /// <summary>
    /// Gets the resolved maxrate in Mbps.
    /// </summary>
    public decimal Maxrate { get; init; } = Maxrate > 0m
        ? Maxrate
        : throw new ArgumentOutOfRangeException(nameof(Maxrate), Maxrate, "Maxrate must be greater than zero.");

    /*
    Это итоговый размер буфера кодека.
    */
    /// <summary>
    /// Gets the resolved bufsize in Mbps.
    /// </summary>
    public decimal Bufsize { get; init; } = Bufsize > 0m
        ? Bufsize
        : throw new ArgumentOutOfRangeException(nameof(Bufsize), Bufsize, "Bufsize must be greater than zero.");

    /*
    Это итоговый алгоритм масштабирования.
    Он уже учитывает дефолт профиля и возможный override из downscale-запроса.
    */
    /// <summary>
    /// Gets the resolved scaling algorithm.
    /// </summary>
    public VideoScaleAlgorithm Algorithm { get; init; } = Algorithm ?? throw new ArgumentNullException(nameof(Algorithm));

    /*
    Это нижняя допустимая граница CQ для текущего результата.
    */
    /// <summary>
    /// Gets the minimum allowed CQ for the resolved settings.
    /// </summary>
    public int CqMin { get; init; } = CqMin > 0
        ? CqMin
        : throw new ArgumentOutOfRangeException(nameof(CqMin), CqMin, "CQ minimum must be greater than zero.");

    /*
    Это верхняя допустимая граница CQ.
    */
    /// <summary>
    /// Gets the maximum allowed CQ for the resolved settings.
    /// </summary>
    public int CqMax { get; init; } = CqMax >= CqMin
        ? CqMax
        : throw new ArgumentOutOfRangeException(nameof(CqMax), CqMax, "CQ maximum must be greater than or equal to minimum.");

    /*
    Это нижняя допустимая граница maxrate после применения bucket-правил.
    */
    /// <summary>
    /// Gets the minimum allowed maxrate in Mbps.
    /// </summary>
    public decimal MaxrateMin { get; init; } = MaxrateMin > 0m
        ? MaxrateMin
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMin), MaxrateMin, "Maxrate minimum must be greater than zero.");

    /*
    Это верхняя допустимая граница maxrate.
    */
    /// <summary>
    /// Gets the maximum allowed maxrate in Mbps.
    /// </summary>
    public decimal MaxrateMax { get; init; } = MaxrateMax >= MaxrateMin
        ? MaxrateMax
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMax), MaxrateMax, "Maxrate maximum must be greater than or equal to minimum.");

    /*
    Это преобразование строки defaults в полноценный итоговый объект.
    Оно сохраняет все значения, но переводит их в форму, с которой дальше работает резолвер.
    */
    /// <summary>
    /// Creates resolved settings from a defaults row.
    /// </summary>
    public static ResolvedVideoSettings FromDefaults(VideoSettingsDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        return new ResolvedVideoSettings(
            defaults.ContentProfile,
            defaults.QualityProfile,
            defaults.Cq,
            defaults.Maxrate,
            defaults.Bufsize,
            defaults.Algorithm,
            defaults.CqMin,
            defaults.CqMax,
            defaults.MaxrateMin,
            defaults.MaxrateMax);
    }

    /*
    Это применение локального ограничения границ для конкретного bucket'а источника.
    Сами рабочие значения CQ, maxrate и bufsize не меняются, меняются только пределы.
    */
    /// <summary>
    /// Applies a source-bucket bounds override to the current settings.
    /// </summary>
    public ResolvedVideoSettings ApplyBoundsOverride(VideoSettingsBoundsOverride? boundsOverride)
    {
        return boundsOverride is null
            ? this
            : new ResolvedVideoSettings(
                ContentProfile,
                QualityProfile,
                Cq,
                Maxrate,
                Bufsize,
                Algorithm,
                boundsOverride.CqMin ?? CqMin,
                boundsOverride.CqMax ?? CqMax,
                boundsOverride.MaxrateMin ?? MaxrateMin,
                boundsOverride.MaxrateMax ?? MaxrateMax);
    }

    /*
    Это создание копии с новыми итоговыми значениями.
    Метод используют, когда резолвер применяет пользовательские override или автоматические ограничения.
    */
    /// <summary>
    /// Returns a copy with updated values and optional bounds.
    /// </summary>
    public ResolvedVideoSettings ApplyOverrides(
        int cq,
        decimal maxrate,
        decimal bufsize,
        VideoScaleAlgorithm? algorithm = null,
        int? cqMin = null,
        int? cqMax = null,
        decimal? maxrateMin = null,
        decimal? maxrateMax = null)
    {
        return new ResolvedVideoSettings(
            ContentProfile,
            QualityProfile,
            cq,
            maxrate,
            bufsize,
            algorithm ?? Algorithm,
            cqMin ?? CqMin,
            cqMax ?? CqMax,
            maxrateMin ?? MaxrateMin,
            maxrateMax ?? MaxrateMax);
    }

    /*
    Это автоматическое ограничение битрейта значением источника.
    Оно срабатывает только когда пользователь сам не зафиксировал rate-поля вручную.
    */
    /// <summary>
    /// Caps maxrate and bufsize by the source bitrate when no manual rate overrides are present.
    /// </summary>
    public ResolvedVideoSettings CapToSourceBitrate(
        long? sourceVideoBitrate,
        VideoSettingsRequest? request,
        decimal bufsizeMultiplier)
    {
        // Явные rate override пользователя имеют приоритет над автоматическим ограничением.
        if (request?.HasManualRateOverrides == true ||
            !sourceVideoBitrate.HasValue ||
            sourceVideoBitrate.Value <= 0)
        {
            return this;
        }

        var sourceBitrateMaxrate = sourceVideoBitrate.Value / 1_000_000m;
        if (sourceBitrateMaxrate <= 0m)
        {
            return this;
        }

        var cappedMaxrate = Math.Min(Maxrate, sourceBitrateMaxrate);
        if (cappedMaxrate >= Maxrate)
        {
            return this;
        }

        cappedMaxrate = Math.Max(
            MinimumPositiveMaxrateMbps,
            decimal.Round(cappedMaxrate, 3, MidpointRounding.AwayFromZero));
        var cappedBufsize = Math.Max(
            MinimumPositiveMaxrateMbps,
            decimal.Round(cappedMaxrate * bufsizeMultiplier, 3, MidpointRounding.AwayFromZero));

        return ApplyOverrides(
            cq: Cq,
            maxrate: cappedMaxrate,
            bufsize: cappedBufsize);
    }
}
