using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Scenarios;

public sealed class TranscodeScenarioCatalog
{
    private readonly IReadOnlyDictionary<string, TranscodeScenario> _scenarios;

    public TranscodeScenarioCatalog(IEnumerable<TranscodeScenario> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        _scenarios = scenarios.ToDictionary(
            static scenario => scenario.Name,
            static scenario => scenario,
            StringComparer.OrdinalIgnoreCase);
    }

    public TranscodeScenario? Get(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _scenarios.TryGetValue(name.Trim(), out var scenario)
            ? scenario
            : null;
    }

    public RawTranscodeRequest Apply(
        RawTranscodeRequest request,
        IReadOnlySet<string>? explicitTemplateFields = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Scenario))
        {
            return request;
        }

        var scenario = Get(request.Scenario);
        if (scenario is null)
        {
            throw new ArgumentException($"Unknown scenario: {request.Scenario}", nameof(request));
        }

        return scenario.Apply(request, explicitTemplateFields);
    }
}
