namespace Transcode.Core.VideoSettings;

/*
Это общий запрос на переопределение видеонастроек.
Он хранит только quality и rate override, но сам по себе не означает смену разрешения.
*/
/// <summary>
/// Represents user overrides for video quality and bitrate-related settings.
/// </summary>
public sealed class VideoSettingsRequest
{
    private static readonly string[] SupportedContentProfilesValues = [.. VideoContentProfile.SupportedValues];
    private static readonly string[] SupportedQualityProfilesValues = [.. VideoQualityProfile.SupportedValues];

    /*
    Это все поддерживаемые профили контента.
    Список нужен, когда сценарий хочет показать пользователю допустимые варианты.
    */
    /// <summary>
    /// Gets the content profiles that can be requested explicitly.
    /// </summary>
    public static IReadOnlyList<string> SupportedContentProfiles => SupportedContentProfilesValues;

    /*
    Это все поддерживаемые профили качества.
    Их можно использовать для валидации и подсказок при сборке запроса.
    */
    /// <summary>
    /// Gets the quality profiles that can be requested explicitly.
    /// </summary>
    public static IReadOnlyList<string> SupportedQualityProfiles => SupportedQualityProfilesValues;

    /*
    Это конструктор набора override для видеонастроек.
    Он сразу нормализует профили, проверяет числовые значения и не допускает пустой запрос без единого изменения.
    */
    /// <summary>
    /// Initializes a new set of video settings overrides.
    /// </summary>
    /// <param name="contentProfile">Requested content profile.</param>
    /// <param name="qualityProfile">Requested quality profile.</param>
    /// <param name="cq">Explicit CQ override.</param>
    /// <param name="maxrate">Explicit maxrate override in Mbps.</param>
    /// <param name="bufsize">Explicit bufsize override in Mbps.</param>
    /// <exception cref="ArgumentException">Thrown when no override value is provided.</exception>
    public VideoSettingsRequest(
        string? contentProfile = null,
        string? qualityProfile = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        if (cq.HasValue && cq.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cq), cq.Value, "CQ must be greater than zero.");
        }

        if (maxrate.HasValue && maxrate.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(maxrate), maxrate.Value, "Maxrate must be greater than zero.");
        }

        if (bufsize.HasValue && bufsize.Value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(bufsize), bufsize.Value, "Bufsize must be greater than zero.");
        }

        ContentProfile = VideoContentProfile.ParseOptional(contentProfile, nameof(contentProfile))?.Value;
        QualityProfile = VideoQualityProfile.ParseOptional(qualityProfile, nameof(qualityProfile))?.Value;
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;

        if (!HasAnyValue(ContentProfile, QualityProfile, Cq, Maxrate, Bufsize))
        {
            throw new ArgumentException("At least one video settings override is required.");
        }
    }

    /*
    Это профиль контента, который пользователь хочет применить вместо профильного значения по умолчанию.
    Пустое значение означает, что выбор остается за профилем целевой высоты.
    */
    /// <summary>
    /// Gets the requested content profile override.
    /// </summary>
    public string? ContentProfile { get; }

    /*
    Это профиль качества, который должен заменить стандартный выбор профиля.
    Если значение не задано, используется качество по умолчанию для выбранной высоты.
    */
    /// <summary>
    /// Gets the requested quality profile override.
    /// </summary>
    public string? QualityProfile { get; }

    /*
    Это ручное значение CQ.
    Оно влияет не только на качество, но и на автоматический пересчет допустимого диапазона rate-параметров.
    */
    /// <summary>
    /// Gets the explicit CQ override.
    /// </summary>
    public int? Cq { get; }

    /*
    Это ручное значение максимального битрейта.
    Его используют как итоговое значение, если пользователь хочет явно управлять rate-моделью.
    */
    /// <summary>
    /// Gets the explicit maxrate override in Mbps.
    /// </summary>
    public decimal? Maxrate { get; }

    /*
    Это ручное значение буфера кодека.
    Обычно оно вычисляется автоматически, но здесь может быть задано явно.
    */
    /// <summary>
    /// Gets the explicit bufsize override in Mbps.
    /// </summary>
    public decimal? Bufsize { get; }

    /*
    Это признак того, что пользователь вмешался в rate-поля вручную.
    Он нужен, чтобы не применять поверх этого автоматическое ограничение битрейтом источника.
    */
    /// <summary>
    /// Gets a value indicating whether the request contains manual bitrate-related overrides.
    /// </summary>
    internal bool HasManualRateOverrides => Cq.HasValue || Maxrate.HasValue || Bufsize.HasValue;

    /*
    Это удобная фабрика для случаев, когда параметры могут быть пустыми.
    Она возвращает null вместо создания формально пустого объекта.
    */
    /// <summary>
    /// Creates a request only when at least one override is provided; otherwise returns <see langword="null"/>.
    /// </summary>
    public static VideoSettingsRequest? CreateOrNull(
        string? contentProfile = null,
        string? qualityProfile = null,
        int? cq = null,
        decimal? maxrate = null,
        decimal? bufsize = null)
    {
        return HasAnyValue(contentProfile, qualityProfile, cq, maxrate, bufsize)
            ? new VideoSettingsRequest(contentProfile, qualityProfile, cq, maxrate, bufsize)
            : null;
    }

    /*
    Это проверка, можно ли принять строку как профиль контента.
    Полезна, когда объект запроса создавать еще рано или не нужно.
    */
    /// <summary>
    /// Determines whether the specified content profile value is supported.
    /// </summary>
    public static bool IsSupportedContentProfile(string? value)
    {
        return VideoContentProfile.IsSupported(value);
    }

    /*
    Это проверка, можно ли принять строку как профиль качества.
    Нужна для ранней валидации пользовательского ввода.
    */
    /// <summary>
    /// Determines whether the specified quality profile value is supported.
    /// </summary>
    public static bool IsSupportedQualityProfile(string? value)
    {
        return VideoQualityProfile.IsSupported(value);
    }

    /*
    Это внутренняя проверка, что в запросе вообще есть что применять.
    Без нее модель могла бы появиться даже тогда, когда пользователь ничего не переопределил.
    */
    /// <summary>
    /// Determines whether at least one override value is present.
    /// </summary>
    private static bool HasAnyValue(
        string? contentProfile,
        string? qualityProfile,
        int? cq,
        decimal? maxrate,
        decimal? bufsize)
    {
        return !string.IsNullOrWhiteSpace(contentProfile) ||
               !string.IsNullOrWhiteSpace(qualityProfile) ||
               cq.HasValue ||
               maxrate.HasValue ||
               bufsize.HasValue;
    }
}
