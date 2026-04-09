using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Transcode.Cli.Core;
using Transcode.Cli.Core.Scenarios;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/*
Это регистрация зависимостей CLI для сценария toh264rife.
Здесь связываются parser/handler и tool-адаптер с настройками из конфигурации.
*/
/// <summary>
/// Registers CLI services for the <c>toh264rife</c> scenario.
/// </summary>
public static class ToH264RifeCliServiceCollectionExtensions
{
	/*
	Это extension-точка подключения сценария в общий CLI host.
	*/
	/// <summary>
	/// Registers all CLI services required by the <c>toh264rife</c> scenario.
	/// </summary>
	/// <param name="services">Service collection being configured.</param>
	/// <param name="configuration">Resolved application configuration.</param>
	/// <returns>The same service collection for chaining.</returns>
	public static IServiceCollection AddToH264RifeCliScenario(this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var dockerImage = GetRequiredValue(configuration, ToH264RifeCliConfigurationKeys.DockerImage);

		services.AddSingleton(services =>
		{
			var logger = services.GetRequiredService<ILogger<ToH264RifeTool>>();
			return new ToH264RifeTool(dockerImage, logger);
		});
		services.AddSingleton<ToH264RifeInfoFormatter>();
		services.AddSingleton<ICliScenarioHandler>(static services =>
			new ToH264RifeCliScenarioHandler(
				services.GetRequiredService<ToH264RifeInfoFormatter>(),
				services.GetRequiredService<ToH264RifeTool>()));

		return services;
	}

	/*
	Это чтение обязательного значения из конфигурации сценария.
	*/
	/// <summary>
	/// Reads a required scenario configuration value.
	/// </summary>
	/// <param name="configuration">Resolved application configuration.</param>
	/// <param name="key">Configuration key.</param>
	/// <returns>Non-empty configuration value.</returns>
	private static string GetRequiredValue(IConfiguration configuration, string key)
	{
		var value = configuration[key];
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException($"Configuration key '{key}' is required for toh264rife.");
		}

		return value;
	}
}
