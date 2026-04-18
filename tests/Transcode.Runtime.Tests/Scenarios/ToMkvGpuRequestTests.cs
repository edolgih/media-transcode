using FluentAssertions;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToMkvGpu.Core;

namespace Transcode.Runtime.Tests.Scenarios;

/*
Это тесты request-модели tomkvgpu.
Они проверяют нормализацию supported values и сценарные инварианты этой legacy-модели.
*/
/// <summary>
/// Verifies normalization and invariants of the ToMkvGpu request model.
/// </summary>
public sealed class ToMkvGpuRequestTests
{
    [Fact]
    public void Constructor_WithValidOptions_NormalizesValues()
    {
        var request = new ToMkvGpuRequest(
            overlayBackground: true,
            synchronizeAudio: true,
            keepSource: true,
            forceEncode: true,
            videoSettings: new VideoSettingsRequest(
                contentProfile: "Film",
                qualityProfile: "Default",
                cq: 24,
                maxrate: 3.7m,
                bufsize: 7.4m),
            downscale: new DownscaleRequest(576, "Bicubic"),
            nvencPreset: "P6",
            maxFramesPerSecond: 40);

        request.KeepSource.Should().BeTrue();
        request.OverlayBackground.Should().BeTrue();
        request.SynchronizeAudio.Should().BeTrue();
        request.ForceEncode.Should().BeTrue();
        request.Downscale.Should().NotBeNull();
        request.Downscale!.TargetHeight.Should().Be(576);
        request.Downscale.Algorithm.Should().NotBeNull();
        request.Downscale.Algorithm!.Value.Should().Be("bicubic");
        request.VideoSettings.Should().NotBeNull();
        request.VideoSettings!.ContentProfile.Should().NotBeNull();
        request.VideoSettings.ContentProfile!.Value.Should().Be("film");
        request.VideoSettings.QualityProfile.Should().NotBeNull();
        request.VideoSettings.QualityProfile!.Value.Should().Be("default");
        request.VideoSettings.Cq.Should().Be(24);
        request.VideoSettings.Maxrate.Should().Be(3.7m);
        request.VideoSettings.Bufsize.Should().Be(7.4m);
        request.NvencPreset.Should().Be(NvencPreset.P6);
        request.MaxFramesPerSecond.Should().Be(40);
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsUnsupported_Throws()
    {
        Action action = static () => _ = new ToMkvGpuRequest(nvencPreset: "p8");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("nvencPreset");
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsEmpty_Throws()
    {
        Action action = static () => _ = new ToMkvGpuRequest(nvencPreset: "");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("nvencPreset");
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsOmitted_UsesP6Preset()
    {
        var request = new ToMkvGpuRequest();

        request.NvencPreset.Should().Be(NvencPreset.P6);
    }

    [Fact]
    public void DownscaleRequest_WhenAlgorithmIsUnsupported_Throws()
    {
        Action action = static () => _ = new DownscaleRequest(576, "nearest");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("algorithm");
    }
}
