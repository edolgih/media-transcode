namespace MediaTranscodeEngine.Cli;

/*
Этот объект описывает один входной файл в рамках CLI-запуска:
какой сценарий выбран, включен ли info-режим и какие scenario-specific аргументы переданы дальше.
*/
/// <summary>
/// Carries one CLI input together with the selected scenario name and raw scenario-specific arguments.
/// </summary>
public sealed class CliTranscodeRequest
{
    /// <summary>
    /// Initializes a per-input CLI transcode request.
    /// </summary>
    /// <param name="inputPath">Input file path.</param>
    /// <param name="scenarioName">Selected scenario name.</param>
    /// <param name="info">Whether info mode is enabled.</param>
    /// <param name="scenarioArgs">Raw scenario-specific arguments.</param>
    public CliTranscodeRequest(
        string inputPath,
        string scenarioName,
        bool info,
        IReadOnlyList<string> scenarioArgs)
    {
        InputPath = string.IsNullOrWhiteSpace(inputPath)
            ? throw new ArgumentException("Input path is required.", nameof(inputPath))
            : inputPath;
        ScenarioName = string.IsNullOrWhiteSpace(scenarioName)
            ? throw new ArgumentException("Scenario name is required.", nameof(scenarioName))
            : scenarioName;
        Info = info;
        ScenarioArgs = scenarioArgs?.ToArray()
            ?? throw new ArgumentNullException(nameof(scenarioArgs));
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
    /// Gets the raw scenario-specific arguments.
    /// </summary>
    public IReadOnlyList<string> ScenarioArgs { get; }
}
