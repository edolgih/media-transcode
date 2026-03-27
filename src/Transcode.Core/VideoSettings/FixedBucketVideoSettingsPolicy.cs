using Transcode.Core.MediaIntent;

namespace Transcode.Core.VideoSettings;

/*
Эта политика фиксирует quality-first поведение для контент-профилей,
которые не должны получать source-adaptive подстройку.
*/
internal static class FixedBucketVideoSettingsPolicy
{
    private const string AnimeContentProfile = "anime";
    private const string FilmContentProfile = "film";
    private const string MultContentProfile = "mult";
    private const decimal SourceMaxrateMultiplier = 1.0m;
    private const decimal MinimumPositiveMaxrateMbps = 0.001m;

    public static bool ShouldUseFixedBucketQuality(
        VideoSettingsProfiles profiles,
        bool useDownscale,
        DownscaleRequest? downscaleRequest,
        int videoHeight,
        VideoSettingsRequest? request)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (!string.IsNullOrWhiteSpace(request?.ContentProfile))
        {
            return IsFixedQualityContentProfile(request.ContentProfile);
        }

        var profile = useDownscale
            ? profiles.GetRequiredProfile(
                downscaleRequest?.TargetHeight ?? throw new InvalidOperationException("Downscale request is required when downscale mode is active."))
            : profiles.ResolveOutputProfile(Math.Max(1, videoHeight));
        return IsFixedQualityContentProfile(profile.DefaultContentProfile);
    }

    public static VideoSettingsDefaults ApplySourceBitrateCap(
        VideoSettingsDefaults settings,
        long? sourceVideoBitrate,
        VideoSettingsRequest? request,
        decimal bufsizeMultiplier)
    {
        if (HasManualRateOverrides(request) ||
            !sourceVideoBitrate.HasValue ||
            sourceVideoBitrate.Value <= 0)
        {
            return settings;
        }

        var sourceBitrateMaxrate = (sourceVideoBitrate.Value / 1_000_000m) * SourceMaxrateMultiplier;
        if (sourceBitrateMaxrate <= 0m)
        {
            return settings;
        }

        var cappedMaxrate = Math.Min(settings.Maxrate, sourceBitrateMaxrate);
        if (cappedMaxrate >= settings.Maxrate)
        {
            return settings;
        }

        cappedMaxrate = Math.Max(
            MinimumPositiveMaxrateMbps,
            decimal.Round(cappedMaxrate, 3, MidpointRounding.AwayFromZero));
        var cappedBufsize = Math.Max(
            MinimumPositiveMaxrateMbps,
            decimal.Round(cappedMaxrate * bufsizeMultiplier, 3, MidpointRounding.AwayFromZero));

        return settings with
        {
            Maxrate = cappedMaxrate,
            Bufsize = cappedBufsize
        };
    }

    private static bool IsFixedQualityContentProfile(string contentProfile)
    {
        return contentProfile.Equals(AnimeContentProfile, StringComparison.OrdinalIgnoreCase) ||
               contentProfile.Equals(FilmContentProfile, StringComparison.OrdinalIgnoreCase) ||
               contentProfile.Equals(MultContentProfile, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasManualRateOverrides(VideoSettingsRequest? request)
    {
        return request?.Cq.HasValue == true ||
               request?.Maxrate.HasValue == true ||
               request?.Bufsize.HasValue == true;
    }
}
