using Transcode.Core.VideoSettings;

namespace Transcode.Core.MediaIntent;

/*
Это модель, которая представляет a normalized video intent for a scenario decision
*/
/// <summary>
/// Represents a normalized video intent for a scenario decision.
/// </summary>
public abstract record VideoIntent;

/*
Это модель, которая представляет a video-copy intent with no encode-specific settings
*/
/// <summary>
/// Represents a video-copy intent with no encode-specific settings.
/// </summary>
public sealed record CopyVideoIntent : VideoIntent;

/*
Это модель, которая представляет an explicit video-encode intent and its encode-specific settings
*/
/// <summary>
/// Represents an explicit video-encode intent and its encode-specific settings.
/// </summary>
public sealed record EncodeVideoIntent(
    string TargetVideoCodec,
    string? PreferredBackend = null,
    H264OutputProfile? CompatibilityProfile = null,
    double? TargetFramesPerSecond = null,
    bool UseFrameInterpolation = false,
    VideoSettingsRequest? VideoSettings = null,
    DownscaleRequest? Downscale = null,
    string? EncoderPreset = null) : VideoIntent;
