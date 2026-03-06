using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Execution;

public sealed class TranscodeExecutionPipeline : ITranscodeExecutionPipeline
{
    private readonly IReadOnlyDictionary<string, ICodecExecutionStrategy> _strategies;

    public TranscodeExecutionPipeline(
        IEnumerable<ICodecExecutionStrategy> codecExecutionStrategies)
    {
        ArgumentNullException.ThrowIfNull(codecExecutionStrategies);

        var strategies = codecExecutionStrategies.ToArray();
        if (strategies.Length == 0)
        {
            throw new ArgumentException("At least one codec execution strategy must be registered.", nameof(codecExecutionStrategies));
        }

        _strategies = strategies.ToDictionary(
            static strategy => strategy.Key,
            static strategy => strategy,
            StringComparer.OrdinalIgnoreCase);
    }

    public string ProcessByKey(string strategyKey, TranscodeRequest request)
    {
        return Process(strategyKey, request, probeOverride: null, useProbeOverride: false);
    }

    public string ProcessByKeyWithProbeResult(string strategyKey, TranscodeRequest request, ProbeResult? probe)
    {
        return Process(strategyKey, request, probe, useProbeOverride: true);
    }

    public string ProcessByKeyWithProbeJson(string strategyKey, TranscodeRequest request, string? probeJson)
    {
        return Process(strategyKey, request, ProbeJsonParser.Parse(probeJson), useProbeOverride: true);
    }

    private string Process(
        string strategyKey,
        TranscodeRequest request,
        ProbeResult? probeOverride,
        bool useProbeOverride)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_strategies.TryGetValue(strategyKey, out var strategy))
        {
            throw new InvalidOperationException($"Execution strategy '{strategyKey}' is not registered.");
        }

        return strategy.Process(request, probeOverride, useProbeOverride);
    }
}
