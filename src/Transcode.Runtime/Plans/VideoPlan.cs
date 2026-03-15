using Transcode.Runtime.VideoSettings;

namespace Transcode.Runtime.Plans;

/// <summary>
/// Represents a normalized video intent for a transcode plan.
/// </summary>
public abstract record VideoPlan;

/// <summary>
/// Represents a video-copy intent with no encode-specific settings.
/// </summary>
public sealed record CopyVideoPlan : VideoPlan;

/// <summary>
/// Represents an explicit video-encode intent and its encode-specific settings.
/// </summary>
public sealed record EncodeVideoPlan(
    string TargetVideoCodec,
    string? PreferredBackend = null,
    VideoCompatibilityProfile? CompatibilityProfile = null,
    double? TargetFramesPerSecond = null,
    bool UseFrameInterpolation = false,
    VideoSettingsRequest? VideoSettings = null,
    DownscaleRequest? Downscale = null,
    string? EncoderPreset = null) : VideoPlan;
