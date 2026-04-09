using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Scenarios.ToMkvGpu.Cli;

/*
Это регистрация зависимостей CLI для сценария tomkvgpu.
Она связывает parser/handler и ffmpeg-tool через DI и конфигурацию приложения.
*/
/// <summary>
/// Registers CLI services for the <c>tomkvgpu</c> scenario.
/// </summary>
public static class ToMkvGpuCliServiceCollectionExtensions
{
    /*
    Это extension-точка подключения сценария в общий CLI host.
    */
    /// <summary>
    /// Registers all CLI services required by the <c>tomkvgpu</c> scenario.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <param name="configuration">Resolved application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddToMkvGpuCliScenario(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var ffmpegPath = GetRequiredConfigurationValue(configuration, ToolConfigurationKeys.FfmpegPath);

        services.AddSingleton(services =>
        {
            var logger = services.GetRequiredService<ILogger<ToMkvGpuFfmpegTool>>();
            return new ToMkvGpuFfmpegTool(ffmpegPath, logger);
        });
        services.AddSingleton<ToMkvGpuInfoFormatter>();
        services.AddSingleton<ICliScenarioHandler>(static services =>
            new ToMkvGpuCliScenarioHandler(
                services.GetRequiredService<ToMkvGpuInfoFormatter>(),
                services.GetRequiredService<ToMkvGpuFfmpegTool>()));

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
            throw new InvalidOperationException($"Configuration key '{key}' is required for tomkvgpu.");
        }

        return value;
    }
}
