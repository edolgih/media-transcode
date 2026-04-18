using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings;

/*
Это одно допустимое значение профиля контента.
Тип нужен, чтобы все части системы работали с одним и тем же нормализованным набором значений.
*/
/// <summary>
/// Represents one supported content profile value.
/// </summary>
public sealed record VideoContentProfile
{
    public static readonly VideoContentProfile Anime = new("anime");
    public static readonly VideoContentProfile Mult = new("mult");
    public static readonly VideoContentProfile Film = new("film");

    private const string SupportedValuesText = "anime, mult, film";
    private static readonly string[] SupportedValuesArray = [Anime.Value, Mult.Value, Film.Value];

    private VideoContentProfile(string value) => Value = value;

    /*
    Это нормализованное строковое значение профиля.
    Его используют в запросах, профилях и итоговых настройках.
    */
    /// <summary>
    /// Gets the normalized content profile value.
    /// </summary>
    public string Value { get; }

    /*
    Это полный список допустимых строковых значений.
    Он нужен для валидации, ошибок и подсказок.
    */
    /// <summary>
    /// Gets all supported content profile values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /*
    Это строгий разбор входной строки в поддерживаемый профиль.
    Метод нормализует значение и выбрасывает исключение, если профиль неизвестен.
    */
    /// <summary>
    /// Parses and normalizes a required content profile value.
    /// </summary>
    public static VideoContentProfile Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "anime" => Anime,
            "mult" => Mult,
            "film" => Film,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /*
    Это мягкий вариант разбора для необязательного значения.
    Только null означает, что override не задан; пустая строка считается ошибкой.
    */
    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static VideoContentProfile? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /*
    Это безопасная проверка строки без исключения.
    Она удобна там, где нужно просто понять, можно ли принять значение.
    */
    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out VideoContentProfile? profile)
    {
        profile = value?.Trim().ToLowerInvariant() switch
        {
            "anime" => Anime,
            "mult" => Mult,
            "film" => Film,
            _ => null
        };

        return profile is not null;
    }

    /*
    Это короткая форма проверки поддерживаемого значения.
    Внутри она использует тот же разбор, что и основная валидация.
    */
    /// <summary>
    /// Determines whether the specified value belongs to the supported set.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    public override string ToString() => Value;
}

/*
Это одно допустимое значение профиля качества.
Через этот тип запросы и профили выбирают согласованный уровень качества.
*/
/// <summary>
/// Represents one supported quality profile value.
/// </summary>
public sealed record VideoQualityProfile
{
    public static readonly VideoQualityProfile High = new("high");
    public static readonly VideoQualityProfile Default = new("default");
    public static readonly VideoQualityProfile Low = new("low");

    private const string SupportedValuesText = "high, default, low";
    private static readonly string[] SupportedValuesArray = [High.Value, Default.Value, Low.Value];

    private VideoQualityProfile(string value) => Value = value;

    /*
    Это нормализованное строковое значение профиля качества.
    Его используют как ключ при выборе defaults и при пользовательских override.
    */
    /// <summary>
    /// Gets the normalized quality profile value.
    /// </summary>
    public string Value { get; }

    /*
    Это все профили качества, которые понимает система.
    Список нужен для ошибок, валидации и отображения допустимых опций.
    */
    /// <summary>
    /// Gets all supported quality profile values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /*
    Это строгий разбор обязательного профиля качества.
    Метод принимает только известные значения и приводит их к единому виду.
    */
    /// <summary>
    /// Parses and normalizes a required quality profile value.
    /// </summary>
    public static VideoQualityProfile Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            "high" => High,
            "default" => Default,
            "low" => Low,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /*
    Это вариант разбора для необязательного значения.
    Только null трактуется как отсутствие override.
    */
    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static VideoQualityProfile? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);

    /*
    Это безопасный разбор профиля качества без исключения.
    Подходит для предварительной проверки ввода.
    */
    /// <summary>
    /// Tries to parse the value without throwing an exception.
    /// </summary>
    public static bool TryParse(string? value, out VideoQualityProfile? profile)
    {
        profile = value?.Trim().ToLowerInvariant() switch
        {
            "high" => High,
            "default" => Default,
            "low" => Low,
            _ => null
        };

        return profile is not null;
    }

    /*
    Это краткая проверка принадлежности значения к поддерживаемому набору.
    */
    /// <summary>
    /// Determines whether the specified value belongs to the supported set.
    /// </summary>
    public static bool IsSupported(string? value) => TryParse(value, out _);

    /*
    Это способ найти соседний профиль качества для пересчета bitrate-шагов.
    Резолвер использует его, когда пользователь меняет CQ и нужно оценить, как должны сдвинуться rate-границы.
    */
    /// <summary>
    /// Tries to resolve the neighboring quality profile used for bitrate recalculation.
    /// </summary>
    public bool TryResolveNeighbor(bool towardsBetterQuality, out VideoQualityProfile neighbor)
    {
        if (this == Default)
        {
            neighbor = towardsBetterQuality ? High : Low;
            return true;
        }

        if (this == High || this == Low)
        {
            neighbor = Default;
            return true;
        }

        neighbor = Default;
        return false;
    }

    public override string ToString() => Value;
}

