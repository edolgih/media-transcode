using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/// <summary>
/// Resolves profile-driven video settings defaults without invoking autosample logic.
/// </summary>
public static class VideoSettingsDefaultsResolver
{
    /// <summary>
    /// Resolves encode defaults from the shared video-settings catalog for the supplied output height.
    /// </summary>
    public static ResolvedVideoSettingsDefaults ResolveEncodeDefaults(
        int outputHeight,
        int? sourceHeight = null,
        VideoSettingsRequest? request = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

        var profile = VideoSettingsProfiles.Default.ResolveOutputProfile(outputHeight);
        var selection = new EffectiveVideoSettingsSelection(
            ContentProfile: request?.ContentProfile ?? profile.DefaultContentProfile,
            QualityProfile: request?.QualityProfile ?? profile.DefaultQualityProfile,
            AutoSampleMode: request?.AutoSampleMode ?? profile.AutoSampling.ModeDefault);
        var defaults = profile.ResolveDefaults(sourceHeight, selection);
        var settings = ApplyManualOverrides(defaults, request, profile);

        return new ResolvedVideoSettingsDefaults(
            ContentProfile: selection.ContentProfile,
            QualityProfile: selection.QualityProfile,
            Cq: settings.Cq,
            Maxrate: settings.Maxrate,
            Bufsize: settings.Bufsize);
    }

    private static VideoSettingsDefaults ApplyManualOverrides(
        VideoSettingsDefaults defaults,
        VideoSettingsRequest? request,
        VideoSettingsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(profile);

        var cq = request?.Cq ?? defaults.Cq;
        var maxrate = request?.Maxrate;
        var hasManualCq = request?.Cq.HasValue == true;
        var hasManualMaxrate = request?.Maxrate.HasValue == true;

        if (!maxrate.HasValue && hasManualCq)
        {
            var delta = defaults.Cq - cq;
            var resolved = defaults.Maxrate + (delta * profile.RateModel.CqStepToMaxrateStep);
            maxrate = Clamp(resolved, defaults.MaxrateMin, defaults.MaxrateMax);
        }

        maxrate ??= defaults.Maxrate;

        var bufsize = request?.Bufsize;
        if (!bufsize.HasValue && (hasManualMaxrate || hasManualCq))
        {
            bufsize = maxrate.Value * profile.RateModel.BufsizeMultiplier;
        }

        bufsize ??= defaults.Bufsize;

        return defaults with
        {
            Cq = cq,
            Maxrate = maxrate.Value,
            Bufsize = bufsize.Value
        };
    }

    private static decimal Clamp(decimal value, decimal min, decimal max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max
            ? max
            : value;
    }
}
