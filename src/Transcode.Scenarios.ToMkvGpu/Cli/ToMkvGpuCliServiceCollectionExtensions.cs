using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Scenarios.ToMkvGpu.Cli;

/// <summary>
/// Registers CLI services for the <c>tomkvgpu</c> scenario.
/// </summary>
public static class ToMkvGpuCliServiceCollectionExtensions
{
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
                services.GetRequiredService<ToMkvGpuFfmpegTool>(),
                services.GetRequiredService<FfmpegSampleMeasurer>()));

        return services;
    }

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
