using FluentAssertions;
using MediaTranscodeEngine.Core.Classification;

namespace MediaTranscodeEngine.Core.Tests.Classification;

public class InputClassificationTests
{
    [Fact]
    public void Classify_WhenHeightAndFpsProvided_ReturnsExpectedBucketKey()
    {
        IInputClassifier sut = new DefaultInputClassifier();

        var actual = sut.Classify(sourceHeight: 1080, sourceFps: 59.94);

        actual.ResolutionBucketKey.Should().Be("fhd_1080");
        actual.FpsBucketKey.Should().Be("high_fps");
    }
}
