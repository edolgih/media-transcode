using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это полный набор профильных настроек видео.
Класс хранит профили по целевой высоте и дает единый источник defaults для обычного encode и для downscale.
*/
/// <summary>
/// Stores the complete set of configured video settings profiles.
/// </summary>
sealed class VideoSettingsProfiles
{
    private readonly IReadOnlyDictionary<int, VideoSettingsProfile> _profilesByTargetHeight;
    private readonly int[] _supportedDownscaleTargetHeights;
    private readonly string[] _supportedContentProfiles;
    private readonly string[] _supportedQualityProfiles;

    VideoSettingsProfiles(IReadOnlyDictionary<int, VideoSettingsProfile> profilesByTargetHeight)
    {
        _profilesByTargetHeight = profilesByTargetHeight;
        _supportedDownscaleTargetHeights = profilesByTargetHeight.Values
            .Where(static profile => profile.SupportsDownscale)
            .OrderByDescending(static profile => profile.TargetHeight)
            .Select(static profile => profile.TargetHeight)
            .ToArray();
        _supportedContentProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.ContentProfile.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _supportedQualityProfiles = profilesByTargetHeight.Values
            .SelectMany(static profile => profile.Defaults)
            .Select(static defaults => defaults.QualityProfile.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /*
    Это стандартный набор профилей, который использует приложение.
    Через него сценарии получают готовую конфигурацию без ручной сборки таблиц.
    */
    /// <summary>
    /// Gets the default profile set used by the application.
    /// </summary>
    public static VideoSettingsProfiles Default { get; } = CreateDefault();

    /*
    Это строгий доступ к профилю по целевой высоте.
    Если профиль не настроен, ошибка возникает сразу, а не позже в процессе резолва.
    */
    /// <summary>
    /// Returns the profile for the specified target height or throws if it is not configured.
    /// </summary>
    public VideoSettingsProfile GetRequiredProfile(int targetHeight)
    {
        if (_profilesByTargetHeight.TryGetValue(targetHeight, out var profile))
        {
            return profile;
        }

        throw new InvalidOperationException($"Video settings profile '{targetHeight}' is not configured.");
    }

    /*
    Это безопасный вариант поиска профиля по высоте.
    Подходит, когда отсутствие профиля считается штатной ситуацией.
    */
    /// <summary>
    /// Tries to get the profile for the specified target height.
    /// </summary>
    public bool TryGetProfile(int targetHeight, out VideoSettingsProfile profile)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out profile!);
    }

    /*
    Это выбор ближайшего профиля по фактической высоте результата.
    Он нужен для обычного encode, когда задана итоговая высота, но нет явного downscale target.
    */
    /// <summary>
    /// Resolves the closest profile for the requested output height.
    /// </summary>
    public VideoSettingsProfile ResolveOutputProfile(int outputHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        return _profilesByTargetHeight.Values
            .OrderBy(profile => Math.Abs(profile.TargetHeight - outputHeight))
            .ThenByDescending(profile => profile.TargetHeight)
            .First();
    }

    /*
    Это все высоты, для которых разрешен явный downscale.
    Список уже отфильтрован только до тех профилей, где downscale действительно поддерживается.
    */
    /// <summary>
    /// Returns the target heights that support explicit downscale.
    /// </summary>
    public IReadOnlyList<int> GetSupportedDownscaleTargetHeights()
    {
        return _supportedDownscaleTargetHeights;
    }

    /*
    Это объединенный список профилей контента из всех настроенных профилей.
    Его можно показывать пользователю как общий поддерживаемый набор.
    */
    /// <summary>
    /// Returns every supported content profile across all configured target heights.
    /// </summary>
    public IReadOnlyList<string> GetSupportedContentProfiles()
    {
        return _supportedContentProfiles;
    }

    /*
    Это объединенный список профилей качества из всех таблиц defaults.
    */
    /// <summary>
    /// Returns every supported quality profile across all configured target heights.
    /// </summary>
    public IReadOnlyList<string> GetSupportedQualityProfiles()
    {
        return _supportedQualityProfiles;
    }

    /*
    Это проверка, можно ли делать downscale именно в эту высоту.
    Она учитывает не только наличие профиля, но и флаг разрешения downscale.
    */
    /// <summary>
    /// Determines whether explicit downscale is allowed for the specified target height.
    /// </summary>
    public bool SupportsDownscaleTargetHeight(int targetHeight)
    {
        return _profilesByTargetHeight.TryGetValue(targetHeight, out var profile) &&
               profile.SupportsDownscale;
    }

