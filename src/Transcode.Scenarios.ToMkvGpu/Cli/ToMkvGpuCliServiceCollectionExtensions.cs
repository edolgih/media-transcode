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
    public static IServiceCollection AddToMkvGpuCliScenario(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(static services =>
        {
            var runtimeValues = services.GetRequiredService<RuntimeValues>();
            var logger = services.GetRequiredService<ILogger<ToMkvGpuFfmpegTool>>();
            return new ToMkvGpuFfmpegTool(runtimeValues.FfmpegPath!, logger);
        });
        services.AddSingleton<ToMkvGpuInfoFormatter>();
        services.AddSingleton<ICliScenarioHandler>(static services =>
            new ToMkvGpuCliScenarioHandler(
                services.GetRequiredService<ToMkvGpuInfoFormatter>(),
                services.GetRequiredService<ToMkvGpuFfmpegTool>(),
                services.GetRequiredService<FfmpegSampleMeasurer>()));

        return services;
    }
}
