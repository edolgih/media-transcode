using FluentAssertions;

namespace Transcode.Runtime.Tests.Scenarios;

public sealed class ToH264RifeDockerScriptTests
{
    [Fact]
    public void FinalMux_WhenAudioIsCopied_DisablesForcedInterleaveDelta()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "rife-trt", "run-rife-trt.sh");

        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("-max_muxing_queue_size 4096");
        script.Should().Contain("-max_interleave_delta 0");
        script.IndexOf("-max_muxing_queue_size 4096", StringComparison.Ordinal)
            .Should().BeLessThan(script.IndexOf("-max_interleave_delta 0", StringComparison.Ordinal));
    }

    [Fact]
    public void FinalMux_WhenContainerIsMkv_WritesCuesToTheFront()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "rife-trt", "run-rife-trt.sh");

        var script = File.ReadAllText(scriptPath);

        script.Should().Contain("elif [[ \"${container_name,,}\" == \"mkv\" ]]; then");
        script.Should().Contain("ffmpeg_args+=(-cues_to_front 1)");
    }
}
