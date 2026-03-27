using Transcode.Core.Tools.Ffmpeg;
using Transcode.Core.VideoSettings;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля video settings для output bucket 720.
Она задаёт quality-oriented defaults и source-height bounds overrides.
*/
/// <summary>
/// Builds the typed profile for output height bucket 720.
/// </summary>
internal static class VideoSettings720Profile
{
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 720,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "hdplus_900",
                    MinHeight: 800,
                    MaxHeight: 999,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 17, MaxrateMax: 5.8m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 19, MaxrateMax: 5.0m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 22, MaxrateMax: 3.8m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 1000,
                    MaxHeight: 1300,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 17, MaxrateMax: 5.8m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 19, MaxrateMax: 5.0m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 22, MaxrateMax: 3.8m)
                    ]),
                new SourceHeightBucket(
                    "uhd_2160",
                    MinHeight: 1800,
                    MaxHeight: 2600,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 35, MaxrateMin: 1.8m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 22, Maxrate: 3.6m, Bufsize: 7.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 25, MaxrateMin: 2.4m, MaxrateMax: 4.2m),
                new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.8m, Bufsize: 5.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 27, MaxrateMin: 2.0m, MaxrateMax: 3.4m),
                new VideoSettingsDefaults("anime", "low", Cq: 29, Maxrate: 2.3m, Bufsize: 4.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 24, CqMax: 35, MaxrateMin: 1.2m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "high", Cq: 21, Maxrate: 4.6m, Bufsize: 9.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 30, MaxrateMin: 2.8m, MaxrateMax: 5.8m),
                new VideoSettingsDefaults("mult", "default", Cq: 23, Maxrate: 4.0m, Bufsize: 8.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 32, MaxrateMin: 2.3m, MaxrateMax: 5.0m),
                new VideoSettingsDefaults("mult", "low", Cq: 28, Maxrate: 3.0m, Bufsize: 6.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 35, MaxrateMin: 1.8m, MaxrateMax: 3.8m),
                new VideoSettingsDefaults("film", "high", Cq: 20, Maxrate: 5.4m, Bufsize: 10.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 16, CqMax: 32, MaxrateMin: 2.4m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "default", Cq: 22, Maxrate: 5.0m, Bufsize: 10.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 34, MaxrateMin: 2.0m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "low", Cq: 28, Maxrate: 3.0m, Bufsize: 6.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 37, MaxrateMin: 1.4m, MaxrateMax: 5.4m)
            ]);
    }
}
