using FluentAssertions;
using MediaTranscodeEngine.Runtime.Videos;

namespace MediaTranscodeEngine.Runtime.Tests.Videos;

public sealed class VideoInspectorTests
{
    [Fact]
    public void Load_WhenFilePathIsBlank_ThrowsArgumentException()
    {
        var sut = new FakeInspector(_ => CreateVideo());

        Action action = () => sut.Load("   ");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Load_WhenImplementationReturnsNull_ThrowsInvalidOperationException()
    {
        var sut = new FakeInspector(_ => null!);

        Action action = () => sut.Load(@"C:\video\input.mkv");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*null source video*");
    }

    [Fact]
    public void Load_WhenRelativePathIsProvided_PassesNormalizedPathToImplementation()
    {
        var capturedPath = string.Empty;
        var sut = new FakeInspector(path =>
        {
            capturedPath = path;
            return CreateVideo(filePath: path);
        });

        _ = sut.Load(@".\input.mkv");

        capturedPath.Should().Be(Path.GetFullPath(@".\input.mkv"));
    }

    private static SourceVideo CreateVideo(string filePath = @"C:\video\input.mkv")
    {
        return new SourceVideo(
            filePath: filePath,
            container: "mkv",
            videoCodec: "h264",
            audioCodecs: ["aac"],
            width: 1920,
            height: 1080,
            framesPerSecond: 29.97,
            duration: TimeSpan.FromMinutes(10));
    }

    private sealed class FakeInspector : VideoInspector
    {
        private readonly Func<string, SourceVideo> _load;

        public FakeInspector(Func<string, SourceVideo> load)
        {
            _load = load;
        }

        protected override SourceVideo LoadCore(string filePath)
        {
            return _load(filePath);
        }
    }
}
