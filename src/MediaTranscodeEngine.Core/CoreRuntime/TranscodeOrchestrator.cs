using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Codecs;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core;

public sealed class TranscodeOrchestrator
{
    private readonly TranscodeRouteSelector _routeSelector;
    private readonly ITranscodeExecutionPipeline _pipeline;

    public TranscodeOrchestrator(
        TranscodeRouteSelector routeSelector,
        ITranscodeExecutionPipeline pipeline)
    {
        _routeSelector = routeSelector;
        _pipeline = pipeline;
    }

    public string Process(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var strategyKey = _routeSelector.SelectStrategyKey(request);
        return _pipeline.ProcessByKey(strategyKey, request);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        ArgumentNullException.ThrowIfNull(request);
        var strategyKey = _routeSelector.SelectStrategyKey(request);
        return _pipeline.ProcessByKeyWithProbeResult(strategyKey, request, probe);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        ArgumentNullException.ThrowIfNull(request);
        var strategyKey = _routeSelector.SelectStrategyKey(request);
        return _pipeline.ProcessByKeyWithProbeJson(strategyKey, request, probeJson);
    }
}
