using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Policy;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class YamlProfileRepository : IProfileRepository
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly string _yamlPath;
    private TranscodePolicyConfig? _cachedConfig;

    public YamlProfileRepository()
        : this(Path.Combine(AppContext.BaseDirectory, "Profiles", "ToMkvGPU.576.Profiles.yaml"))
    {
    }

    public YamlProfileRepository(string yamlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yamlPath);
        _yamlPath = yamlPath;
    }

    public TranscodePolicyConfig Get576Config()
    {
        if (_cachedConfig is not null)
        {
            return _cachedConfig;
        }

        if (!File.Exists(_yamlPath))
        {
            throw new FileNotFoundException("Profile YAML file was not found.", _yamlPath);
        }

        var yamlText = File.ReadAllText(_yamlPath);

        YamlTranscodePolicyConfig yamlConfig;
        try
        {
            yamlConfig = YamlDeserializer.Deserialize<YamlTranscodePolicyConfig>(yamlText)
                ?? throw new InvalidOperationException("Profile YAML is empty.");
        }
        catch (YamlException exception)
        {
            throw new InvalidOperationException("Profile YAML parsing failed.", exception);
        }

        var config = MapConfig(yamlConfig);
        ProfileConfigValidator.Validate576Config(config);

        _cachedConfig = config;
        return _cachedConfig;
    }

    private static TranscodePolicyConfig MapConfig(YamlTranscodePolicyConfig source)
    {
        return new TranscodePolicyConfig(
            ContentProfiles: MapContentProfiles(source.ContentProfiles),
            RateModel: new RateModelSettings(
                source.RateModel?.CqStepToMaxrateStep ?? 0,
                source.RateModel?.BufsizeMultiplier ?? 0),
            QualityRanges: MapQualityRanges(source.QualityRanges),
            ContentQualityRanges: MapContentQualityRanges(source.ContentQualityRanges),
            SourceBuckets: MapSourceBuckets(source.SourceBuckets),
            AutoSampling: MapAutoSampling(source.AutoSampling));
    }

    private static IReadOnlyDictionary<string, ContentProfileSettings> MapContentProfiles(
        IDictionary<string, YamlContentProfileSettings>? source)
    {
        var result = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var content in source)
        {
            if (content.Value is null)
            {
                throw new InvalidOperationException($"Profile YAML is invalid: content profile '{content.Key}' is null.");
            }

            result[content.Key] = new ContentProfileSettings(
                AlgoDefault: content.Value.AlgoDefault ?? string.Empty,
                Defaults: MapProfileDefaults(content.Value.Defaults),
                Limits: MapProfileLimits(content.Value.Limits));
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ProfileDefaults> MapProfileDefaults(
        IDictionary<string, YamlProfileDefaults>? source)
    {
        var result = new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var defaults in source)
        {
            if (defaults.Value is null)
            {
                throw new InvalidOperationException($"Profile YAML is invalid: defaults for quality '{defaults.Key}' is null.");
            }

            result[defaults.Key] = new ProfileDefaults(
                Cq: defaults.Value.Cq,
                Maxrate: defaults.Value.Maxrate,
                Bufsize: defaults.Value.Bufsize);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ProfileLimits> MapProfileLimits(
        IDictionary<string, YamlProfileLimits>? source)
    {
        var result = new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return result;
        }

        foreach (var limits in source)
        {
            if (limits.Value is null)
            {
                throw new InvalidOperationException($"Profile YAML is invalid: limits for quality '{limits.Key}' is null.");
            }

            result[limits.Key] = new ProfileLimits(
                CqMin: limits.Value.CqMin,
                CqMax: limits.Value.CqMax,
                MaxrateMin: limits.Value.MaxrateMin,
                MaxrateMax: limits.Value.MaxrateMax);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, ReductionRange>? MapQualityRanges(
        IDictionary<string, YamlReductionRange>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase);
        foreach (var range in source)
        {
            if (range.Value is null)
            {
                throw new InvalidOperationException($"Profile YAML is invalid: quality range '{range.Key}' is null.");
            }

            result[range.Key] = MapReductionRange(range.Value);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ReductionRange>>? MapContentQualityRanges(
        IDictionary<string, Dictionary<string, YamlReductionRange>>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new Dictionary<string, IReadOnlyDictionary<string, ReductionRange>>(StringComparer.OrdinalIgnoreCase);
        foreach (var content in source)
        {
            if (content.Value is null)
            {
                throw new InvalidOperationException($"Profile YAML is invalid: content quality range '{content.Key}' is null.");
            }

            var qualityRanges = new Dictionary<string, ReductionRange>(StringComparer.OrdinalIgnoreCase);
            foreach (var quality in content.Value)
            {
                if (quality.Value is null)
                {
                    throw new InvalidOperationException($"Profile YAML is invalid: range '{content.Key}/{quality.Key}' is null.");
                }

                qualityRanges[quality.Key] = MapReductionRange(quality.Value);
            }

            result[content.Key] = qualityRanges;
        }

        return result;
    }

    private static IReadOnlyList<SourceBucketSettings>? MapSourceBuckets(IList<YamlSourceBucket>? source)
    {
        if (source is null)
        {
            return null;
        }

        var result = new List<SourceBucketSettings>(source.Count);
        foreach (var bucket in source)
        {
            if (bucket is null)
            {
                throw new InvalidOperationException("Profile YAML is invalid: source bucket item is null.");
            }

            result.Add(new SourceBucketSettings(
                Name: bucket.Name ?? string.Empty,
                Match: MapSourceBucketMatch(bucket.Match),
                IsDefault: bucket.IsDefault,
                ContentQualityRanges: MapContentQualityRanges(bucket.ContentQualityRanges),
                QualityRanges: MapQualityRanges(bucket.QualityRanges)));
        }

        return result;
    }

    private static SourceBucketMatch? MapSourceBucketMatch(YamlSourceBucketMatch? source)
    {
        if (source is null)
        {
            return null;
        }

        return new SourceBucketMatch(
            MinHeightInclusive: source.MinHeightInclusive,
            MinHeightExclusive: source.MinHeightExclusive,
            MaxHeightInclusive: source.MaxHeightInclusive,
            MaxHeightExclusive: source.MaxHeightExclusive);
    }

    private static AutoSamplingSettings? MapAutoSampling(YamlAutoSamplingSettings? source)
    {
        if (source is null)
        {
            return null;
        }

        return new AutoSamplingSettings(
            EnabledByDefault: source.EnabledByDefault ?? true,
            MaxIterations: source.MaxIterations ?? 8,
            ModeDefault: source.ModeDefault ?? "accurate",
            HybridAccurateIterations: source.HybridAccurateIterations ?? 2);
    }

    private static ReductionRange MapReductionRange(YamlReductionRange source)
    {
        return new ReductionRange(
            MinInclusive: source.MinInclusive,
            MinExclusive: source.MinExclusive,
            MaxInclusive: source.MaxInclusive,
            MaxExclusive: source.MaxExclusive);
    }

    private sealed class YamlTranscodePolicyConfig
    {
        public Dictionary<string, YamlContentProfileSettings>? ContentProfiles { get; set; }
        public YamlRateModelSettings? RateModel { get; set; }
        public Dictionary<string, YamlReductionRange>? QualityRanges { get; set; }
        public Dictionary<string, Dictionary<string, YamlReductionRange>>? ContentQualityRanges { get; set; }
        public List<YamlSourceBucket>? SourceBuckets { get; set; }
        public YamlAutoSamplingSettings? AutoSampling { get; set; }
    }

    private sealed class YamlRateModelSettings
    {
        public double CqStepToMaxrateStep { get; set; }
        public double BufsizeMultiplier { get; set; }
    }

    private sealed class YamlContentProfileSettings
    {
        public string? AlgoDefault { get; set; }
        public Dictionary<string, YamlProfileDefaults>? Defaults { get; set; }
        public Dictionary<string, YamlProfileLimits>? Limits { get; set; }
    }

    private sealed class YamlProfileDefaults
    {
        public int Cq { get; set; }
        public double Maxrate { get; set; }
        public double Bufsize { get; set; }
    }

    private sealed class YamlProfileLimits
    {
        public int CqMin { get; set; }
        public int CqMax { get; set; }
        public double MaxrateMin { get; set; }
        public double MaxrateMax { get; set; }
    }

    private sealed class YamlReductionRange
    {
        public double? MinInclusive { get; set; }
        public double? MinExclusive { get; set; }
        public double? MaxInclusive { get; set; }
        public double? MaxExclusive { get; set; }
    }

    private sealed class YamlSourceBucket
    {
        public string? Name { get; set; }
        public YamlSourceBucketMatch? Match { get; set; }
        public bool IsDefault { get; set; }
        public Dictionary<string, Dictionary<string, YamlReductionRange>>? ContentQualityRanges { get; set; }
        public Dictionary<string, YamlReductionRange>? QualityRanges { get; set; }
    }

    private sealed class YamlSourceBucketMatch
    {
        public double? MinHeightInclusive { get; set; }
        public double? MinHeightExclusive { get; set; }
        public double? MaxHeightInclusive { get; set; }
        public double? MaxHeightExclusive { get; set; }
    }

    private sealed class YamlAutoSamplingSettings
    {
        public bool? EnabledByDefault { get; set; }
        public int? MaxIterations { get; set; }
        public string? ModeDefault { get; set; }
        public int? HybridAccurateIterations { get; set; }
    }
}
