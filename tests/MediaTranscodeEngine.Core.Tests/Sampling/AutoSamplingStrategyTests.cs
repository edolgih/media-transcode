using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Sampling;

namespace MediaTranscodeEngine.Core.Tests.Sampling;

public class AutoSamplingStrategyTests
{
    [Fact]
    public void Resolve_WhenModeFast_UsesFastProvider()
    {
        var sut = CreateSut();
        var accurateCalls = 0;
        var fastCalls = 0;

        _ = sut.Resolve(new AutoSamplingContext(
            ContentProfile: "anime",
            QualityProfile: "default",
            BaseSettings: CreateBaseSettings(),
            SourceHeight: 1080,
            Mode: AutoSamplingMode.Fast,
            AccurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            FastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 45.0;
            }));

        fastCalls.Should().BeGreaterThan(0);
        accurateCalls.Should().Be(0);
    }

    [Fact]
    public void Resolve_WhenModeHybrid_UsesFastThenAccurate()
    {
        var sut = CreateSut();
        var accurateCalls = 0;
        var fastCalls = 0;

        _ = sut.Resolve(new AutoSamplingContext(
            ContentProfile: "anime",
            QualityProfile: "default",
            BaseSettings: CreateBaseSettings(),
            SourceHeight: 1080,
            Mode: AutoSamplingMode.Hybrid,
            AccurateReductionProvider: (_, _, _) =>
            {
                accurateCalls++;
                return 45.0;
            },
            FastReductionProvider: (_, _, _) =>
            {
                fastCalls++;
                return 30.0;
            }));

        fastCalls.Should().BeGreaterThan(0);
        accurateCalls.Should().BeGreaterThan(0);
    }

    private static IAutoSamplingStrategy CreateSut()
    {
        return new PolicyDrivenAutoSamplingStrategy(
            profileRepository: new LegacyPolicyConfigProfileRepository(new StaticProfileRepository()),
            policy: new ProfilePolicy());
    }

    private static QualitySettings CreateBaseSettings()
    {
        return new QualitySettings(
            Cq: 23,
            Maxrate: 2.4,
            Bufsize: 4.8,
            DownscaleAlgo: "bilinear");
    }
}
