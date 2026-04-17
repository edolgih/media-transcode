using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля для целевой высоты 480.
Она описывает defaults и bucket-ограничения для более компактного SD-выхода.
*/
/// <summary>
/// Builds the configured video settings profile for target height 480.
/// </summary>
internal static class VideoSettings480Profile
{
	/*
	Это точка сборки профильной таблицы для 480.
	*/
	/// <summary>
	/// Creates the configured profile for target height 480.
	/// </summary>
	public static VideoSettingsProfile Create()
	{
		return new VideoSettingsProfile(
			targetHeight: 480,
			defaultContentProfile: "film",
			defaultQualityProfile: "default",
			rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
			sourceBuckets:
			[
				new SourceHeightBucket(
					"sd_576",
					MinHeight: 481,
					MaxHeight: 649),
				new SourceHeightBucket(
					"hd_720",
					MinHeight: 650,
					MaxHeight: 899,
					BoundsOverrides:
					[
						new VideoSettingsBoundsOverride("mult", "high", CqMin: 19, MaxrateMax: 4.2m),
						new VideoSettingsBoundsOverride("mult", "default", CqMin: 20, MaxrateMax: 3.6m),
						new VideoSettingsBoundsOverride("mult", "low", CqMin: 22, MaxrateMax: 2.8m)
					]),
				new SourceHeightBucket(
					"fhd_1080",
					MinHeight: 900,
					MaxHeight: 1300,
					BoundsOverrides:
					[
						new VideoSettingsBoundsOverride("mult", "low", CqMax: 37, MaxrateMin: 1.3m)
					])
			],
			defaults:
			[
				new VideoSettingsDefaults("anime", "high", Cq: 23, Maxrate: 2.5m, Bufsize: 5.0m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 25, MaxrateMin: 1.8m,
					MaxrateMax: 3.2m),
				new VideoSettingsDefaults("anime", "default", Cq: 24, Maxrate: 1.8m, Bufsize: 3.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 27, MaxrateMin: 1.5m,
					MaxrateMax: 2.3m),
				new VideoSettingsDefaults("anime", "low", Cq: 30, Maxrate: 1.6m, Bufsize: 3.2m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 25, CqMax: 36, MaxrateMin: 0.8m,
					MaxrateMax: 2.4m),
				new VideoSettingsDefaults("mult", "high", Cq: 23, Maxrate: 3.2m, Bufsize: 6.4m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 32, MaxrateMin: 2.0m,
					MaxrateMax: 4.2m),
				new VideoSettingsDefaults("mult", "default", Cq: 24, Maxrate: 2.8m, Bufsize: 5.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 34, MaxrateMin: 1.7m,
					MaxrateMax: 3.6m),
				new VideoSettingsDefaults("mult", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.2m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 37, MaxrateMin: 1.3m,
					MaxrateMax: 2.8m),
				new VideoSettingsDefaults("film", "high", Cq: 22, Maxrate: 3.4m, Bufsize: 6.8m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 17, CqMax: 34, MaxrateMin: 1.5m,
					MaxrateMax: 6.0m),
				new VideoSettingsDefaults("film", "default", Cq: 23, Maxrate: 3.1m, Bufsize: 6.2m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 36, MaxrateMin: 1.2m,
					MaxrateMax: 6.0m),
				new VideoSettingsDefaults("film", "low", Cq: 28, Maxrate: 2.2m, Bufsize: 4.4m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 21, CqMax: 39, MaxrateMin: 0.9m, MaxrateMax: 3.0m)
			]);
	}
}