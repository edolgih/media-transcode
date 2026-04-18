using FluentAssertions;
using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;
using Transcode.Scenarios.ToH264Gpu.Core;

namespace Transcode.Runtime.Tests.Scenarios;

/*
Это тесты self-validating request-модели toh264gpu.
Они проверяют нормализацию значений и защиту сценарных инвариантов.
*/
/// <summary>
/// Verifies normalization and invariants of the ToH264Gpu request model.
/// </summary>
public sealed class ToH264GpuRequestTests
{
    [Fact]
    public void Constructor_WithValidOptions_NormalizesValues()
    {
        var request = new ToH264GpuRequest(
            keepSource: true,
            forceEncode: true,
            downscale: new DownscaleRequest(576, "lanczos"),
            keepFramesPerSecond: true,
            videoSettings: new VideoSettingsRequest(
                contentProfile: "film",
                qualityProfile: "default",
                cq: 21,
                maxrate: 4.2m,
                bufsize: 8.4m),
            nvencPreset: "P6",
            denoise: true,
            synchronizeAudio: true,
            outputMkv: true);

        request.KeepSource.Should().BeTrue();
        request.ForceEncode.Should().BeTrue();
        request.Downscale.Should().NotBeNull();
        request.Downscale!.TargetHeight.Should().Be(576);
        request.Downscale.Algorithm.Should().NotBeNull();
        request.Downscale.Algorithm!.Value.Should().Be("lanczos");
        request.KeepFramesPerSecond.Should().BeTrue();
        request.VideoSettings.Should().NotBeNull();
        request.VideoSettings!.ContentProfile.Should().NotBeNull();
        request.VideoSettings.ContentProfile!.Value.Should().Be("film");
        request.VideoSettings.QualityProfile.Should().NotBeNull();
        request.VideoSettings.QualityProfile!.Value.Should().Be("default");
        request.VideoSettings.Cq.Should().Be(21);
        request.VideoSettings.Maxrate.Should().Be(4.2m);
        request.VideoSettings.Bufsize.Should().Be(8.4m);
        request.NvencPreset.Should().Be(NvencPreset.P6);
        request.Denoise.Should().BeTrue();
        request.SynchronizeAudio.Should().BeTrue();
        request.OutputMkv.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsUnsupported_Throws()
    {
        Action action = static () => _ = new ToH264GpuRequest(nvencPreset: "p8");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("nvencPreset");
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsEmpty_Throws()
    {
        Action action = static () => _ = new ToH264GpuRequest(nvencPreset: "");

        action.Should().Throw<ArgumentException>()
            .WithParameterName("nvencPreset");
    }

    [Fact]
    public void Constructor_WhenNvencPresetIsOmitted_UsesP6Preset()
    {
        var request = new ToH264GpuRequest();

        request.NvencPreset.Should().Be(NvencPreset.P6);
    }

    [Fact]
    public void Constructor_WhenDownscaleAlgorithmIsOmitted_LeavesAlgorithmUnspecified()
    {
        var request = new ToH264GpuRequest(downscale: new DownscaleRequest(576));

        request.Downscale.Should().NotBeNull();
        request.Downscale!.Algorithm.Should().BeNull();
    }

    [Fact]
    public void Constructor_WhenCqIsAboveH264Limit_Throws()
    {
        Action action = static () => _ = new ToH264GpuRequest(
            videoSettings: new VideoSettingsRequest(cq: 52));

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("cq");
    }
}
