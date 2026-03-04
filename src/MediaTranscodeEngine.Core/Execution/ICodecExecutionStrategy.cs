using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

public interface ICodecExecutionStrategy
{
    string Key { get; }

    string Process(TranscodeRequest request, ProbeResult? probeOverride, bool useProbeOverride);
}
