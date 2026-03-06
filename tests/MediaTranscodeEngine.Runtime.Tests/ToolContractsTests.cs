using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Tools;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests;

public sealed class ToolContractsTests
{
    [Fact]
    public void BuildExecution_WhenToolCanHandlePlan_ReturnsCommandSequence()
    {
        var tool = new SampleTool();
        var video = new SourceVideo(
            FilePath: @"C:\video\input.mp4",
            Container: "mp4",
            VideoCodec: "h264",
            AudioCodecs: ["aac"],
            Width: 1920,
            Height: 1080,
            FramesPerSecond: 29.97,
            Duration: TimeSpan.FromMinutes(5));
        var plan = new TranscodePlan(
            TargetContainer: "mkv",
            TargetVideoCodec: "h265",
            PreferredBackend: "gpu",
            TargetHeight: 720,
            TargetFramesPerSecond: 60,
            UseFrameInterpolation: true,
            CopyVideo: false,
            CopyAudio: true,
            FixTimestamps: false,
            KeepSource: false,
            OutputPath: @"C:\video\input.mkv");

        Assert.True(tool.CanHandle(plan));

        var execution = tool.BuildExecution(video, plan);

        Assert.Equal("sample-tool", execution.ToolName);
        Assert.Equal(2, execution.Commands.Count);
        Assert.Equal("interpolate-fps \"C:\\video\\input.mp4\" 60", execution.Commands[0]);
        Assert.Equal("encode-video \"C:\\video\\input.mp4\" h265 gpu \"C:\\video\\input.mkv\"", execution.Commands[1]);
    }

    private sealed class SampleTool : ITranscodeTool
    {
        public string Name => "sample-tool";

        public bool CanHandle(TranscodePlan plan)
        {
            return plan.UseFrameInterpolation && !plan.CopyVideo;
        }

        public ToolExecution BuildExecution(SourceVideo video, TranscodePlan plan)
        {
            return new ToolExecution(
                ToolName: Name,
                Commands:
                [
                    $"interpolate-fps \"{video.FilePath}\" {plan.TargetFramesPerSecond}",
                    $"encode-video \"{video.FilePath}\" {plan.TargetVideoCodec} {plan.PreferredBackend} \"{plan.OutputPath}\""
                ]);
        }
    }
}
