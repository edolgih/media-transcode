using Microsoft.Extensions.Configuration;
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
	public static IServiceCollection AddToH264RifeCliScenario(this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var ffmpegPath = CliPathResolver.GetRequiredExecutable(configuration, ToolConfigurationKeys.FfmpegPath, "toh264rife");
		var rifeNcnnPath = CliPathResolver.GetRequiredExecutable(configuration, ToH264RifeCliConfigurationKeys.RifeNcnnPath, "toh264rife");

		services.AddSingleton(services =>
		{
			var logger = services.GetRequiredService<ILogger<ToH264RifeTool>>();
			return new ToH264RifeTool(ffmpegPath, rifeNcnnPath, logger);
		});
		services.AddSingleton<ToH264RifeInfoFormatter>();
		services.AddSingleton<ICliScenarioHandler>(static services =>
			new ToH264RifeCliScenarioHandler(
				services.GetRequiredService<ToH264RifeInfoFormatter>(),
				services.GetRequiredService<ToH264RifeTool>()));

		return services;
	}
}
