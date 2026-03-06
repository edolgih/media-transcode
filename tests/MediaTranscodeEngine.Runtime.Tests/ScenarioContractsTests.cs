using MediaTranscodeEngine.Runtime.Plans;
using MediaTranscodeEngine.Runtime.Scenarios;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests;

public sealed class ScenarioContractsTests
{
    [Fact]
    public void BuildPlan_WhenVideoRequiresEncoding_ReturnsToolAgnosticPlan()
    {
        var scenario = new SampleScenario();
        var video = new SourceVideo(
            FilePath: @"C:\video\input.mp4",
            Container: "mp4",
            VideoCodec: "h264",
            AudioCodecs: ["aac"],
            Width: 1920,
            Height: 1080,
            FramesPerSecond: 29.97,
            Duration: TimeSpan.FromMinutes(42));

        var plan = scenario.BuildPlan(video);

        Assert.Equal("mkv", plan.TargetContainer);
        Assert.Equal("h265", plan.TargetVideoCodec);
        Assert.Equal("gpu", plan.PreferredBackend);
        Assert.Equal(720, plan.TargetHeight);
        Assert.Null(plan.TargetFramesPerSecond);
        Assert.False(plan.UseFrameInterpolation);
        Assert.False(plan.CopyVideo);
        Assert.True(plan.CopyAudio);
        Assert.False(plan.FixTimestamps);
        Assert.False(plan.KeepSource);
    }

    private sealed class SampleScenario : TranscodeScenario
    {
        public override string Name => "sample";

        public override TranscodePlan BuildPlan(SourceVideo video)
        {
            return new TranscodePlan(
                TargetContainer: "mkv",
                TargetVideoCodec: "h265",
                PreferredBackend: "gpu",
                TargetHeight: video.Height > 720 ? 720 : null,
                TargetFramesPerSecond: null,
                UseFrameInterpolation: false,
                CopyVideo: false,
                CopyAudio: true,
                FixTimestamps: false,
                KeepSource: false);
        }
    }
}