    /*
    Это фабрика набора профилей из конкретных объектов профиля.
    Она удобна для тестов и для сборки стандартной конфигурации.
    */
    /// <summary>
    /// Creates a profile set from the supplied profile definitions.
    /// </summary>
    internal static VideoSettingsProfiles Create(params VideoSettingsProfile[] profiles)
    {
        return new VideoSettingsProfiles(profiles.ToDictionary(static profile => profile.TargetHeight));
    }

    /*
    Это сборка стандартного набора профилей приложения.
    */
    /// <summary>
    /// Creates the default application profile set.
    /// </summary>
    private static VideoSettingsProfiles CreateDefault()
    {
        return Create(
            VideoSettings1080Profile.Create(),
            VideoSettings720Profile.Create(),
            VideoSettings424Profile.Create(),
            VideoSettings480Profile.Create(),
            VideoSettings576Profile.Create());
    }
}

/*
Это одна запись значений по умолчанию внутри профильной таблицы.
Она описывает базовые настройки для конкретной пары content profile и quality profile.
*/
/// <summary>
/// Represents one default settings row inside a target-height profile.
/// </summary>
sealed record VideoSettingsDefaults(
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
    /*
    Это конструктор, который позволяет описывать таблицы defaults обычными строками.
    Он сразу переводит их в типизированные значения профилей и алгоритма.
    */
    /// <summary>
    /// Initializes a default settings row from string identifiers.
    /// </summary>
    public VideoSettingsDefaults(
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
    Это профиль контента, для которого действует эта строка defaults.
    */
    /// <summary>
    /// Gets the content profile this defaults row applies to.
    /// </summary>
    public VideoContentProfile ContentProfile { get; init; } = ContentProfile ?? throw new ArgumentNullException(nameof(ContentProfile));

    /*
    Это профиль качества, для которого действует эта строка defaults.
    */
    /// <summary>
    /// Gets the quality profile this defaults row applies to.
    /// </summary>
    public VideoQualityProfile QualityProfile { get; init; } = QualityProfile ?? throw new ArgumentNullException(nameof(QualityProfile));

    /*
    Это базовое значение CQ для выбранной пары профилей.
    */
    /// <summary>
    /// Gets the default CQ value.
    /// </summary>
    public int Cq { get; init; } = Cq > 0
        ? Cq
        : throw new ArgumentOutOfRangeException(nameof(Cq), Cq, "CQ must be greater than zero.");

    /*
    Это базовый maxrate в мегабитах в секунду.
    */
    /// <summary>
    /// Gets the default maxrate in Mbps.
    /// </summary>
    public decimal Maxrate { get; init; } = Maxrate > 0m
        ? Maxrate
        : throw new ArgumentOutOfRangeException(nameof(Maxrate), Maxrate, "Maxrate must be greater than zero.");

    /*
    Это базовый bufsize, который обычно рассчитывается как производное от rate-модели.
    */
    /// <summary>
    /// Gets the default bufsize in Mbps.
    /// </summary>
    public decimal Bufsize { get; init; } = Bufsize > 0m
        ? Bufsize
        : throw new ArgumentOutOfRangeException(nameof(Bufsize), Bufsize, "Bufsize must be greater than zero.");

    /*
    Это алгоритм масштабирования по умолчанию для этого сочетания профилей.
    */
    /// <summary>
    /// Gets the default scaling algorithm.
    /// </summary>
    public VideoScaleAlgorithm Algorithm { get; init; } = Algorithm ?? throw new ArgumentNullException(nameof(Algorithm));

    /*
    Это нижняя граница CQ, ниже которой опускаться нельзя.
    */
    /// <summary>
    /// Gets the minimum allowed CQ value.
    /// </summary>
    public int CqMin { get; init; } = CqMin > 0
        ? CqMin
        : throw new ArgumentOutOfRangeException(nameof(CqMin), CqMin, "CQ minimum must be greater than zero.");

    /*
    Это верхняя граница CQ для этой строки defaults.
    */
    /// <summary>
    /// Gets the maximum allowed CQ value.
    /// </summary>
    public int CqMax { get; init; } = CqMax >= CqMin
        ? CqMax
        : throw new ArgumentOutOfRangeException(nameof(CqMax), CqMax, "CQ maximum must be greater than or equal to minimum.");

    /*
    Это минимальный допустимый maxrate после всех пересчетов и override.
    */
    /// <summary>
    /// Gets the minimum allowed maxrate in Mbps.
    /// </summary>
    public decimal MaxrateMin { get; init; } = MaxrateMin > 0m
        ? MaxrateMin
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMin), MaxrateMin, "Maxrate minimum must be greater than zero.");

    /*
    Это верхняя граница maxrate для этой комбинации профилей.
    */
    /// <summary>
    /// Gets the maximum allowed maxrate in Mbps.
    /// </summary>
    public decimal MaxrateMax { get; init; } = MaxrateMax >= MaxrateMin
        ? MaxrateMax
        : throw new ArgumentOutOfRangeException(nameof(MaxrateMax), MaxrateMax, "Maxrate maximum must be greater than or equal to minimum.");
}

