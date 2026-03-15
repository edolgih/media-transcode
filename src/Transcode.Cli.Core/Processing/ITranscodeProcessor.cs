namespace Transcode.Cli.Core.Processing;

/*
Это минимальный контракт обработки одного входного файла в CLI.
На выходе возвращается строка, которую нужно напечатать пользователю.
*/
/// <summary>
/// Processes one CLI input and returns the line that should be printed for it.
/// </summary>
internal interface ITranscodeProcessor
{
    /// <summary>
    /// Processes the supplied CLI request.
    /// </summary>
    /// <param name="request">Per-input CLI request.</param>
    /// <returns>Output line for the input, or an empty string when nothing should be printed.</returns>
    string Process(CliTranscodeRequest request);
}
