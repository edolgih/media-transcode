using Transcode.Core.Tools.Ffmpeg;

namespace Transcode.Core.VideoSettings.Profiles;

/*
Это фабрика профиля для целевой высоты 576.
Она задает таблицу defaults и локальные bucket-правила для SD-выхода.
*/
/// <summary>
/// Builds the configured video settings profile for target height 576.
/// </summary>
internal static class VideoSettings576Profile
{
	/*
	Это точка сборки профильной таблицы для 576.
	*/
	/// <summary>
	/// Creates the configured profile for target height 576.
	/// </summary>
	public static VideoSettingsProfile Create()
	{
		return new VideoSettingsProfile(
			targetHeight: 576,
			defaultContentProfile: "film",
			defaultQualityProfile: "default",
			rateModel: new VideoSettingsRateModel(CqStepToMaxrateStep: 0.4m, BufsizeMultiplier: 2.0m),
			sourceBuckets:
			[
				new SourceHeightBucket(
					"sd_576",
					MinHeight: 577,
					MaxHeight: 649),
				new SourceHeightBucket(
					"hd_720",
					MinHeight: 650,
					MaxHeight: 899,
					BoundsOverrides:
					[
						new VideoSettingsBoundsOverride("mult", "high", CqMin: 18, MaxrateMax: 5.0m),
						new VideoSettingsBoundsOverride("mult", "default", CqMin: 20, MaxrateMax: 4.2m),
						new VideoSettingsBoundsOverride("mult", "low", CqMin: 22, MaxrateMax: 3.2m)
					]),
				new SourceHeightBucket(
					"fhd_1080",
					MinHeight: 900,
					MaxHeight: 1300,
					BoundsOverrides:
					[
						new VideoSettingsBoundsOverride("mult", "low", CqMax: 36, MaxrateMin: 1.4m)
					])
			],
			defaults:
			[
				new VideoSettingsDefaults("anime", "high", Cq: 22, Maxrate: 3.3m, Bufsize: 6.5m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 19, CqMax: 24, MaxrateMin: 2.4m,
					MaxrateMax: 4.2m),
				new VideoSettingsDefaults("anime", "default", Cq: 23, Maxrate: 2.4m, Bufsize: 4.8m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 26, MaxrateMin: 2.0m,
					MaxrateMax: 3.0m),
				new VideoSettingsDefaults("anime", "low", Cq: 29, Maxrate: 2.1m, Bufsize: 4.1m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 24, CqMax: 35, MaxrateMin: 1.0m,
					MaxrateMax: 3.2m),
				new VideoSettingsDefaults("mult", "high", Cq: 22, Maxrate: 3.7m, Bufsize: 7.4m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 31, MaxrateMin: 2.3m,
					MaxrateMax: 5.0m),
				new VideoSettingsDefaults("mult", "default", Cq: 24, Maxrate: 3.3m, Bufsize: 6.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 33, MaxrateMin: 1.9m,
					MaxrateMax: 4.2m),
				new VideoSettingsDefaults("mult", "low", Cq: 28, Maxrate: 2.5m, Bufsize: 5.0m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 22, CqMax: 36, MaxrateMin: 1.4m,
					MaxrateMax: 3.2m),
				new VideoSettingsDefaults("film", "high", Cq: 21, Maxrate: 4.3m, Bufsize: 8.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 16, CqMax: 33, MaxrateMin: 2.0m,
					MaxrateMax: 8.0m),
				new VideoSettingsDefaults("film", "default", Cq: 23, Maxrate: 3.8m, Bufsize: 7.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 18, CqMax: 35, MaxrateMin: 1.6m,
					MaxrateMax: 8.0m),
				new VideoSettingsDefaults("film", "low", Cq: 27, Maxrate: 2.8m, Bufsize: 5.6m,
					Algorithm: FfmpegScaleAlgorithms.Bilinear, CqMin: 20, CqMax: 38, MaxrateMin: 1.2m, MaxrateMax: 4.0m)
			]);
	}
}