using Transcode.Core.VideoSettings;

namespace Transcode.Scenarios.ToH264Rife.Core;

/*
Это компактная модель настроек видео для toh264rife.
Она хранит только те поля, которые реально нужны docker-tool для финального кодирования.
*/
/// <summary>
/// Stores compact resolved video settings required by the <c>toh264rife</c> tool layer.
/// </summary>
sealed record ToH264RifeVideoSettings(
    string ContentProfile,
    string QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize)
{
    /*
    Это итоговый maxrate, переведенный в кбит/с для передачи в docker-команду.
    */
    /// <summary>
    /// Gets the resolved VBV maxrate in kilobits per second.
    /// </summary>
    public int MaxrateKbps => ToKbps(Maxrate);

    /*
    Это итоговый bufsize, переведенный в кбит/с.
    */
    /// <summary>
    /// Gets the resolved VBV bufsize in kilobits per second.
    /// </summary>
    public int BufsizeKbps => ToKbps(Bufsize);

    /*
    Это создание компактной модели из общего результата резолва VideoSettings.
    */
    /// <summary>
    /// Creates a compact settings record from general resolved video settings.
    /// </summary>
    public static ToH264RifeVideoSettings FromResolvedSettings(ResolvedVideoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new ToH264RifeVideoSettings(
            ContentProfile: settings.ContentProfile.Value,
            QualityProfile: settings.QualityProfile.Value,
            Cq: settings.Cq,
            Maxrate: settings.Maxrate,
            Bufsize: settings.Bufsize);
    }

    /*
    Это перевод значения из Мбит/с в кбит/с для CLI/tool аргументов.
    */
    /// <summary>
    /// Converts Mbps to kbps using CLI-friendly rounding.
    /// </summary>
    private static int ToKbps(decimal value)
    {
        return (int)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    }
}
