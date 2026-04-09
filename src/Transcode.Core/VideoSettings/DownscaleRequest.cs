namespace Transcode.Core.VideoSettings;

/*
Это явный запрос на уменьшение высоты видео.
Тип отделяет downscale от обычных quality override и при необходимости хранит уже выбранный алгоритм масштабирования.
*/
/// <summary>
/// Represents an explicit request to downscale video and optionally pin the scaling algorithm.
/// </summary>
public sealed class DownscaleRequest
{
    private static readonly int[] SupportedTargetHeightsValues = [.. VideoDownscaleTargetHeight.SupportedValues];

    /*
    Это все целевые высоты, в которые система умеет делать downscale.
    Список нужен для валидации, подсказок и построения UI.
    */
    /// <summary>
    /// Gets the target heights supported for explicit downscale requests.
    /// </summary>
    public static IReadOnlyList<int> SupportedTargetHeights => SupportedTargetHeightsValues;

    /*
    Это список алгоритмов масштабирования, которые можно указать явно.
    Он совпадает с тем набором, который поддерживает слой FFmpeg.
    */
    /// <summary>
    /// Gets the scaling algorithms that can be requested explicitly.
    /// </summary>
    public static IReadOnlyList<string> SupportedAlgorithms => VideoScaleAlgorithm.SupportedValues;

    /*
    Это конструктор запроса на downscale.
    Он сразу нормализует и проверяет высоту и алгоритм, чтобы дальше работать только с корректными значениями.
    */
    /// <summary>
    /// Initializes a new downscale request with a validated target height and optional algorithm.
    /// </summary>
    /// <param name="targetHeight">Requested output height.</param>
    /// <param name="algorithm">Optional scaling algorithm that should be kept with the request.</param>
    public DownscaleRequest(int targetHeight, string? algorithm = null)
    {
        TargetHeight = VideoDownscaleTargetHeight.Parse(targetHeight, nameof(targetHeight)).Value;
        Algorithm = VideoScaleAlgorithm.ParseOptional(algorithm, nameof(algorithm))?.Value;
    }

    /*
    Это итоговая высота, в которую нужно привести видео.
    Значение всегда уже проверено на поддержку системой.
    */
    /// <summary>
    /// Gets the validated target height for the downscaled video.
    /// </summary>
    public int TargetHeight { get; }

    /*
    Это алгоритм масштабирования, который нужно использовать.
    Пустое значение означает, что запрос фиксирует только новую высоту, а алгоритм можно выбрать позже.
    */
    /// <summary>
    /// Gets the requested scaling algorithm, or <see langword="null"/> when only the target height is fixed.
    /// </summary>
    public string? Algorithm { get; }

    /*
    Это способ аккуратно подставить алгоритм по умолчанию.
    Он не затирает уже выбранный алгоритм и возвращает нормализованную копию только когда это действительно нужно.
    */
    /// <summary>
    /// Returns a copy with a default algorithm when the request does not already specify one.
    /// </summary>
    /// <param name="algorithm">Algorithm to apply as the default value.</param>
    /// <returns>The current instance when an algorithm is already set; otherwise a normalized copy.</returns>
    public DownscaleRequest WithDefaultAlgorithm(string algorithm)
    {
        var resolvedAlgorithm = VideoScaleAlgorithm.Parse(algorithm, nameof(algorithm));

        return Algorithm is not null
            ? this
            : new DownscaleRequest(TargetHeight, resolvedAlgorithm.Value);
    }

    /*
    Это быстрая проверка высоты без создания объекта запроса.
    Удобно, когда нужно валидировать входные данные заранее.
    */
    /// <summary>
    /// Determines whether the specified target height is supported for downscale.
    /// </summary>
    public static bool IsSupportedTargetHeight(int targetHeight)
    {
        return VideoDownscaleTargetHeight.IsSupported(targetHeight);
    }

    /*
    Это быстрая проверка алгоритма масштабирования без создания объекта запроса.
    Ее можно использовать на этапе парсинга пользовательских параметров.
    */
    /// <summary>
    /// Determines whether the specified scaling algorithm is supported.
    /// </summary>
    public static bool IsSupportedAlgorithm(string? value)
    {
        return VideoScaleAlgorithm.IsSupported(value);
    }
}
