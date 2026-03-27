using Transcode.Core.VideoSettings.Profiles;

namespace Transcode.Core.VideoSettings;

/*
Это общий resolver profile-driven video settings.
Он раздельно обслуживает ordinary encode и explicit downscale и использует единый каталог профилей и bounds-правила.
*/
/// <summary>
/// Resolves effective profile-driven video settings for encode and explicit downscale paths.
/// </summary>
internal sealed class VideoSettingsResolver
{
	private readonly VideoSettingsProfiles _profiles;

	/// <summary>
	/// Initializes a resolver backed by the supplied profile catalog.
	/// </summary>
	public VideoSettingsResolver(VideoSettingsProfiles profiles)
	{
		_profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
	}

	public ProfileDrivenVideoSettingsResolution ResolveForEncode(
		VideoSettingsRequest? request,
		int outputHeight,
		int? sourceHeight = null)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outputHeight);

		var profile = _profiles.ResolveOutputProfile(outputHeight);
		var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, request);
		return ResolveCore(
			profile,
			effectiveSelection,
			request,
			algorithmOverride: null,
			sourceHeightForDefaults: sourceHeight);
	}

	public ProfileDrivenVideoSettingsResolution ResolveForDownscale(
		DownscaleRequest request,
		VideoSettingsRequest? videoSettings,
		int sourceHeight)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);

		var profile = _profiles.GetRequiredProfile(request.TargetHeight);
		var effectiveSelection = BuildEffectiveVideoSettingsSelection(profile, videoSettings);
		return ResolveCore(
			profile,
			effectiveSelection,
			videoSettings,
			algorithmOverride: request.Algorithm,
			sourceHeightForDefaults: sourceHeight);
	}

	private ProfileDrivenVideoSettingsResolution ResolveCore(
		VideoSettingsProfile profile,
		EffectiveVideoSettingsSelection effectiveSelection,
		VideoSettingsRequest? request,
		string? algorithmOverride,
		int? sourceHeightForDefaults)
	{
		var baseSettings = profile.ResolveDefaults(sourceHeightForDefaults, effectiveSelection);
		var settings = ApplyOverrides(baseSettings, request, profile, algorithmOverride);
		return new ProfileDrivenVideoSettingsResolution(profile, effectiveSelection, baseSettings, settings);
	}

	private static EffectiveVideoSettingsSelection BuildEffectiveVideoSettingsSelection(
		VideoSettingsProfile profile,
		VideoSettingsRequest? request)
	{
		ArgumentNullException.ThrowIfNull(profile);

		return new EffectiveVideoSettingsSelection(
			ContentProfile: request?.ContentProfile ?? profile.DefaultContentProfile,
			QualityProfile: request?.QualityProfile ?? profile.DefaultQualityProfile);
	}

	private static VideoSettingsDefaults ApplyOverrides(
		VideoSettingsDefaults defaults,
		VideoSettingsRequest? request,
		VideoSettingsProfile profile,
		string? algorithmOverride)
	{
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
	EffectiveVideoSettingsSelection EffectiveSelection,
	VideoSettingsDefaults BaseSettings,
	VideoSettingsDefaults Settings);

/*
Это fully resolved selection для profile-driven video settings.
Он отделяет selection defaults от raw override-request и не допускает nullable в profile-layer.
*/
/// <summary>
/// Represents the resolved non-null profile selection used inside the video-settings pipeline.
/// </summary>
internal sealed record EffectiveVideoSettingsSelection(
	string ContentProfile,
	string QualityProfile)
{
	public string ContentProfile { get; init; } = NormalizeSupportedValue(
		ContentProfile,
		nameof(ContentProfile),
		VideoSettingsRequest.IsSupportedContentProfile,
		VideoSettingsRequest.SupportedContentProfiles);

	public string QualityProfile { get; init; } = NormalizeSupportedValue(
		QualityProfile,
		nameof(QualityProfile),
		VideoSettingsRequest.IsSupportedQualityProfile,
		VideoSettingsRequest.SupportedQualityProfiles);

	private static string NormalizeSupportedValue(
		string? value,
		string paramName,
		Func<string?, bool> isSupported,
		IReadOnlyList<string> supportedValues)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

		var normalized = value.Trim().ToLowerInvariant();
		if (!isSupported(normalized))
		{
			throw new ArgumentOutOfRangeException(paramName, value,
				$"Supported values: {string.Join(", ", supportedValues)}.");
		}

		return normalized;
	}
}