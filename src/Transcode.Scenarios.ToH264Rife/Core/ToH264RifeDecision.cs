using Transcode.Core.MediaIntent;
namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это итоговое решение сценария toh264rife.
Оно хранит выбранные намерения по видео и аудио, целевой контейнер и параметры интерполяции, которые пойдут в tool-слой.
*/
/// <summary>
/// Represents the final resolved decision for the <c>toh264rife</c> scenario.
/// </summary>
internal sealed class ToH264RifeDecision
{
    /*
    Это конструктор полностью разрешенного решения.
    В него попадают уже нормализованные и проверенные значения, которые нужны для построения команд.
    */
    /// <summary>
    /// Initializes a fully resolved <c>toh264rife</c> decision.
    /// </summary>
    public ToH264RifeDecision(
        string targetContainer,
        VideoIntent video,
        AudioIntent audio,
        bool keepSource,
        string outputPath,
        string interpolationQualityProfile,
        string interpolationModelName,
        ToH264RifeVideoSettings resolvedVideoSettings,
        double resolvedTargetFramesPerSecond,
        int userFacingTargetFramesPerSecond,
        int framesPerSecondMultiplier)
    {
        TargetContainer = targetContainer;
        Video = video;
        Audio = audio;
        KeepSource = keepSource;
        OutputPath = outputPath;
        InterpolationQualityProfile = interpolationQualityProfile ?? throw new ArgumentNullException(nameof(interpolationQualityProfile));
        InterpolationModelName = interpolationModelName ?? throw new ArgumentNullException(nameof(interpolationModelName));
        ResolvedVideoSettings = resolvedVideoSettings ?? throw new ArgumentNullException(nameof(resolvedVideoSettings));
        ResolvedTargetFramesPerSecond = resolvedTargetFramesPerSecond;
        UserFacingTargetFramesPerSecond = userFacingTargetFramesPerSecond;
        FramesPerSecondMultiplier = framesPerSecondMultiplier;
    }

    /*
    Это целевой контейнер результата.
    */
    /// <summary>
    /// Gets the target output container.
    /// </summary>
    public string TargetContainer { get; }

    /*
    Это выбранный путь обработки видеопотока.
    */
    /// <summary>
    /// Gets the resolved video intent.
    /// </summary>
    public VideoIntent Video { get; }

    /*
    Это выбранный путь обработки аудиопотока.
    */
    /// <summary>
    /// Gets the resolved audio intent.
    /// </summary>
    public AudioIntent Audio { get; }

    /*
    Это флаг, нужно ли сохранять исходный файл после обработки.
    */
    /// <summary>
    /// Gets a value indicating whether the source file should be preserved.
    /// </summary>
    public bool KeepSource { get; }

    /*
    Это итоговый путь выходного файла.
    */
    /// <summary>
    /// Gets the resolved output path.
    /// </summary>
    public string OutputPath { get; }

    /*
    Это профиль качества интерполяции, выбранный пользователем или по умолчанию.
    */
    /// <summary>
    /// Gets the interpolation quality profile used by the scenario.
    /// </summary>
    public string InterpolationQualityProfile { get; }

    /*
    Это конкретная модель интерполяции, которая будет запущена в tool-слое.
    */
    /// <summary>
    /// Gets the interpolation model name resolved from the quality profile.
    /// </summary>
    public string InterpolationModelName { get; }

    /*
    Это итоговые настройки кодирования после выбора профилей и override.
    */
    /// <summary>
    /// Gets the resolved video encoding settings.
    /// </summary>
    public ToH264RifeVideoSettings ResolvedVideoSettings { get; }

    /*
    Это точное целевое FPS после умножения исходного значения.
    */
    /// <summary>
    /// Gets the precise target frames per second used for execution.
    /// </summary>
    public double ResolvedTargetFramesPerSecond { get; }

    /*
    Это округленное значение целевого FPS для имени выходного файла и пользовательского отображения.
    */
    /// <summary>
    /// Gets the user-facing rounded target frames per second.
    /// </summary>
    public int UserFacingTargetFramesPerSecond { get; }

    /*
    Это множитель FPS, с которым запрошена интерполяция.
    */
    /// <summary>
    /// Gets the configured frame-rate multiplier.
    /// </summary>
    public int FramesPerSecondMultiplier { get; }

    /*
    Это признак, что видеопоток будет скопирован без перекодирования.
    */
    /// <summary>
    /// Gets a value indicating whether the video stream will be copied without re-encoding.
    /// </summary>
    public bool CopyVideo => Video is CopyVideoIntent;

    /*
    Это признак, что аудиопоток будет скопирован без перекодирования.
    */
    /// <summary>
    /// Gets a value indicating whether the audio stream will be copied without re-encoding.
    /// </summary>
    public bool CopyAudio => Audio is CopyAudioIntent;

    /*
    Это признак, что решение включает интерполяцию кадров.
    */
    /// <summary>
    /// Gets a value indicating whether frame interpolation is required.
    /// </summary>
    public bool RequiresInterpolation => Video is EncodeVideoIntent { UseFrameInterpolation: true };
}
