using Microsoft.Extensions.Logging;

namespace Transcode.Cli.Core.Scenarios;

/*
Этот тип описывает, как конкретный сценарий превращает ошибку в лог-токен и пользовательский вывод CLI.
*/
/// <summary>
/// Describes scenario-specific failure handling for logging and final CLI output.
/// </summary>
public sealed record CliScenarioFailure(
    LogLevel Level,
    string LogToken,
    string NonInfoOutput,
    string InfoOutput);
