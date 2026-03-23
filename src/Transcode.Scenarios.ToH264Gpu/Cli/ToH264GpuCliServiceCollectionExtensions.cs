using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Scenarios.ToH264Gpu.Cli;

/// <summary>
/// Registers CLI services for the <c>toh264gpu</c> scenario.
/// </summary>
public static class ToH264GpuCliServiceCollectionExtensions
{
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
                services.GetRequiredService<ToH264GpuFfmpegTool>(),
                services.GetRequiredService<FfmpegSampleMeasurer>()));

        return services;
    }

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
