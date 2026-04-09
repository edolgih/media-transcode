using Microsoft.Extensions.Configuration;
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
    /*
    Это построение: help text using the supplied scenario registry
    */
    /// <summary>
    /// Builds help text using the supplied scenario registry.
    /// </summary>
    /// <param name="configuration">Resolved CLI configuration.</param>
    /// <param name="registry">Registered CLI scenarios.</param>
    /// <returns>Rendered help text.</returns>
    public static string BuildHelpText(IConfiguration configuration, CliScenarioRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(configuration);
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
        lines.Add($"  {ToolConfigurationKeys.FfprobePath} current: {configuration[ToolConfigurationKeys.FfprobePath]}");
        lines.Add($"  {ToolConfigurationKeys.FfmpegPath}  current: {configuration[ToolConfigurationKeys.FfmpegPath]}");

        foreach (var handler in registry.GetScenarioHandlersOrdered())
        {
            lines.Add(string.Empty);
            lines.Add($"Scenario: {handler.Name}");

            if (handler.HelpOptions.Count > 0)
            {
                lines.Add("  Options:");
                AddOptionRows(lines, handler.HelpOptions, indent: "    ");
            }

            var configurationRows = handler.GetConfigurationDisplayRows(configuration);
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
