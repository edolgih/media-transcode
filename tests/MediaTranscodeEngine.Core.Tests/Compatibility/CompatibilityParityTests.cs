using FluentAssertions;
using MediaTranscodeEngine.Core.Compatibility;

namespace MediaTranscodeEngine.Core.Tests.Compatibility;

public class CompatibilityParityTests
{
    [Fact]
    public void Process_WhenLegacyCasesProvided_MatchesLegacyBehavior()
    {
        IStreamCompatibilityPolicy sut = new DefaultStreamCompatibilityPolicy();

        var cases = new[]
        {
            new StreamCompatibilityInput(
                IsMkvInput: true,
                HasAudioStream: true,
                IsVideoCopyCompatible: true,
                HasNonAacAudio: false,
                ForceSyncAudio: false,
                NeedVideoEncode: false),
            new StreamCompatibilityInput(
                IsMkvInput: true,
                HasAudioStream: true,
                IsVideoCopyCompatible: true,
                HasNonAacAudio: true,
                ForceSyncAudio: false,
                NeedVideoEncode: false),
            new StreamCompatibilityInput(
                IsMkvInput: false,
                HasAudioStream: true,
                IsVideoCopyCompatible: false,
                HasNonAacAudio: false,
                ForceSyncAudio: true,
                NeedVideoEncode: true),
            new StreamCompatibilityInput(
                IsMkvInput: true,
                HasAudioStream: false,
                IsVideoCopyCompatible: true,
                HasNonAacAudio: false,
                ForceSyncAudio: false,
                NeedVideoEncode: false)
        };

        foreach (var input in cases)
        {
            var expectedNeedAudioEncode = input.HasAudioStream &&
                                          (input.HasNonAacAudio || input.NeedVideoEncode || input.ForceSyncAudio);
            var expectedNeedContainerChange = !input.IsMkvInput;
            var expectedCopyPath = input.IsMkvInput && !input.NeedVideoEncode && !expectedNeedAudioEncode;

            var actual = sut.Decide(input);

            actual.NeedAudioEncode.Should().Be(expectedNeedAudioEncode);
            actual.NeedContainerChange.Should().Be(expectedNeedContainerChange);
            actual.IsCopyPath.Should().Be(expectedCopyPath);
        }
    }
}
