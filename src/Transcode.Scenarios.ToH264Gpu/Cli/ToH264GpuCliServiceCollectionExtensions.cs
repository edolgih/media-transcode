using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Scenarios.ToH264Gpu.Cli;

/*
Это регистрация зависимостей CLI для сценария toh264gpu.
Здесь подключаются tool, formatter и CLI-handler с параметрами из конфигурации.
*/
/// <summary>
/// Registers CLI services for the <c>toh264gpu</c> scenario.
/// </summary>
public static class ToH264GpuCliServiceCollectionExtensions
{
    /*
    Это extension-точка подключения сценария в общий CLI host.
    */
    /// <summary>
    /// Registers all CLI services required by the <c>toh264gpu</c> scenario.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <param name="configuration">Resolved application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddToH264GpuCliScenario(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var ffmpegPath = GetRequiredConfigurationValue(configuration, ToolConfigurationKeys.FfmpegPath);

        services.AddSingleton(services =>
        {
            var logger = services.GetRequiredService<ILogger<ToH264GpuFfmpegTool>>();
            return new ToH264GpuFfmpegTool(ffmpegPath, logger);
        });
        services.AddSingleton<ToH264GpuInfoFormatter>();
        services.AddSingleton<ICliScenarioHandler>(static services =>
            new ToH264GpuCliScenarioHandler(
                services.GetRequiredService<ToH264GpuInfoFormatter>(),
                services.GetRequiredService<ToH264GpuFfmpegTool>()));

        return services;
    }

    /*
    Это чтение обязательного значения конфигурации для сценария.
    */
    /// <summary>
    /// Reads a required scenario configuration value.
    /// </summary>
    /// <param name="configuration">Resolved application configuration.</param>
    /// <param name="key">Configuration key.</param>
    /// <returns>Non-empty configuration value.</returns>
    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration key '{key}' is required for toh264gpu.");
        }

        return value;
    }
}
