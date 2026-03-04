using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Codecs;

public interface ITranscodeRoute
{
    bool CanHandle(TranscodeRequest request);

    string Process(TranscodeRequest request);

    string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe);

    string ProcessWithProbeJson(TranscodeRequest request, string? probeJson);
}
