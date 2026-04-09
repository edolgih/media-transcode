namespace Transcode.Core.VideoSettings.Profiles;

/*
Это профиль видеонастроек для одной целевой высоты.
Он хранит базовые defaults, модель пересчета bitrate и правила, которые зависят от высоты источника.
*/
/// <summary>
/// Represents video settings configuration for one target output height.
/// </summary>
sealed class VideoSettingsProfile
{
	private readonly
		IReadOnlyDictionary<(VideoContentProfile ContentProfile, VideoQualityProfile QualityProfile),
			VideoSettingsDefaults> _defaultsByProfile;

	/*
	Это конструктор профильной таблицы для конкретной высоты.
	Он связывает defaults, source bucket'ы и коэффициенты пересчета в один объект, которым уже может пользоваться резолвер.
	*/
	/// <summary>
	/// Initializes a target-height video settings profile.
	/// </summary>
	public VideoSettingsProfile(
		int targetHeight,
		string defaultContentProfile,
		string defaultQualityProfile,
		VideoSettingsRateModel rateModel,
		IReadOnlyList<SourceHeightBucket> sourceBuckets,
		IReadOnlyList<VideoSettingsDefaults> defaults,
		bool supportsDownscale = true)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
		ArgumentException.ThrowIfNullOrWhiteSpace(defaultContentProfile);
		ArgumentException.ThrowIfNullOrWhiteSpace(defaultQualityProfile);
		ArgumentNullException.ThrowIfNull(rateModel);
		ArgumentNullException.ThrowIfNull(sourceBuckets);
		ArgumentNullException.ThrowIfNull(defaults);

