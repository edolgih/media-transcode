using MediaTranscodeEngine.Runtime.VideoSettings;

namespace MediaTranscodeEngine.Runtime.Plans;

/*
Это общий план транскодирования, который сценарий передает инструменту.
В нем лежит общий intent без renderer-specific деталей; reusable VideoSettings и explicit Downscale разделены отдельно.
*/
/// <summary>
/// Describes a tool-agnostic transcode intent produced by a scenario for a specific source video.
/// </summary>
public sealed record TranscodePlan
{
    /// <summary>
    /// Initializes a tool-agnostic transcode plan.
    /// </summary>
    /// <param name="targetContainer">Target container identifier.</param>
    /// <param name="video">Normalized video intent: copy or encode.</param>
    /// <param name="audio">Normalized audio intent: copy or encode.</param>
    /// <param name="keepSource">Whether the source file should be preserved.</param>
    /// <param name="outputPath">Optional explicit output path.</param>
    /// <param name="applyOverlayBackground">Whether the plan requests background overlay during video encoding.</param>
    public TranscodePlan(
        string targetContainer,
        VideoPlan video,
        AudioPlan audio,
        bool keepSource,
        string? outputPath = null,
        bool applyOverlayBackground = false)
    {
        TargetContainer = NormalizeRequiredToken(targetContainer, nameof(targetContainer));
        Video = NormalizeVideoPlan(video);
        Audio = NormalizeAudioPlan(audio);
        KeepSource = keepSource;
        OutputPath = NormalizeOptionalPath(outputPath);
        ApplyOverlayBackground = applyOverlayBackground;
    }

    /// <summary>
    /// Gets the normalized target container identifier.
    /// </summary>
    public string TargetContainer { get; }

    /// <summary>
    /// Gets the normalized video intent.
    /// </summary>
    public VideoPlan Video { get; }

    /// <summary>
    /// Gets the encode-specific video intent when the plan requires video encoding.
    /// </summary>
    public EncodeVideoPlan? EncodeVideo => Video as EncodeVideoPlan;

    /// <summary>
    /// Gets the normalized audio intent.
    /// </summary>
    public AudioPlan Audio { get; }

    /// <summary>
    /// Gets the encode-specific audio intent when the plan requires audio encoding.
    /// </summary>
    public EncodeAudioPlan? EncodeAudio => Audio as EncodeAudioPlan;

    /// <summary>
    /// Gets the normalized target video codec identifier when re-encoding is required.
    /// </summary>
    public string? TargetVideoCodec => EncodeVideo?.TargetVideoCodec;

    /// <summary>
    /// Gets the normalized preferred backend identifier when a backend is relevant.
    /// </summary>
    public string? PreferredBackend => EncodeVideo?.PreferredBackend;

    /// <summary>
    /// Gets the normalized compatibility profile selected by the scenario when the target codec uses one.
    /// </summary>
    public VideoCompatibilityProfile? VideoCompatibilityProfile => EncodeVideo?.CompatibilityProfile;

    /// <summary>
    /// Gets the target output height in pixels when resizing is required.
    /// </summary>
    public int? TargetHeight => EncodeVideo?.Downscale?.TargetHeight;

    /// <summary>
    /// Gets the target frame rate when the scenario requests a frame rate change.
    /// </summary>
    public double? TargetFramesPerSecond => EncodeVideo?.TargetFramesPerSecond;

    /// <summary>
    /// Gets a value indicating whether frame interpolation is required.
    /// </summary>
    public bool UseFrameInterpolation => EncodeVideo?.UseFrameInterpolation ?? false;

    /// <summary>
    /// Gets reusable video-settings directives when the plan needs profile-driven output settings.
    /// </summary>
    public VideoSettingsRequest? VideoSettings => EncodeVideo?.VideoSettings;

    /// <summary>
    /// Gets explicit downscale intent when the plan requests a resized output.
    /// </summary>
    public DownscaleRequest? Downscale => EncodeVideo?.Downscale;

    /// <summary>
    /// Gets a value indicating whether the source video stream should be copied.
    /// </summary>
    public bool CopyVideo => Video is CopyVideoPlan;

    /// <summary>
    /// Gets a value indicating whether compatible audio streams should be copied.
    /// </summary>
    public bool CopyAudio => Audio is CopyAudioPlan;

