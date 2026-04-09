using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля для целевой высоты 424.
Она задает таблицу defaults и bucket-правила для самого компактного поддерживаемого downscale-профиля.
*/
/// <summary>
/// Builds the configured video settings profile for target height 424.
/// </summary>
internal static class VideoSettings424Profile
{
    /*
    Это точка сборки профильной таблицы для 424.
    */
    /// <summary>
    /// Creates the configured profile for target height 424.
    /// </summary>
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 424,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "sd_480",
                    MinHeight: 425,
                    MaxHeight: 649),
                new SourceHeightBucket(
                    "hd_720",
                    MinHeight: 650,
                    MaxHeight: 899,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 20, MaxrateMax: 3.6m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 21, MaxrateMax: 3.2m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 23, MaxrateMax: 2.4m)
                    ]),
                new SourceHeightBucket(
                    "fhd_1080",
                    MinHeight: 900,
                    MaxHeight: 1300,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 38, MaxrateMin: 1.1m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 24, Maxrate: 2.1m, Bufsize: 4.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 26, MaxrateMin: 1.6m, MaxrateMax: 2.8m),
                new VideoSettingsDefaults("anime", "default", Cq: 25, Maxrate: 1.6m, Bufsize: 3.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 28, MaxrateMin: 1.3m, MaxrateMax: 2.0m),
                new VideoSettingsDefaults("anime", "low", Cq: 31, Maxrate: 1.4m, Bufsize: 2.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 26, CqMax: 36, MaxrateMin: 0.8m, MaxrateMax: 2.0m),
                new VideoSettingsDefaults("mult", "high", Cq: 24, Maxrate: 2.8m, Bufsize: 5.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 33, MaxrateMin: 1.8m, MaxrateMax: 3.6m),
                new VideoSettingsDefaults("mult", "default", Cq: 25, Maxrate: 2.5m, Bufsize: 5.0m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 35, MaxrateMin: 1.5m, MaxrateMax: 3.2m),
                new VideoSettingsDefaults("mult", "low", Cq: 30, Maxrate: 1.9m, Bufsize: 3.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 23, CqMax: 38, MaxrateMin: 1.1m, MaxrateMax: 2.4m),
                new VideoSettingsDefaults("film", "high", Cq: 23, Maxrate: 3.1m, Bufsize: 6.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 35, MaxrateMin: 1.4m, MaxrateMax: 5.0m),
                new VideoSettingsDefaults("film", "default", Cq: 24, Maxrate: 2.9m, Bufsize: 5.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 37, MaxrateMin: 1.1m, MaxrateMax: 5.0m),
                new VideoSettingsDefaults("film", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 40, MaxrateMin: 0.8m, MaxrateMax: 2.6m)
            ]);
    }
}
