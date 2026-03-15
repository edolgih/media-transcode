using MediaTranscodeEngine.Cli.Scenarios;

namespace MediaTranscodeEngine.Cli.Parsing;

/*
Этот helper собирает help-текст CLI из общей части, зарегистрированных сценариев и текущей runtime-конфигурации.
*/
/// <summary>
/// Builds CLI help text from shared options, registered scenarios, and runtime configuration.
/// </summary>
internal static class CliHelpBuilder
{
    /// <summary>
    /// Builds help text using the supplied scenario registry.
    /// </summary>
    /// <param name="runtimeValues">Configured runtime executable paths.</param>
    /// <param name="registry">Registered CLI scenarios.</param>
    /// <returns>Rendered help text.</returns>
    public static string BuildHelpText(RuntimeValues runtimeValues, CliScenarioRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(runtimeValues);
        ArgumentNullException.ThrowIfNull(registry);

        var exeName = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(exeName))
        {
            exeName = "MediaTranscodeEngine.Cli.exe";
        }

        var lines = new List<string>
        {
            "MediaTranscodeEngine CLI",
            string.Empty,
            $"Usage: {exeName} [options]",
            string.Empty,
            "Options:"
        };

        AddOptionRows(lines, registry.GetHelpOptions());

        lines.Add(string.Empty);
        lines.Add("Configuration (appsettings / environment):");
        lines.Add($"  {nameof(RuntimeValues)}:FfprobePath current: {runtimeValues.FfprobePath}");
        lines.Add($"  {nameof(RuntimeValues)}:FfmpegPath  current: {runtimeValues.FfmpegPath}");

        lines.Add(string.Empty);
        lines.Add("Examples:");
        foreach (var example in registry.GetHelpExamples(exeName))
        {
            lines.Add($"  {example}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddOptionRows(List<string> lines, IReadOnlyList<CliHelpOption> options)
    {
        foreach (var option in options)
        {
            lines.Add($"  {option.Usage,-32} {option.HelpText}");
        }
    }
}
