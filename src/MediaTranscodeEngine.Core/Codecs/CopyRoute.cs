using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class CopyRoute : ITranscodeRoute
{
    private readonly ITranscodeExecutionPipeline _pipeline;

    public CopyRoute(ITranscodeExecutionPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public bool CanHandle(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.ComputeMode.Equals(RequestContracts.General.GpuComputeMode, StringComparison.OrdinalIgnoreCase) &&
               !request.PreferH264 &&
               request.TargetContainer.Equals(RequestContracts.General.MkvContainer, StringComparison.OrdinalIgnoreCase);
    }

    public string Process(TranscodeRequest request)
    {
        return _pipeline.ProcessCopy(request);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        return _pipeline.ProcessCopyWithProbeResult(request, probe);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        return _pipeline.ProcessCopyWithProbeJson(request, probeJson);
    }
}
