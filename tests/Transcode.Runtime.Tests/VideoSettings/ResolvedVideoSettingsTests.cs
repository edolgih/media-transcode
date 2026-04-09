using FluentAssertions;
using Transcode.Core.VideoSettings;

namespace Transcode.Runtime.Tests.VideoSettings;

/// <summary>
/// Verifies invariants and state transitions of resolved video settings.
/// </summary>
public sealed class ResolvedVideoSettingsTests
{
    [Fact]
    public void CapToSourceBitrate_WhenSourceBitrateIsLower_CapsMaxrateAndBufsize()
    {
        var settings = CreateSettings();

        var actual = settings.CapToSourceBitrate(
            sourceVideoBitrate: 2_500_000,
            request: null,
            bufsizeMultiplier: 2.0m);

        actual.Maxrate.Should().Be(2.5m);
        actual.Bufsize.Should().Be(5.0m);
        actual.Cq.Should().Be(settings.Cq);
    }

    [Fact]
    public void CapToSourceBitrate_WhenRequestHasManualRateOverrides_KeepsOriginalSettings()
    {
        var settings = CreateSettings();
        var request = new VideoSettingsRequest(cq: 20);

        var actual = settings.CapToSourceBitrate(
            sourceVideoBitrate: 2_500_000,
            request: request,
            bufsizeMultiplier: 2.0m);

        actual.Should().Be(settings);
    }

    private static ResolvedVideoSettings CreateSettings()
    {
        return ResolvedVideoSettings.FromDefaults(
            new VideoSettingsDefaults(
                "film",
                "default",
                23,
                3.8m,
                7.6m,
                "bilinear",
                18,
                35,
                1.6m,
                8.0m));
    }
}
