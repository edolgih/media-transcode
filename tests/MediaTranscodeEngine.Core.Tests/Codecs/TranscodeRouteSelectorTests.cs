using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Codecs;

namespace MediaTranscodeEngine.Core.Tests.Codecs;

public class TranscodeRouteSelectorTests
{
    [Fact]
    public void Select_WhenCopyRouteMatches_ReturnsCopyRoute()
    {
        var expected = new NamedRoute("copy", _ => true);
        var sut = new TranscodeRouteSelector(
        [
            expected,
            new NamedRoute("other", _ => false)
        ]);
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var actual = sut.Select(request);

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Select_WhenH264GpuMatches_ReturnsH264GpuRoute()
    {
        var expected = new NamedRoute("h264", static request =>
            request.ComputeMode.Equals(RequestContracts.General.GpuComputeMode, StringComparison.OrdinalIgnoreCase) &&
            request.PreferH264);
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("copy", _ => false),
            expected
        ]);
        var request = TranscodeRequest.Create(
            InputPath: "C:\\video\\movie.mp4",
            PreferH264: true);

        var actual = sut.Select(request);

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public void Select_WhenNoRouteMatches_ThrowsExpectedError()
    {
        var sut = new TranscodeRouteSelector(
        [
            new NamedRoute("copy", _ => false)
        ]);
        var request = TranscodeRequest.Create(InputPath: "C:\\video\\movie.mp4");

        var act = () => sut.Select(request);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No transcode route was found*");
    }

    private sealed class NamedRoute : ITranscodeRoute
    {
        private readonly Func<TranscodeRequest, bool> _predicate;

        public NamedRoute(string name, Func<TranscodeRequest, bool> predicate)
        {
            Name = name;
            _predicate = predicate;
        }

        public string Name { get; }

        public bool CanHandle(TranscodeRequest request) => _predicate(request);

        public string Process(TranscodeRequest request) => Name;

        public string ProcessWithProbeResult(TranscodeRequest request, ProbeResult? probe) => Name;

        public string ProcessWithProbeJson(TranscodeRequest request, string? probeJson) => Name;
    }
}
