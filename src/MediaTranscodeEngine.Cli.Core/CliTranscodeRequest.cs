namespace MediaTranscodeEngine.Cli;

/*
Этот объект описывает один входной файл в рамках CLI-запуска:
какой сценарий выбран, включен ли info-режим и какой normalized scenario-specific input уже построен на CLI boundary.
*/
/// <summary>
/// Carries one CLI input together with the selected scenario name and normalized scenario-specific input.
/// </summary>
public sealed class CliTranscodeRequest
{
    /// <summary>
    /// Initializes a per-input CLI transcode request.
    /// </summary>
    /// <param name="inputPath">Input file path.</param>
    /// <param name="scenarioName">Selected scenario name.</param>
    /// <param name="info">Whether info mode is enabled.</param>
    /// <param name="scenarioInput">Normalized scenario-specific input object.</param>
    /// <param name="scenarioArgCount">Count of raw scenario-specific CLI tokens.</param>
    public CliTranscodeRequest(
        string inputPath,
        string scenarioName,
        bool info,
        object scenarioInput,
        int scenarioArgCount)
    {
        InputPath = string.IsNullOrWhiteSpace(inputPath)
            ? throw new ArgumentException("Input path is required.", nameof(inputPath))
            : inputPath;
        ScenarioName = string.IsNullOrWhiteSpace(scenarioName)
            ? throw new ArgumentException("Scenario name is required.", nameof(scenarioName))
            : scenarioName;
        Info = info;
        ScenarioInput = scenarioInput ?? throw new ArgumentNullException(nameof(scenarioInput));
        ScenarioArgCount = scenarioArgCount >= 0
            ? scenarioArgCount
            : throw new ArgumentOutOfRangeException(nameof(scenarioArgCount), scenarioArgCount, "Scenario arg count must be non-negative.");
    }

    /// <summary>
    /// Gets the input file path.
    /// </summary>
    public string InputPath { get; }

    /// <summary>
    /// Gets the selected scenario name.
    /// </summary>
    public string ScenarioName { get; }

    /// <summary>
    /// Gets a value indicating whether info mode is enabled.
    /// </summary>
    public bool Info { get; }

    /// <summary>
    /// Gets the normalized scenario-specific input object.
    /// </summary>
    public object ScenarioInput { get; }

    /// <summary>
    /// Gets the count of raw scenario-specific CLI tokens.
    /// </summary>
    public int ScenarioArgCount { get; }
}