		TargetHeight = targetHeight;
		SupportsDownscale = supportsDownscale;
		DefaultContentProfile = VideoContentProfile.Parse(defaultContentProfile, nameof(defaultContentProfile));
		DefaultQualityProfile = VideoQualityProfile.Parse(defaultQualityProfile, nameof(defaultQualityProfile));
		RateModel = rateModel;
		SourceBuckets = sourceBuckets;
		Defaults = defaults;
		_defaultsByProfile = defaults.ToDictionary(static entry => (entry.ContentProfile, entry.QualityProfile));
	}

	/*
	Это целевая высота, для которой предназначен профиль.
	*/
	/// <summary>
	/// Gets the target output height described by this profile.
	/// </summary>
	public int TargetHeight { get; }

	/*
	Это признак, можно ли использовать профиль как явную цель downscale.
	*/
	/// <summary>
	/// Gets a value indicating whether explicit downscale is allowed for this profile.
	/// </summary>
	public bool SupportsDownscale { get; }

	/*
	Это профиль контента, который используется, если пользователь не выбрал свой.
	*/
	/// <summary>
	/// Gets the default content profile for this target height.
	/// </summary>
	public VideoContentProfile DefaultContentProfile { get; }

	/*
	Это профиль качества по умолчанию для этой целевой высоты.
	*/
	/// <summary>
	/// Gets the default quality profile for this target height.
	/// </summary>
	public VideoQualityProfile DefaultQualityProfile { get; }

	/*
	Это модель пересчета bitrate-полей при изменении CQ.
	*/
	/// <summary>
	/// Gets the rate model used to recalculate bitrate-related values.
	/// </summary>
	public VideoSettingsRateModel RateModel { get; }

	/*
	Это набор диапазонов высоты источника для локальных ограничений.
	*/
	/// <summary>
	/// Gets the source-height buckets configured for this profile.
	/// </summary>
	public IReadOnlyList<SourceHeightBucket> SourceBuckets { get; }

	/*
	Это таблица базовых значений для разных сочетаний content и quality profile.
	*/
	/// <summary>
	/// Gets the default rows available for this target height.
	/// </summary>
	public IReadOnlyList<VideoSettingsDefaults> Defaults { get; }

	/*
	Это имя bucket'а источника, который подходит под переданную высоту.
	Оно полезно для логов, диагностики и тестов.
	*/
	/// <summary>
	/// Resolves the name of the source-height bucket that matches the supplied height.
	/// </summary>
	public string? ResolveSourceBucket(int? sourceHeight)
	{
		return ResolveSourceBucketDefinition(sourceHeight)?.Name;
	}

	/*
	Это диагностическое сообщение, если профиль не может подобрать bucket для высоты источника.
	*/
	/// <summary>
	/// Returns a diagnostic message when no source-height bucket can be resolved.
	/// </summary>
	public string? ResolveSourceBucketIssue(int? sourceHeight)
	{
		if (!sourceHeight.HasValue)
		{
			var fallbackBucket = ResolveSourceBucketDefinition(sourceHeight);
			if (fallbackBucket is null)
			{
				return $"{TargetHeight} source bucket missing: height is unknown; add SourceBuckets";
			}

			return null;
		}

		var bucket = ResolveSourceBucketDefinition(sourceHeight);
		if (bucket is null)
		{
			return $"{TargetHeight} source bucket missing: height {sourceHeight.Value}; add SourceBuckets";
		}

		return null;
	}

	/*
	Это получение базовых значений без уточнения высоты источника.
	Тогда профиль использует default bucket, если он настроен.
	*/
	/// <summary>
	/// Resolves default settings without an explicit source height.
	/// </summary>
	public ResolvedVideoSettings ResolveDefaults(EffectiveVideoSettingsSelection selection)
	{
		return ResolveDefaults(sourceHeight: null, selection);
	}

	/*
	Это получение базовых значений с учетом высоты исходного видео.
	При необходимости поверх defaults будут наложены локальные bucket-ограничения.
	*/
	/// <summary>
	/// Resolves default settings while taking the source height into account.
	/// </summary>
	public ResolvedVideoSettings ResolveDefaults(int? sourceHeight, EffectiveVideoSettingsSelection selection)
	{
		ArgumentNullException.ThrowIfNull(selection);

		if (_defaultsByProfile.TryGetValue((selection.ContentProfile, selection.QualityProfile), out var defaults))
		{
			return ApplyBoundsOverride(sourceHeight, defaults, selection.ContentProfile, selection.QualityProfile);
		}

		throw new InvalidOperationException(
			$"Video settings defaults are not configured for content '{selection.ContentProfile}' and quality '{selection.QualityProfile}'.");
	}

	/*
	Это безопасный поиск строки defaults по строковым именам профилей.
	*/
	/// <summary>
	/// Tries to get the defaults row for the specified string profile pair.
	/// </summary>
	public bool TryGetDefaults(string contentProfile, string qualityProfile, out VideoSettingsDefaults defaults)
	{
		return TryGetDefaults(
			VideoContentProfile.Parse(contentProfile, nameof(contentProfile)),
			VideoQualityProfile.Parse(qualityProfile, nameof(qualityProfile)),
			out defaults);
	}

	/*
	Это безопасный поиск строки defaults по типизированной паре профилей.
	*/
	/// <summary>
	/// Tries to get the defaults row for the specified typed profile pair.
	/// </summary>
	public bool TryGetDefaults(VideoContentProfile contentProfile, VideoQualityProfile qualityProfile,
		out VideoSettingsDefaults defaults)
	{
		return _defaultsByProfile.TryGetValue((contentProfile, qualityProfile), out defaults!);
	}

	/*
	Это попытка сразу получить итоговые defaults с учетом bucket-ограничений.
	*/
	/// <summary>
	/// Tries to resolve final default settings for the specified string profile pair.
	/// </summary>
	public bool TryResolveDefaults(int? sourceHeight, string contentProfile, string qualityProfile,
		out ResolvedVideoSettings defaults)
	{
		return TryResolveDefaults(
			sourceHeight,
			VideoContentProfile.Parse(contentProfile, nameof(contentProfile)),
			VideoQualityProfile.Parse(qualityProfile, nameof(qualityProfile)),
			out defaults);
	}

	/*
	Это попытка получить итоговые defaults по типизированной паре профилей.
	Если подходящей строки нет, метод возвращает false вместо исключения.
	*/
	/// <summary>
	/// Tries to resolve final default settings for the specified typed profile pair.
	/// </summary>
	public bool TryResolveDefaults(
		int? sourceHeight,
		VideoContentProfile contentProfile,
		VideoQualityProfile qualityProfile,
		out ResolvedVideoSettings defaults)
	{
		if (!_defaultsByProfile.TryGetValue((contentProfile, qualityProfile), out var baseDefaults))
		{
			defaults = default!;
			return false;
		}

		defaults = ApplyBoundsOverride(sourceHeight, baseDefaults, contentProfile, qualityProfile);
		return true;
	}

	/*
	Это выбор bucket'а по высоте источника.
	Если точного совпадения нет, используется bucket, помеченный как default.
	*/
	/// <summary>
	/// Resolves the source-height bucket definition for the supplied height.
	/// </summary>
	private SourceHeightBucket? ResolveSourceBucketDefinition(int? sourceHeight)
	{
		if (sourceHeight.HasValue)
		{
			var matched = SourceBuckets.FirstOrDefault(bucket => bucket.Matches(sourceHeight.Value));
			if (matched is not null)
			{
				return matched;
			}
		}

		return SourceBuckets.FirstOrDefault(static bucket => bucket.IsDefault);
	}

	/*
	Это применение локального bucket-override поверх базовой строки defaults.
	*/
	/// <summary>
	/// Applies a bucket-specific bounds override to the selected defaults row.
	/// </summary>
	private ResolvedVideoSettings ApplyBoundsOverride(
		int? sourceHeight,
		VideoSettingsDefaults defaults,
		VideoContentProfile contentProfile,
		VideoQualityProfile qualityProfile)
	{
		var boundsOverride = ResolveSourceBucketDefinition(sourceHeight)
			?.ResolveBoundsOverride(contentProfile, qualityProfile);
		return ResolvedVideoSettings
			.FromDefaults(defaults)
			.ApplyBoundsOverride(boundsOverride);
	}
}