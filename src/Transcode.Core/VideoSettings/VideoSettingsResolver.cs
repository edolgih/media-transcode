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
	private static readonly string[] QualityOrder = ["high", "default", "low"];
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
		var settings = ApplyOverrides(baseSettings, effectiveSelection, request, profile, sourceHeightForDefaults, algorithmOverride);
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
		EffectiveVideoSettingsSelection effectiveSelection,
		VideoSettingsRequest? request,
		VideoSettingsProfile profile,
		int? sourceHeightForDefaults,
		string? algorithmOverride)
	{
		var cq = request?.Cq ?? defaults.Cq;
		var maxrate = request?.Maxrate;
		var hasManualCq = request?.Cq.HasValue == true;
		var hasManualMaxrate = request?.Maxrate.HasValue == true;
		var resolvedMaxrateMin = defaults.MaxrateMin;
		var resolvedMaxrateMax = defaults.MaxrateMax;

		if (hasManualCq)
		{
			var resolvedRateBounds = ResolveDirectionalRateBounds(
				profile,
				effectiveSelection,
				defaults,
				sourceHeightForDefaults,
				cq);
			resolvedMaxrateMin = resolvedRateBounds.MaxrateMin;
			resolvedMaxrateMax = resolvedRateBounds.MaxrateMax;

			if (!maxrate.HasValue)
			{
				maxrate = Clamp(resolvedRateBounds.Maxrate, resolvedMaxrateMin, resolvedMaxrateMax);
			}
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
			MaxrateMin: resolvedMaxrateMin,
			MaxrateMax: resolvedMaxrateMax);
	}

	private static DirectionalRateBounds ResolveDirectionalRateBounds(
		VideoSettingsProfile profile,
		EffectiveVideoSettingsSelection effectiveSelection,
		VideoSettingsDefaults defaults,
		int? sourceHeightForDefaults,
		int targetCq)
	{
		var delta = defaults.Cq - targetCq;
		if (delta == 0)
		{
			return new DirectionalRateBounds(defaults.Maxrate, defaults.MaxrateMin, defaults.MaxrateMax);
		}

		var rateModel = ResolveDirectionalRateModel(
			profile,
			effectiveSelection,
			defaults,
			sourceHeightForDefaults,
			delta > 0);
		var resolvedMaxrate = defaults.Maxrate + (delta * rateModel.MaxrateStep);
		var resolvedMaxrateMin = defaults.MaxrateMin + (delta * rateModel.MaxrateMinStep);
		var resolvedMaxrateMax = defaults.MaxrateMax + (delta * rateModel.MaxrateMaxStep);

		if (resolvedMaxrateMin > resolvedMaxrateMax)
		{
			(resolvedMaxrateMin, resolvedMaxrateMax) = (resolvedMaxrateMax, resolvedMaxrateMin);
		}

		return new DirectionalRateBounds(resolvedMaxrate, resolvedMaxrateMin, resolvedMaxrateMax);
	}

	private static DirectionalRateModel ResolveDirectionalRateModel(
		VideoSettingsProfile profile,
		EffectiveVideoSettingsSelection effectiveSelection,
		VideoSettingsDefaults defaults,
		int? sourceHeightForDefaults,
		bool towardsBetterQuality)
	{
		ArgumentNullException.ThrowIfNull(profile);
		ArgumentNullException.ThrowIfNull(effectiveSelection);
		ArgumentNullException.ThrowIfNull(defaults);

		var fallbackStep = profile.RateModel.CqStepToMaxrateStep;
		var fallbackModel = new DirectionalRateModel(
			MaxrateStep: fallbackStep,
			MaxrateMinStep: fallbackStep,
			MaxrateMaxStep: fallbackStep);

		if (!TryResolveNeighborQualityProfile(effectiveSelection.QualityProfile, towardsBetterQuality, out var neighborQualityProfile) ||
			!profile.TryResolveDefaults(sourceHeightForDefaults, effectiveSelection.ContentProfile, neighborQualityProfile, out var neighborDefaults))
		{
			return fallbackModel;
		}

		var cqDistance = Math.Abs(defaults.Cq - neighborDefaults.Cq);
		if (cqDistance <= 0)
		{
			return fallbackModel;
		}

		return new DirectionalRateModel(
			MaxrateStep: ResolveDirectionalStep(defaults.Maxrate, neighborDefaults.Maxrate, cqDistance),
			MaxrateMinStep: ResolveDirectionalStep(defaults.MaxrateMin, neighborDefaults.MaxrateMin, cqDistance),
			MaxrateMaxStep: ResolveDirectionalStep(defaults.MaxrateMax, neighborDefaults.MaxrateMax, cqDistance));
	}

	private static bool TryResolveNeighborQualityProfile(string qualityProfile, bool towardsBetterQuality, out string neighborQualityProfile)
	{
		neighborQualityProfile = string.Empty;

		var currentIndex = Array.IndexOf(QualityOrder, qualityProfile);
		if (currentIndex < 0)
		{
			return false;
		}

		var neighborIndex = towardsBetterQuality
			? currentIndex - 1
			: currentIndex + 1;
		if (neighborIndex < 0 || neighborIndex >= QualityOrder.Length)
		{
			neighborIndex = towardsBetterQuality
				? currentIndex + 1
				: currentIndex - 1;
		}

		if (neighborIndex < 0 || neighborIndex >= QualityOrder.Length)
		{
			return false;
		}

		neighborQualityProfile = QualityOrder[neighborIndex];
		return true;
	}

	private static decimal ResolveDirectionalStep(decimal currentValue, decimal neighborValue, int cqDistance)
	{
		var distance = Math.Abs(currentValue - neighborValue);
		return distance > 0m
			? distance / cqDistance
			: 0m;
	}

	private static decimal Clamp(decimal value, decimal min, decimal max)
	{
		if (value < min)
		{
			return min;
		}

		return value > max ? max : value;
	}

	private sealed record DirectionalRateBounds(decimal Maxrate, decimal MaxrateMin, decimal MaxrateMax);

	private sealed record DirectionalRateModel(decimal MaxrateStep, decimal MaxrateMinStep, decimal MaxrateMaxStep);
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
