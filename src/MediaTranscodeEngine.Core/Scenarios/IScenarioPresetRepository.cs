namespace MediaTranscodeEngine.Core.Scenarios;

public interface IScenarioPresetRepository
{
    ScenarioPreset? Get(string? name);
}
