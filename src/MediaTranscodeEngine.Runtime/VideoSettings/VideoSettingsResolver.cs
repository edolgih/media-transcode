using MediaTranscodeEngine.Runtime.VideoSettings.Profiles;

namespace MediaTranscodeEngine.Runtime.VideoSettings;

/*
Это общий resolver profile-driven video settings.
Он раздельно обслуживает ordinary encode и explicit downscale, но использует один и тот же каталог профилей, bounds и autosample-логику.
*/
/// <summary>
/// Resolves effective profile-driven video settings for encode and explicit downscale paths.
/// </summary>
internal sealed class VideoSettingsResolver
{
    private readonly VideoSettingsProfiles _profiles;
    private readonly VideoSettingsAutoSampler _autoSampler;

    /// <summary>
    /// Initializes a resolver backed by the supplied profile catalog.
    /// </summary>
    public VideoSettingsResolver(VideoSettingsProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _autoSampler = new VideoSettingsAutoSampler(_profiles);
    }

    public ProfileDrivenVideoSettingsResolution ResolveForEncode(
        VideoSettingsRequest? request,
        int outputHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        string? defaultAutoSampleMode = "fast",
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        var profile = _profiles.ResolveOutputProfile(outputHeight);
        var effectiveRequest = BuildEffectiveVideoSettingsRequest(request, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveRequest,
            algorithmOverride: null,
            sourceHeightForRanges: null,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    public ProfileDrivenVideoSettingsResolution ResolveForDownscale(
        DownscaleRequest request,
        VideoSettingsRequest? videoSettings,
        int sourceHeight,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        string? defaultAutoSampleMode = "hybrid",
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

        var profile = _profiles.GetRequiredProfile(request.TargetHeight);
        var effectiveRequest = BuildEffectiveVideoSettingsRequest(videoSettings, defaultAutoSampleMode);
        return ResolveCore(
            profile,
            effectiveRequest,
            algorithmOverride: request.Algorithm,
            sourceHeightForRanges: sourceHeight,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);
    }

    private ProfileDrivenVideoSettingsResolution ResolveCore(
        VideoSettingsProfile profile,
        VideoSettingsRequest effectiveRequest,
        string? algorithmOverride,
        int? sourceHeightForRanges,
        TimeSpan duration,
        long? sourceBitrate,
        bool hasAudio,
        Func<VideoSettingsDefaults, IReadOnlyList<VideoSettingsSampleWindow>, decimal?>? accurateReductionProvider)
    {
        var baseSettings = profile.ResolveDefaults(
            sourceHeight: sourceHeightForRanges,
            contentProfile: effectiveRequest.ContentProfile,
            qualityProfile: effectiveRequest.QualityProfile);

        var autoSampleResolution = _autoSampler.ResolveWithDiagnostics(
            profile,
            effectiveRequest,
            baseSettings,
            sourceHeightForRanges,
            duration,
            sourceBitrate,
            hasAudio,
            accurateReductionProvider);

        var settings = ApplyOverrides(autoSampleResolution.Settings, effectiveRequest, profile, algorithmOverride);
        return new ProfileDrivenVideoSettingsResolution(profile, effectiveRequest, baseSettings, autoSampleResolution, settings);
    }

    private static VideoSettingsRequest BuildEffectiveVideoSettingsRequest(
        VideoSettingsRequest? request,
        string? defaultAutoSampleMode)
    {
        return VideoSettingsRequest.CreateOrNull(
            contentProfile: request?.ContentProfile,
            qualityProfile: request?.QualityProfile,
            autoSampleMode: request?.AutoSampleMode ?? defaultAutoSampleMode,
            cq: request?.Cq,
            maxrate: request?.Maxrate,
            bufsize: request?.Bufsize)
            ?? throw new InvalidOperationException("Effective video settings selection must resolve to at least one value.");
    }

    private static VideoSettingsDefaults ApplyOverrides(
        VideoSettingsDefaults defaults,
        VideoSettingsRequest request,
        VideoSettingsProfile profile,
        string? algorithmOverride)
    {
        var cq = request.Cq ?? defaults.Cq;
        var maxrate = request.Maxrate;

        if (!maxrate.HasValue && request.Cq.HasValue)
        {
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request.Bufsize;
        if (!bufsize.HasValue && (request.Maxrate.HasValue || request.Cq.HasValue))
        {
            bufsize = maxrate.Value * profile.RateModel.BufsizeMultiplier;
        }

        bufsize ??= defaults.Bufsize;

        return new VideoSettingsDefaults(
            ContentProfile: defaults.ContentProfile,
            QualityProfile: defaults.QualityProfile,
            Cq: cq,
            Maxrate: maxrate.Value,
            Bufsize: bufsize.Value,
            Algorithm: algorithmOverride ?? defaults.Algorithm,
            CqMin: defaults.CqMin,
            CqMax: defaults.CqMax,
            MaxrateMin: defaults.MaxrateMin,
            MaxrateMax: defaults.MaxrateMax);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}

/*
Это диагностический результат разрешения video settings.
Он нужен тестам и логированию, но не вводит отдельную доменную модель.
*/
/// <summary>
/// Describes the full resolution result for profile-driven video settings.
/// </summary>
internal sealed record ProfileDrivenVideoSettingsResolution(
    VideoSettingsProfile Profile,
    VideoSettingsRequest EffectiveRequest,
    VideoSettingsDefaults BaseSettings,
    VideoSettingsAutoSampleResolution AutoSample,
    VideoSettingsDefaults Settings);
