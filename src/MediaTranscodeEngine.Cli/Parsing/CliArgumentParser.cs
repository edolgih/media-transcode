using MediaTranscodeEngine.Cli.Scenarios;

namespace MediaTranscodeEngine.Cli.Parsing;

/*
Этот парсер разбирает только общие аргументы CLI.
Scenario-specific аргументы он не интерпретирует, а передает выбранному сценарию как есть.
*/
/// <summary>
/// Parses common CLI arguments and delegates scenario-specific validation to the selected scenario handler.
/// </summary>
internal static class CliArgumentParser
{
    /// <summary>
    /// Parses CLI arguments using the default scenario registry.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="parsed">Common parse result on success.</param>
    /// <param name="errorText">Error message on failure.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(
        string[] args,
        out CliParseResult parsed,
        out string? errorText)
    {
        return TryParse(args, CliScenarioRegistry.Default, out parsed, out errorText);
    }

    /// <summary>
    /// Parses CLI arguments using the supplied scenario registry.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="registry">Available CLI scenarios.</param>
    /// <param name="parsed">Common parse result on success.</param>
    /// <param name="errorText">Error message on failure.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(
        string[] args,
        CliScenarioRegistry registry,
        out CliParseResult parsed,
        out string? errorText)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(registry);

        parsed = default!;
        errorText = null;

        var inputs = new List<string>();
        var scenarioArgs = new List<string>();
        string? scenarioName = null;
        var info = false;

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (registry.TryGetLegacyScenarioName(token, out var suggestedScenarioName))
            {
                errorText = $"Do not use legacy scenario command tokens. Use --scenario {suggestedScenarioName}.";
                return false;
            }

            if (string.Equals(token, CliCommonOptions.InputOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadRequiredValue(args, ref i, token, out var inputPath, out errorText))
                {
                    return false;
                }

                inputs.Add(inputPath);
                continue;
            }

            if (string.Equals(token, CliCommonOptions.ScenarioOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadRequiredValue(args, ref i, token, out scenarioName, out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, CliCommonOptions.InfoOptionName, StringComparison.OrdinalIgnoreCase))
            {
                info = true;
                continue;
            }

            if (token.StartsWith("-", StringComparison.Ordinal))
            {
                scenarioArgs.Add(token);
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    scenarioArgs.Add(args[i + 1]);
                    i++;
                }

                continue;
            }

            errorText = $"Unexpected argument: {token}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(scenarioName))
        {
            errorText = $"Scenario is required. Use --scenario <name>. Available scenarios: {registry.GetSupportedScenarioDisplay()}.";
            return false;
        }

        if (!registry.TryGetScenario(scenarioName, out var handler))
        {
            errorText = $"Unsupported scenario: {scenarioName}. Available scenarios: {registry.GetSupportedScenarioDisplay()}.";
            return false;
        }

        parsed = new CliParseResult(
            inputs: inputs.ToArray(),
            scenario: scenarioName,
            info: info,
            scenarioArgs: scenarioArgs.ToArray());

        return handler.TryValidate(parsed.ScenarioArgs, out errorText);
    }

    private static bool TryReadRequiredValue(
        string[] args,
        ref int index,
        string optionName,
        out string value,
        out string? errorText)
    {
        value = string.Empty;
        errorText = null;

        var valueIndex = index + 1;
        if (valueIndex >= args.Length)
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        var token = args[valueIndex];
        if (token.StartsWith("-", StringComparison.Ordinal))
        {
            errorText = $"{optionName} requires a value.";
            return false;
        }

        value = token;
        index = valueIndex;
        return true;
    }
}
