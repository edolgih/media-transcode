using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

public interface ITranscodeExecutionPipeline
{
    string ProcessCopy(TranscodeRequest request);

    string ProcessCopyWithProbeResult(TranscodeRequest request, ProbeResult? probe);

    string ProcessCopyWithProbeJson(TranscodeRequest request, string? probeJson);

    string ProcessH264Gpu(TranscodeRequest request);

    string ProcessH264GpuWithProbeResult(TranscodeRequest request, ProbeResult? probe);

    string ProcessH264GpuWithProbeJson(TranscodeRequest request, string? probeJson);
}
