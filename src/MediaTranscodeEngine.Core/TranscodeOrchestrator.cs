using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Codecs;

namespace MediaTranscodeEngine.Core;

public sealed class TranscodeOrchestrator
{
    private readonly TranscodeRouteSelector _routeSelector;

    public TranscodeOrchestrator(TranscodeRouteSelector routeSelector)
    {
        _routeSelector = routeSelector;
    }

    public string Process(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = _routeSelector.Select(request);
        return route.Process(request);
    }

    public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = _routeSelector.Select(request);
        return route.ProcessWithProbeResult(request, probe);
    }

    public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = _routeSelector.Select(request);
        return route.ProcessWithProbeJson(request, probeJson);
    }
}
