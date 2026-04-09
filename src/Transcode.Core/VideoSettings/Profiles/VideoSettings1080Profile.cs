using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля для целевой высоты 1080.
Она описывает все defaults и bucket-ограничения для Full HD без явного downscale в эту высоту.
*/
/// <summary>
/// Builds the configured video settings profile for target height 1080.
/// </summary>
internal static class VideoSettings1080Profile
{
    /*
    Это точка сборки профильной таблицы для 1080.
    На выходе получается готовый объект, который можно включить в общий набор профилей.
    */
    /// <summary>
    /// Creates the configured profile for target height 1080.
    /// </summary>
    public static VideoSettingsProfile Create()
    {
        return new VideoSettingsProfile(
            targetHeight: 1080,
            defaultContentProfile: "film",
            defaultQualityProfile: "default",
            rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
            sourceBuckets:
            [
                new SourceHeightBucket(
                    "native_encode",
                    MinHeight: 1,
                    MaxHeight: 1,
                    IsDefault: true),
                new SourceHeightBucket(
                    "qhd_1440",
                    MinHeight: 1301,
                    MaxHeight: 1799,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "high", CqMin: 15, MaxrateMax: 6.4m),
                        new VideoSettingsBoundsOverride("mult", "default", CqMin: 17, MaxrateMax: 5.6m),
                        new VideoSettingsBoundsOverride("mult", "low", CqMin: 20, MaxrateMax: 4.2m)
                    ]),
                new SourceHeightBucket(
                    "uhd_2160",
                    MinHeight: 1800,
                    MaxHeight: 2600,
                    BoundsOverrides:
                    [
                        new VideoSettingsBoundsOverride("mult", "low", CqMax: 34, MaxrateMin: 2.0m)
                    ])
            ],
            defaults:
            [
                new VideoSettingsDefaults("anime", "high", Cq: 20, Maxrate: 4.2m, Bufsize: 8.4m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 24, MaxrateMin: 2.8m, MaxrateMax: 5.0m),
                new VideoSettingsDefaults("anime", "default", Cq: 21, Maxrate: 3.4m, Bufsize: 6.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 26, MaxrateMin: 2.4m, MaxrateMax: 4.0m),
                new VideoSettingsDefaults("anime", "low", Cq: 27, Maxrate: 2.6m, Bufsize: 5.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 34, MaxrateMin: 1.4m, MaxrateMax: 3.6m),
                new VideoSettingsDefaults("mult", "high", Cq: 19, Maxrate: 5.2m, Bufsize: 10.4m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 15, CqMax: 28, MaxrateMin: 3.0m, MaxrateMax: 6.4m),
                new VideoSettingsDefaults("mult", "default", Cq: 21, Maxrate: 4.6m, Bufsize: 9.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 30, MaxrateMin: 2.6m, MaxrateMax: 5.6m),
                new VideoSettingsDefaults("mult", "low", Cq: 26, Maxrate: 3.4m, Bufsize: 6.8m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 34, MaxrateMin: 2.0m, MaxrateMax: 4.2m),
                new VideoSettingsDefaults("film", "high", Cq: 18, Maxrate: 6.2m, Bufsize: 12.4m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 14, CqMax: 31, MaxrateMin: 2.8m, MaxrateMax: 8.8m),
                new VideoSettingsDefaults("film", "default", Cq: 20, Maxrate: 5.8m, Bufsize: 11.6m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 16, CqMax: 33, MaxrateMin: 2.4m, MaxrateMax: 8.0m),
                new VideoSettingsDefaults("film", "low", Cq: 26, Maxrate: 3.6m, Bufsize: 7.2m, Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 36, MaxrateMin: 1.8m, MaxrateMax: 5.4m)
            ],
            supportsDownscale: false);
    }
}
