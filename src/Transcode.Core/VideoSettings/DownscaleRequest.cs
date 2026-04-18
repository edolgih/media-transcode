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
        : this(targetHeight, VideoScaleAlgorithm.ParseOptional(algorithm, nameof(algorithm)))
    {
    }

    /*
    Это внутренний конструктор для сценариев, где алгоритм уже разобран в value type.
    */
    /// <summary>
    /// Initializes a new downscale request from a pre-parsed scaling algorithm.
    /// </summary>
    /// <param name="targetHeight">Requested output height.</param>
    /// <param name="algorithm">Optional pre-parsed scaling algorithm.</param>
    private DownscaleRequest(int targetHeight, VideoScaleAlgorithm? algorithm)
    {
        TargetHeight = VideoDownscaleTargetHeight.Parse(targetHeight, nameof(targetHeight)).Value;
        Algorithm = algorithm;
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
    public VideoScaleAlgorithm? Algorithm { get; }

    /*
    Это способ аккуратно подставить алгоритм по умолчанию.
    Он не затирает уже выбранный алгоритм и возвращает нормализованную копию только когда это действительно нужно.
    */
    /// <summary>
    /// Returns a copy with a default algorithm when the request does not already specify one.
    /// </summary>
    /// <param name="algorithm">Pre-parsed algorithm to apply as default.</param>
    /// <returns>The current instance when an algorithm is already set; otherwise a copy with the provided algorithm.</returns>
    public DownscaleRequest WithDefaultAlgorithm(VideoScaleAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(algorithm);

        return Algorithm is not null
            ? this
            : new DownscaleRequest(TargetHeight, algorithm);
    }
}