/*
Это одно допустимое значение алгоритма масштабирования.
Тип связывает пользовательские строки с теми именами, которые понимает FFmpeg.
*/
/// <summary>
/// Represents one supported scaling algorithm value.
/// </summary>
public sealed record VideoScaleAlgorithm
{
    public static readonly VideoScaleAlgorithm Bilinear = new(FfmpegScaleAlgorithms.Bilinear);
    public static readonly VideoScaleAlgorithm Bicubic = new(FfmpegScaleAlgorithms.Bicubic);
    public static readonly VideoScaleAlgorithm Lanczos = new(FfmpegScaleAlgorithms.Lanczos);

    private const string SupportedValuesText = "bilinear, bicubic, lanczos";
    private static readonly string[] SupportedValuesArray =
        [FfmpegScaleAlgorithms.Bilinear, FfmpegScaleAlgorithms.Bicubic, FfmpegScaleAlgorithms.Lanczos];

    private VideoScaleAlgorithm(string value) => Value = value;

    /*
    Это нормализованное имя алгоритма масштабирования.
    Оно передается дальше в инструменты сборки ffmpeg-команд.
    */
    /// <summary>
    /// Gets the normalized scaling algorithm value.
    /// </summary>
    public string Value { get; }

    /*
    Это все алгоритмы масштабирования, которые разрешены системой.
    */
    /// <summary>
    /// Gets all supported scaling algorithm values.
    /// </summary>
    public static IReadOnlyList<string> SupportedValues => SupportedValuesArray;

    /*
    Это строгий разбор обязательного алгоритма.
    Он нужен, когда значение уже должно быть валидным и пригодным для запуска.
    */
    /// <summary>
    /// Parses and normalizes a required scaling algorithm value.
    /// </summary>
    public static VideoScaleAlgorithm Parse(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value.Trim().ToLowerInvariant() switch
        {
            FfmpegScaleAlgorithms.Bilinear => Bilinear,
            FfmpegScaleAlgorithms.Bicubic => Bicubic,
            FfmpegScaleAlgorithms.Lanczos => Lanczos,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };
    }

    /*
    Это вариант разбора для необязательного алгоритма.
    Если алгоритм равен null, решение о нем можно отложить на следующий этап.
    */
    /// <summary>
    /// Returns <see langword="null"/> when the value is <see langword="null"/>; otherwise parses it.
    /// </summary>
    public static VideoScaleAlgorithm? ParseOptional(string? value, string paramName) =>
        value is null ? null : Parse(value, paramName);



    public override string ToString() => Value;
}

/*
Это одна допустимая целевая высота для downscale.
Тип задает закрытый список высот, с которыми умеют работать сценарии и профильные таблицы.
*/
/// <summary>
/// Represents one supported target height for explicit downscale.
/// </summary>
sealed record VideoDownscaleTargetHeight
{
    public static readonly VideoDownscaleTargetHeight H720 = new(720);
    public static readonly VideoDownscaleTargetHeight H576 = new(576);
    public static readonly VideoDownscaleTargetHeight H480 = new(480);
    public static readonly VideoDownscaleTargetHeight H424 = new(424);

    private const string SupportedValuesText = "720, 576, 480, 424";
    private static readonly int[] SupportedValuesArray = [H720.Value, H576.Value, H480.Value, H424.Value];

    private VideoDownscaleTargetHeight(int value) => Value = value;

    /*
    Это числовое значение целевой высоты.
    Оно уже проверено и всегда входит в список поддерживаемых высот.
    */
    /// <summary>
    /// Gets the validated target height value.
    /// </summary>
    public int Value { get; }

    /*
    Это полный набор допустимых целевых высот.
    Его используют и для валидации, и для отображения поддерживаемых опций.
    */
    /// <summary>
    /// Gets all supported downscale target heights.
    /// </summary>
    public static IReadOnlyList<int> SupportedValues => SupportedValuesArray;

    /*
    Это строгий разбор высоты downscale.
    Неподдерживаемые значения сразу отсекаются как ошибка входных данных.
    */
    /// <summary>
    /// Parses a required downscale target height and validates that it is supported.
    /// </summary>
    public static VideoDownscaleTargetHeight Parse(int value, string paramName) =>
        value switch
        {
            720 => H720,
            576 => H576,
            480 => H480,
            424 => H424,
            _ => throw new ArgumentOutOfRangeException(paramName, value, $"Supported values: {SupportedValuesText}.")
        };

    /*
    Это безопасная проверка высоты без исключения.
    */
    /// <summary>
    /// Tries to parse the target height without throwing an exception.
    /// </summary>
    public static bool TryParse(int value, out VideoDownscaleTargetHeight? targetHeight)
    {
        targetHeight = value switch
        {
            720 => H720,
            576 => H576,
            480 => H480,
            424 => H424,
            _ => null
        };

        return targetHeight is not null;
    }

    /*
    Это краткая проверка, входит ли высота в поддерживаемый набор.
    */
    /// <summary>
    /// Determines whether the specified height belongs to the supported set.
    /// </summary>
    public static bool IsSupported(int value) => TryParse(value, out _);

    public override string ToString() => Value.ToString();
}
