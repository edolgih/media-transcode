namespace Transcode.Cli.Core.Scenarios;

/*
Этот реестр хранит зарегистрированные CLI-сценарии и дает общий доступ
к их именам, help-данным и lookup по имени или legacy-токену.
*/
/// <summary>
/// Stores registered CLI scenarios and exposes lookup helpers used by parsing, help rendering, and processing.
/// </summary>
internal sealed class CliScenarioRegistry
{
    private readonly IReadOnlyDictionary<string, ICliScenarioHandler> _handlersByName;
    private readonly IReadOnlyDictionary<string, string> _legacyScenarioNamesByToken;

    /*
    Это создание и инициализация: a registry from the supplied scenario handlers
    */
    /// <summary>
    /// Initializes a registry from the supplied scenario handlers.
    /// </summary>
    /// <param name="handlers">Registered scenario handlers.</param>
    public CliScenarioRegistry(IEnumerable<ICliScenarioHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var handlerList = handlers.ToArray();
        if (handlerList.Length == 0)
        {
            throw new ArgumentException("At least one CLI scenario handler must be registered.", nameof(handlers));
        }

        _handlersByName = handlerList.ToDictionary(
            static handler => handler.Name,
            StringComparer.OrdinalIgnoreCase);
        _legacyScenarioNamesByToken = BuildLegacyScenarioNames(handlerList);
    }

    /*
    Это попытка найти зарегистрированный сценарий по его имени.
    */
    /// <summary>
    /// Tries to resolve a registered scenario by name.
    /// </summary>
    /// <param name="scenarioName">Scenario name.</param>
    /// <param name="handler">Resolved scenario handler.</param>
    /// <returns><see langword="true"/> when the scenario exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetScenario(string scenarioName, out ICliScenarioHandler handler)
    {
        return _handlersByName.TryGetValue(scenarioName, out handler!);
    }

    /*
    Это попытка сопоставить legacy-токен команды с именем сценария.
    */
    /// <summary>
    /// Tries to resolve a legacy command token to its scenario name.
    /// </summary>
    /// <param name="token">Legacy token.</param>
    /// <param name="scenarioName">Resolved scenario name.</param>
    /// <returns><see langword="true"/> when the token is recognized; otherwise <see langword="false"/>.</returns>
    public bool TryGetLegacyScenarioName(string token, out string scenarioName)
    {
        return _legacyScenarioNamesByToken.TryGetValue(token, out scenarioName!);
    }

    /*
    Это возврат: registered scenario handlers ordered for deterministic help output
    */
    /// <summary>
    /// Returns registered scenario handlers ordered for deterministic help output.
    /// </summary>
    /// <returns>Scenario handlers ordered by scenario name.</returns>
    public IReadOnlyList<ICliScenarioHandler> GetScenarioHandlersOrdered()
    {
        return _handlersByName.Values
            .OrderBy(static handler => handler.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /*
    Это возврат: the supported scenario names as a display string
    */
    /// <summary>
    /// Returns the supported scenario names as a display string.
    /// </summary>
    /// <returns>Comma-separated scenario list.</returns>
    public string GetSupportedScenarioDisplay()
    {
        return string.Join(", ",
            _handlersByName.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyDictionary<string, string> BuildLegacyScenarioNames(
        IReadOnlyList<ICliScenarioHandler> handlers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handler in handlers)
        {
            foreach (var token in handler.LegacyCommandTokens)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                result[token.Trim()] = handler.Name;
            }
        }

        return result;
    }
}
