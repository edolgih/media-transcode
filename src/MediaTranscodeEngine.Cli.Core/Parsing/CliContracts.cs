namespace MediaTranscodeEngine.Cli.Parsing;

/*
Это результат общего разбора CLI-аргументов до того, как сценарий интерпретирует свои собственные опции.
*/
/// <summary>
/// Represents the common CLI parse result before scenario-specific arguments are interpreted.
/// </summary>
public sealed class CliParseResult
{
    /// <summary>
    /// Initializes the common CLI parse result.
    /// </summary>
    /// <param name="inputs">Parsed input paths.</param>
    /// <param name="scenario">Selected scenario name.</param>
    /// <param name="info">Whether info mode is enabled.</param>
    /// <param name="scenarioArgs">Raw scenario-specific arguments.</param>
    public CliParseResult(
        IReadOnlyList<string> inputs,
        string scenario,
        bool info,
        IReadOnlyList<string> scenarioArgs)
    {
        Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        Scenario = string.IsNullOrWhiteSpace(scenario)
            ? throw new ArgumentException("Scenario name is required.", nameof(scenario))
            : scenario;
        Info = info;
        ScenarioArgs = scenarioArgs ?? throw new ArgumentNullException(nameof(scenarioArgs));
    }

    /// <summary>
    /// Gets the parsed input paths.
    /// </summary>
    public IReadOnlyList<string> Inputs { get; }

    /// <summary>
    /// Gets the selected scenario name.
    /// </summary>
    public string Scenario { get; }

    /// <summary>
    /// Gets a value indicating whether info mode is enabled.
    /// </summary>
    public bool Info { get; }

    /// <summary>
    /// Gets the raw scenario-specific arguments.
    /// </summary>
    public IReadOnlyList<string> ScenarioArgs { get; }
}

/*
Это одна строка справки для help-вывода CLI: синтаксис опции и краткое пояснение.
*/
/// <summary>
/// Describes one help row shown in CLI usage output.
/// </summary>
public sealed class CliHelpOption
{
    /// <summary>
    /// Initializes a CLI help row.
    /// </summary>
    /// <param name="usage">Displayed usage syntax.</param>
    /// <param name="helpText">Human-readable help text.</param>
    public CliHelpOption(string usage, string helpText)
    {
        Usage = string.IsNullOrWhiteSpace(usage)
            ? throw new ArgumentException("Usage is required.", nameof(usage))
            : usage;
        HelpText = string.IsNullOrWhiteSpace(helpText)
            ? throw new ArgumentException("Help text is required.", nameof(helpText))
            : helpText;
    }

    /// <summary>
    /// Gets the displayed usage syntax.
    /// </summary>
    public string Usage { get; }

    /// <summary>
    /// Gets the human-readable help text.
    /// </summary>
    public string HelpText { get; }
}

/*
Здесь собраны общие имена CLI-опций и генерация help-строк для shared-части CLI.
*/
/// <summary>
/// Defines shared CLI option names and shared help rows.
/// </summary>
public static class CliCommonOptions
{
    public const string HelpOptionName = "--help";
    public const string ShortHelpOptionName = "-h";
    public const string InputOptionName = "--input";
    public const string ScenarioOptionName = "--scenario";
    public const string InfoOptionName = "--info";

    /// <summary>
    /// Creates help rows for the shared CLI options.
    /// </summary>
    /// <param name="supportedScenarioDisplay">Display string with supported scenario names.</param>
    /// <returns>Shared help rows.</returns>
    public static IReadOnlyList<CliHelpOption> CreateHelpOptions(string supportedScenarioDisplay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(supportedScenarioDisplay);

        return
        [
            new CliHelpOption("--help, -h", "Show help."),
            new CliHelpOption("--input <path>", "Input file path."),
            new CliHelpOption("--scenario <name>", $"Scenario name. Required. Available: {supportedScenarioDisplay}."),
            new CliHelpOption("--info", "Show per-file runtime decision markers.")
        ];
    }
}
