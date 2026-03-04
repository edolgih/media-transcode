using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Cli.Processing;

internal interface ITranscodeProcessor
{
    string Process(TranscodeRequest request);
}