    /// <summary>
    /// Gets a value indicating whether timestamp normalization is required.
    /// </summary>
    public bool FixTimestamps => Audio is RepairAudioPlan;

    /// <summary>
    /// Gets a value indicating whether the source file should be preserved.
    /// </summary>
    public bool KeepSource { get; }

    /// <summary>
    /// Gets the preferred encoder preset when the scenario wants to influence encode speed/quality tradeoffs.
    /// </summary>
    public string? EncoderPreset => EncodeVideo?.EncoderPreset;

    /// <summary>
    /// Gets an explicit output path when the scenario chooses one.
    /// </summary>
    public string? OutputPath { get; }

    /// <summary>
    /// Gets a value indicating whether background overlay should be applied during video encoding.
    /// </summary>
    public bool ApplyOverlayBackground { get; }

    /// <summary>
    /// Gets a value indicating whether the scenario requests the sync-safe audio path.
    /// </summary>
    public bool SynchronizeAudio => Audio is SynchronizeAudioPlan;

    /// <summary>
    /// Gets a value indicating whether the plan requires video encoding.
    /// </summary>
    public bool RequiresVideoEncode => !CopyVideo;

    /// <summary>
    /// Gets a value indicating whether the plan requires audio encoding.
    /// </summary>
    public bool RequiresAudioEncode => !CopyAudio;

    /// <summary>
    /// Gets a value indicating whether the plan changes the output height.
    /// </summary>
    public bool ChangesResolution => EncodeVideo?.Downscale is not null;

    /// <summary>
    /// Gets a value indicating whether the plan changes the frame rate.
    /// </summary>
    public bool ChangesFrameRate => EncodeVideo?.TargetFramesPerSecond is not null;

    private static VideoPlan NormalizeVideoPlan(VideoPlan video)
    {
        ArgumentNullException.ThrowIfNull(video);

        return video switch
        {
            CopyVideoPlan => video,
            EncodeVideoPlan encodeVideo => NormalizeEncodeVideoPlan(encodeVideo),
            _ => throw new ArgumentException($"Unsupported video plan type '{video.GetType().Name}'.", nameof(video))
        };
    }

    private static AudioPlan NormalizeAudioPlan(AudioPlan audio)
    {
        ArgumentNullException.ThrowIfNull(audio);

        return audio switch
        {
            CopyAudioPlan => audio,
            EncodeAudioPlan => audio,
            _ => throw new ArgumentException($"Unsupported audio plan type '{audio.GetType().Name}'.", nameof(audio))
        };
    }

    private static EncodeVideoPlan NormalizeEncodeVideoPlan(EncodeVideoPlan video)
    {
        ArgumentNullException.ThrowIfNull(video);

        var targetVideoCodec = NormalizeRequiredToken(video.TargetVideoCodec, nameof(video.TargetVideoCodec));
        var preferredBackend = NormalizeOptionalToken(video.PreferredBackend);
        var targetFramesPerSecond = NormalizeOptionalPositiveDouble(video.TargetFramesPerSecond, nameof(video.TargetFramesPerSecond));
        var encoderPreset = NormalizeOptionalToken(video.EncoderPreset);

        if (video.UseFrameInterpolation && !targetFramesPerSecond.HasValue)
        {
            throw new ArgumentException("Frame interpolation requires a target frame rate.", nameof(video.TargetFramesPerSecond));
        }

        if (targetVideoCodec.Equals("h264", StringComparison.OrdinalIgnoreCase) &&
            video.CompatibilityProfile is null)
        {
            throw new ArgumentException("H.264 encode plan requires compatibility profile.", nameof(video.CompatibilityProfile));
        }

        return new EncodeVideoPlan(
            TargetVideoCodec: targetVideoCodec,
            PreferredBackend: preferredBackend,
            CompatibilityProfile: video.CompatibilityProfile,
            TargetFramesPerSecond: targetFramesPerSecond,
            UseFrameInterpolation: video.UseFrameInterpolation,
            VideoSettings: video.VideoSettings,
            Downscale: video.Downscale,
            EncoderPreset: encoderPreset);
    }

    private static string NormalizeRequiredToken(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path.Trim());
    }

    private static double? NormalizeOptionalPositiveDouble(double? value, string paramName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value > 0
            ? value.Value
            : throw new ArgumentOutOfRangeException(paramName, value.Value, "Value must be greater than zero.");
    }

}
