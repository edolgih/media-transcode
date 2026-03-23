using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/// <summary>
/// Registers CLI services for the <c>toh264rife</c> scenario.
/// </summary>
public static class ToH264RifeCliServiceCollectionExtensions
{
    public static IServiceCollection AddToH264RifeCliScenario(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(static services =>
        {
            var runtimeValues = services.GetRequiredService<RuntimeValues>();
            var logger = services.GetRequiredService<ILogger<ToH264RifeTool>>();
            return new ToH264RifeTool(runtimeValues.FfmpegPath!, runtimeValues.RifeNcnnPath, logger);
        });
        services.AddSingleton<ToH264RifeInfoFormatter>();
        services.AddSingleton<ICliScenarioHandler>(static services =>
            new ToH264RifeCliScenarioHandler(
                services.GetRequiredService<ToH264RifeInfoFormatter>(),
                services.GetRequiredService<ToH264RifeTool>()));

        return services;
    }
}