/*
Это модель пересчета rate-параметров.
Она задает, как двигать maxrate и bufsize, когда меняется CQ.
*/
/// <summary>
/// Stores coefficients used to recalculate maxrate and bufsize.
/// </summary>
sealed record VideoSettingsRateModel(decimal CqStepToMaxrateStep, decimal BufsizeMultiplier)
{
    /*
    Это шаг изменения maxrate на один шаг CQ.
    Он используется как базовый коэффициент пересчета.
    */
    /// <summary>
    /// Gets the maxrate delta applied for one CQ step.
    /// </summary>
    public decimal CqStepToMaxrateStep { get; init; } = CqStepToMaxrateStep > 0m
        ? CqStepToMaxrateStep
        : throw new ArgumentOutOfRangeException(nameof(CqStepToMaxrateStep), CqStepToMaxrateStep, "CQ step must be greater than zero.");

    /*
    Это множитель, по которому рассчитывается bufsize от maxrate.
    */
    /// <summary>
    /// Gets the multiplier used to derive bufsize from maxrate.
    /// </summary>
    public decimal BufsizeMultiplier { get; init; } = BufsizeMultiplier > 0m
        ? BufsizeMultiplier
        : throw new ArgumentOutOfRangeException(nameof(BufsizeMultiplier), BufsizeMultiplier, "Bufsize multiplier must be greater than zero.");
}

