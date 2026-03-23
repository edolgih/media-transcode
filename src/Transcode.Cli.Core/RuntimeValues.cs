namespace Transcode.Cli.Core;

/*
Этот тип хранит пути к внешним утилитам, которые нужны CLI для работы.
*/
/// <summary>
/// Holds configured external tool executable paths used by the CLI host.
/// </summary>
public sealed class RuntimeValues
{
    /// <summary>
    /// Gets the configured path to <c>ffprobe</c>.
    /// </summary>
    public string? FfprobePath { get; init; }

    /// <summary>
    /// Gets the configured path to <c>ffmpeg</c>.
    /// </summary>
    public string? FfmpegPath { get; init; }

    /// <summary>
    /// Gets the configured path to <c>rife-ncnn-vulkan</c>.
    /// </summary>
    public string? RifeNcnnPath { get; init; }
}
