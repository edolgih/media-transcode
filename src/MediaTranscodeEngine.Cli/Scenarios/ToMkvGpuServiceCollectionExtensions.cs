using MediaTranscodeEngine.Core.Execution;
using MediaTranscodeEngine.Core.Scenarios;
using MediaTranscodeEngine.Core.Scenarios.ToMkvGpu;
using Microsoft.Extensions.DependencyInjection;

namespace MediaTranscodeEngine.Cli.Scenarios;

internal static class ToMkvGpuServiceCollectionExtensions
{
    public static IServiceCollection AddToMkvGpuScenario(this IServiceCollection services)
    {
        services.AddSingleton<H264RemuxEligibilityPolicy>();
        services.AddSingleton<H264TimestampPolicy>();
        services.AddSingleton<H264AudioPolicy>();
        services.AddSingleton<H264RateControlPolicy>();
        services.AddSingleton<H264CommandBuilder>();
        services.AddSingleton(TranscodeScenario.CreateToMkvGpu());
        services.AddSingleton<ICodecExecutionStrategy, CopyCodecExecutionStrategy>();
        services.AddSingleton<ICodecExecutionStrategy, H264GpuCodecExecutionStrategy>();
        return services;
    }
}