/*
Это локальное переопределение границ для одной пары профилей.
Оно применяется внутри конкретного диапазона высоты источника и меняет только нужные пределы.
*/
/// <summary>
/// Represents a partial bounds override for one content and quality profile pair.
/// </summary>
sealed record VideoSettingsBoundsOverride(
    VideoContentProfile ContentProfile,
    VideoQualityProfile QualityProfile,
    int? CqMin = null,
    int? CqMax = null,
    decimal? MaxrateMin = null,
    decimal? MaxrateMax = null)
{
    /*
    Это конструктор, который позволяет задавать override строковыми идентификаторами профилей.
    */
    /// <summary>
    /// Initializes a bounds override from string identifiers.
    /// </summary>
    public VideoSettingsBoundsOverride(
        string ContentProfile,
        string QualityProfile,
        int? CqMin = null,
        int? CqMax = null,
        decimal? MaxrateMin = null,
        decimal? MaxrateMax = null)
        : this(
            VideoContentProfile.Parse(ContentProfile, nameof(ContentProfile)),
            VideoQualityProfile.Parse(QualityProfile, nameof(QualityProfile)),
            CqMin,
            CqMax,
            MaxrateMin,
            MaxrateMax)
    {
    }

    /*
    Это профиль контента, к которому относится override.
    */
    /// <summary>
    /// Gets the content profile this override targets.
    /// </summary>
    public VideoContentProfile ContentProfile { get; init; } = ContentProfile ?? throw new ArgumentNullException(nameof(ContentProfile));

    /*
    Это профиль качества, к которому относится override.
    */
    /// <summary>
    /// Gets the quality profile this override targets.
    /// </summary>
    public VideoQualityProfile QualityProfile { get; init; } = QualityProfile ?? throw new ArgumentNullException(nameof(QualityProfile));

    /*
    Это локальная нижняя граница CQ, если ее нужно изменить.
    */
    /// <summary>
    /// Gets the optional CQ minimum override.
    /// </summary>
    public int? CqMin { get; init; } = NormalizeOptionalPositiveInt(CqMin, nameof(CqMin));

    /*
    Это локальная верхняя граница CQ, если профилю нужно послабление или ужесточение.
    */
    /// <summary>
    /// Gets the optional CQ maximum override.
    /// </summary>
    public int? CqMax { get; init; } = NormalizeOptionalPositiveInt(CqMax, nameof(CqMax));

    /*
    Это локальная нижняя граница maxrate.
    */
    /// <summary>
    /// Gets the optional maxrate minimum override in Mbps.
    /// </summary>
    public decimal? MaxrateMin { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMin, nameof(MaxrateMin));

    /*
    Это локальная верхняя граница maxrate.
    */
    /// <summary>
    /// Gets the optional maxrate maximum override in Mbps.
    /// </summary>
    public decimal? MaxrateMax { get; init; } = NormalizeOptionalPositiveDecimal(MaxrateMax, nameof(MaxrateMax));

    /*
    Это проверка, что override относится именно к запрошенной паре профилей.
    */
    /// <summary>
    /// Determines whether this override applies to the specified profile pair.
    /// </summary>
    public bool Matches(VideoContentProfile contentProfile, VideoQualityProfile qualityProfile)
    {
        return ContentProfile == contentProfile && QualityProfile == qualityProfile;
    }

    /*
    Это нормализация необязательной целочисленной границы.
    Пустое значение сохраняется, а неположительное считается ошибкой конфигурации.
    */
    /// <summary>
    /// Normalizes an optional positive integer value.
    /// </summary>
    private static int? NormalizeOptionalPositiveInt(int? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

    /*
    Это нормализация необязательной десятичной границы.
    */
    /// <summary>
    /// Normalizes an optional positive decimal value.
    /// </summary>
    private static decimal? NormalizeOptionalPositiveDecimal(decimal? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0m
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }
}

/*
Это диапазон высоты источника внутри профиля.
Через него профиль понимает, какие локальные ограничения применять для конкретного размера исходника.
*/
/// <summary>
/// Represents a source-height bucket together with its local bounds overrides.
/// </summary>
sealed record SourceHeightBucket(
    string Name,
    int MinHeight,
    int MaxHeight,
    IReadOnlyList<VideoSettingsBoundsOverride>? BoundsOverrides = null,
    bool IsDefault = false)
{
    /*
    Это имя bucket'а для логов, диагностики и тестов.
    */
    /// <summary>
    /// Gets the normalized bucket name.
    /// </summary>
    public string Name { get; init; } = NormalizeRequiredToken(Name, nameof(Name));

    /*
    Это минимальная высота источника, попадающая в bucket.
    */
    /// <summary>
    /// Gets the inclusive minimum source height for the bucket.
    /// </summary>
    public int MinHeight { get; init; } = MinHeight > 0
        ? MinHeight
        : throw new ArgumentOutOfRangeException(nameof(MinHeight), MinHeight, "Minimum height must be greater than zero.");

    /*
    Это максимальная высота источника, попадающая в bucket.
    */
    /// <summary>
    /// Gets the inclusive maximum source height for the bucket.
    /// </summary>
    public int MaxHeight { get; init; } = MaxHeight >= MinHeight
        ? MaxHeight
        : throw new ArgumentOutOfRangeException(nameof(MaxHeight), MaxHeight, "Maximum height must be greater than or equal to minimum height.");

    /*
    Это список локальных override для пар профилей внутри bucket'а.
    Если список пуст, используются только базовые ограничения defaults.
    */
    /// <summary>
    /// Gets the local bounds overrides available inside this bucket.
    /// </summary>
    public IReadOnlyList<VideoSettingsBoundsOverride> BoundsOverrides { get; init; } = BoundsOverrides ?? Array.Empty<VideoSettingsBoundsOverride>();

    /*
    Это признак bucket'а по умолчанию.
    Его используют как запасной вариант, когда точную высоту источника определить нельзя.
    */
    /// <summary>
    /// Gets a value indicating whether this bucket is the fallback bucket.
    /// </summary>
    public bool IsDefault { get; init; } = IsDefault;

    /*
    Это проверка, входит ли высота источника в диапазон bucket'а.
    */
    /// <summary>
    /// Determines whether the specified height falls into this bucket.
    /// </summary>
    public bool Matches(int height)
    {
        return height >= MinHeight && height <= MaxHeight;
    }

    /*
    Это поиск локального override по типизированной паре профилей.
    */
    /// <summary>
    /// Resolves the local bounds override for the specified profile pair.
    /// </summary>
    public VideoSettingsBoundsOverride? ResolveBoundsOverride(VideoContentProfile contentProfile, VideoQualityProfile qualityProfile)
    {
        return BoundsOverrides.FirstOrDefault(overrideEntry => overrideEntry.Matches(contentProfile, qualityProfile));
    }

    /*
    Это удобный перегруженный вариант поиска override по строковым именам профилей.
    */
    /// <summary>
    /// Resolves the local bounds override for the specified string profile pair.
    /// </summary>
    public VideoSettingsBoundsOverride? ResolveBoundsOverride(string contentProfile, string qualityProfile)
    {
        return ResolveBoundsOverride(
            VideoContentProfile.Parse(contentProfile, nameof(contentProfile)),
            VideoQualityProfile.Parse(qualityProfile, nameof(qualityProfile)));
    }

    /*
    Это нормализация обязательного текстового идентификатора bucket'а.
    */
    /// <summary>
    /// Normalizes a required token value.
    /// </summary>
    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
