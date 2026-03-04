using FluentAssertions;
using MediaTranscodeEngine.Core.Compatibility;

namespace MediaTranscodeEngine.Core.Tests.Compatibility;

public class StreamCompatibilityPolicyTests
{
    [Fact]
    public void Decide_WhenAudioNonAac_ReturnsAudioReencode()
    {
        IStreamCompatibilityPolicy sut = new DefaultStreamCompatibilityPolicy();

        var decision = sut.Decide(new StreamCompatibilityInput(
            IsMkvInput: true,
            HasAudioStream: true,
            IsVideoCopyCompatible: true,
            HasNonAacAudio: true,
            ForceSyncAudio: false,
            NeedVideoEncode: false));

        decision.NeedAudioEncode.Should().BeTrue();
        decision.Reasons.Should().Contain("audio non-aac");
    }

    [Fact]
    public void Decide_WhenSyncRequested_ReturnsForceSync()
    {
        IStreamCompatibilityPolicy sut = new DefaultStreamCompatibilityPolicy();

        var decision = sut.Decide(new StreamCompatibilityInput(
            IsMkvInput: true,
            HasAudioStream: true,
            IsVideoCopyCompatible: true,
            HasNonAacAudio: false,
            ForceSyncAudio: true,
            NeedVideoEncode: false));

        decision.ForceSyncAudio.Should().BeTrue();
        decision.NeedAudioEncode.Should().BeTrue();
        decision.Reasons.Should().Contain("sync audio");
    }

    [Fact]
    public void Decide_WhenStreamsCompatible_ReturnsCopyPath()
    {
        IStreamCompatibilityPolicy sut = new DefaultStreamCompatibilityPolicy();

        var decision = sut.Decide(new StreamCompatibilityInput(
            IsMkvInput: true,
            HasAudioStream: true,
            IsVideoCopyCompatible: true,
            HasNonAacAudio: false,
            ForceSyncAudio: false,
            NeedVideoEncode: false));

        decision.IsCopyPath.Should().BeTrue();
        decision.NeedAudioEncode.Should().BeFalse();
        decision.NeedContainerChange.Should().BeFalse();
    }
}
