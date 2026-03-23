using Transcode.Cli.Core.Scenarios;

namespace Transcode.Cli.Core.Parsing;

/*
Этот helper собирает help-текст CLI из общей части, зарегистрированных сценариев и текущей конфигурации внешних утилит.
*/
/// <summary>
/// Builds CLI help text from shared options, registered scenarios, and external tool configuration.
/// </summary>
internal static class CliHelpBuilder
{
    /// <summary>
    /// Builds help text using the supplied scenario registry.
    /// </summary>
    /// <param name="runtimeValues">Configured external tool executable paths.</param>
    /// <param name="registry">Registered CLI scenarios.</param>
    /// <returns>Rendered help text.</returns>
    public static string BuildHelpText(RuntimeValues runtimeValues, CliScenarioRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(runtimeValues);
        ArgumentNullException.ThrowIfNull(registry);

        var exeName = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeName))
        {
            exeName = "Transcode.Cli.exe";
        }

        var lines = new List<string>
        {
            "Transcode CLI",
            string.Empty,
            $"Usage: {exeName} [options]",
            string.Empty,
            "Options:"
        };

        AddOptionRows(
            lines,
            CliCommonOptions.CreateHelpOptions(registry.GetSupportedScenarioDisplay()),
            indent: "  ");

        lines.Add(string.Empty);
        lines.Add("Configuration (appsettings / environment):");
        lines.Add($"  {nameof(RuntimeValues)}:FfprobePath current: {runtimeValues.FfprobePath}");
        lines.Add($"  {nameof(RuntimeValues)}:FfmpegPath  current: {runtimeValues.FfmpegPath}");

        foreach (var handler in registry.GetScenarioHandlersOrdered())
        {
            lines.Add(string.Empty);
            lines.Add($"Scenario: {handler.Name}");

            if (handler.HelpOptions.Count > 0)
            {
                lines.Add("  Options:");
                AddOptionRows(lines, handler.HelpOptions, indent: "    ");
            }

            var configurationRows = handler.GetConfigurationDisplayRows(runtimeValues);
            if (configurationRows.Count > 0)
            {
                lines.Add("  Configuration:");
                foreach (var row in configurationRows)
                {
                    lines.Add($"    {row}");
                }
            }

            var examples = handler.GetHelpExamples(exeName);
            if (examples.Count > 0)
            {
                lines.Add("  Examples:");
                foreach (var example in examples)
                {
                    lines.Add($"    {example}");
                }
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddOptionRows(List<string> lines, IReadOnlyList<CliHelpOption> options, string indent)
    {
        foreach (var option in options)
        {
            lines.Add($"{indent}{option.Usage,-32} {option.HelpText}");
        }
    }
}
