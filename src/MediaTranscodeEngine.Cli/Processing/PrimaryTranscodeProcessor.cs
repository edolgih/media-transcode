using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core;

namespace MediaTranscodeEngine.Cli.Processing;

internal sealed class PrimaryTranscodeProcessor : ITranscodeProcessor
{
    private readonly TranscodeOrchestrator _engine;

    public PrimaryTranscodeProcessor(TranscodeOrchestrator engine)
    {
        _engine = engine;
    }

    public string Process(TranscodeRequest request)
    {
        return _engine.Process(request);
    }
}
