using FluentAssertions;
using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Tests.VideoSettings;

public sealed class VideoSettingsRequestTests
{
    [Fact]
    public void SupportedProfiles_AreDerivedFromRuntimeProfileCatalog()
    {
        VideoSettingsRequest.SupportedContentProfiles.Should().Equal("anime", "mult", "film");
        VideoSettingsRequest.SupportedQualityProfiles.Should().Equal("high", "default", "low");
    }

    [Fact]
    public void SupportedAutoSampleModes_ExposeCanonicalRuntimeValues()
    {
        VideoSettingsRequest.SupportedAutoSampleModes.Should().Equal("accurate", "fast", "hybrid");
        DownscaleRequest.SupportedAlgorithms.Should().Equal("bilinear", "bicubic", "lanczos");
    }
}
