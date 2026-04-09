namespace Transcode.Scenarios.ToH264Rife.Cli;

/*
Это ключи конфигурации CLI для сценария toh264rife.
Они задают имена секций, которые читает service registration и help.
*/
/// <summary>
/// Defines CLI configuration keys used by the <c>toh264rife</c> scenario.
/// </summary>
public static class ToH264RifeCliConfigurationKeys
{
    /*
    Это ключ имени docker-образа для запуска интерполяции.
    */
    /// <summary>
    /// Gets the configuration key that stores the docker image name.
    /// </summary>
    public const string DockerImage = "Scenarios:ToH264Rife:DockerImage";
}
