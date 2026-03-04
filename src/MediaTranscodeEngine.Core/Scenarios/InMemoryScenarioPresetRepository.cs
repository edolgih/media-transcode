namespace MediaTranscodeEngine.Core.Scenarios;

public sealed class InMemoryScenarioPresetRepository : IScenarioPresetRepository
{
    private readonly IReadOnlyDictionary<string, ScenarioPreset> _presets;

    public InMemoryScenarioPresetRepository()
        : this(new[] { ScenarioPreset.CreateToMkvGpu() })
    {
    }

    public InMemoryScenarioPresetRepository(IEnumerable<ScenarioPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);
        _presets = presets.ToDictionary(
            static preset => preset.Name,
            static preset => preset,
            StringComparer.OrdinalIgnoreCase);
    }

    public ScenarioPreset? Get(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _presets.TryGetValue(name.Trim(), out var preset)
            ? preset
            : null;
    }
}
