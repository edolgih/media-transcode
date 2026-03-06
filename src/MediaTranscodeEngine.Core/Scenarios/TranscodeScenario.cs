using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Scenarios;

	public sealed class TranscodeScenario
{
    private static readonly IReadOnlySet<string> EmptyExplicitFieldSet = new HashSet<string>(StringComparer.Ordinal);
    private readonly Func<RawTranscodeRequest, IReadOnlySet<string>, RawTranscodeRequest>? _applyBehavior;

    public TranscodeScenario(
        string name,
        string? targetContainer = null,
        string? encoderBackend = null,
        string? videoPreset = null,
        string? targetVideoCodec = null,
        bool? overlayBg = null,
        int? downscale = null,
        string? downscaleAlgo = null,
        string? contentProfile = null,
        string? qualityProfile = null,
        bool? noAutoSample = null,
        string? autoSampleMode = null,
        bool? syncAudio = null,
        int? cq = null,
        double? maxrate = null,
        double? bufsize = null,
        bool? forceVideoEncode = null,
        bool? keepFps = null,
        bool? useAq = null,
        int? aqStrength = null,
        bool? denoise = null,
        bool? fixTimestamps = null,
        bool? keepSource = null,
        Func<RawTranscodeRequest, IReadOnlySet<string>, RawTranscodeRequest>? applyBehavior = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Scenario name is required.", nameof(name));
        }

        Name = name.Trim();
        TargetContainer = targetContainer;
        EncoderBackend = encoderBackend;
        VideoPreset = videoPreset;
        TargetVideoCodec = targetVideoCodec;
        OverlayBg = overlayBg;
        Downscale = downscale;
        DownscaleAlgo = downscaleAlgo;
        ContentProfile = contentProfile;
        QualityProfile = qualityProfile;
        NoAutoSample = noAutoSample;
        AutoSampleMode = autoSampleMode;
        SyncAudio = syncAudio;
        Cq = cq;
        Maxrate = maxrate;
        Bufsize = bufsize;
        ForceVideoEncode = forceVideoEncode;
        KeepFps = keepFps;
        UseAq = useAq;
        AqStrength = aqStrength;
        Denoise = denoise;
        FixTimestamps = fixTimestamps;
        KeepSource = keepSource;
        _applyBehavior = applyBehavior;
    }

    public string Name { get; }
    public string? TargetContainer { get; }
    public string? EncoderBackend { get; }
    public string? VideoPreset { get; }
    public string? TargetVideoCodec { get; }
    public bool? OverlayBg { get; }
    public int? Downscale { get; }
    public string? DownscaleAlgo { get; }
    public string? ContentProfile { get; }
    public string? QualityProfile { get; }
    public bool? NoAutoSample { get; }
    public string? AutoSampleMode { get; }
    public bool? SyncAudio { get; }
    public int? Cq { get; }
    public double? Maxrate { get; }
    public double? Bufsize { get; }
    public bool? ForceVideoEncode { get; }
    public bool? KeepFps { get; }
    public bool? UseAq { get; }
    public int? AqStrength { get; }
    public bool? Denoise { get; }
    public bool? FixTimestamps { get; }
    public bool? KeepSource { get; }

    public RawTranscodeRequest Apply(
        RawTranscodeRequest request,
        IReadOnlySet<string>? explicitTemplateFields = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var explicitFields = explicitTemplateFields ?? EmptyExplicitFieldSet;
        var merged = request with
        {
            TargetContainer = ResolveString(
                explicitValue: request.TargetContainer,
                defaultValue: RequestContracts.General.DefaultContainer,
                scenarioValue: TargetContainer,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.TargetContainer))),
            EncoderBackend = ResolveString(
                explicitValue: request.EncoderBackend,
                defaultValue: RequestContracts.General.DefaultEncoderBackend,
                scenarioValue: EncoderBackend,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.EncoderBackend))),
            VideoPreset = ResolveString(
                explicitValue: request.VideoPreset,
                defaultValue: RequestContracts.General.DefaultVideoPreset,
                scenarioValue: VideoPreset,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.VideoPreset))),
            TargetVideoCodec = ResolveString(
                explicitValue: request.TargetVideoCodec,
                defaultValue: RequestContracts.General.DefaultTargetVideoCodec,
                scenarioValue: TargetVideoCodec,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.TargetVideoCodec))),
            OverlayBg = ResolveBool(
                explicitValue: request.OverlayBg,
                defaultValue: false,
                scenarioValue: OverlayBg,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.OverlayBg))),
            Downscale = ResolveNullableInt(
                explicitValue: request.Downscale,
                scenarioValue: Downscale,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Downscale))),
            DownscaleAlgo = ResolveNullableString(
                explicitValue: request.DownscaleAlgo,
                scenarioValue: DownscaleAlgo,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.DownscaleAlgo))),
            ContentProfile = ResolveString(
                explicitValue: request.ContentProfile,
                defaultValue: RequestContracts.Transcode.DefaultContentProfile,
                scenarioValue: ContentProfile,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.ContentProfile))),
            QualityProfile = ResolveString(
                explicitValue: request.QualityProfile,
                defaultValue: RequestContracts.Transcode.DefaultQualityProfile,
                scenarioValue: QualityProfile,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.QualityProfile))),
            NoAutoSample = ResolveBool(
                explicitValue: request.NoAutoSample,
                defaultValue: false,
                scenarioValue: NoAutoSample,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.NoAutoSample))),
            AutoSampleMode = ResolveString(
                explicitValue: request.AutoSampleMode,
                defaultValue: RequestContracts.Transcode.DefaultAutoSampleMode,
                scenarioValue: AutoSampleMode,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.AutoSampleMode))),
            SyncAudio = ResolveBool(
                explicitValue: request.SyncAudio,
                defaultValue: false,
                scenarioValue: SyncAudio,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.SyncAudio))),
            Cq = ResolveNullableInt(
                explicitValue: request.Cq,
                scenarioValue: Cq,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Cq))),
            Maxrate = ResolveNullableDouble(
                explicitValue: request.Maxrate,
                scenarioValue: Maxrate,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Maxrate))),
            Bufsize = ResolveNullableDouble(
                explicitValue: request.Bufsize,
                scenarioValue: Bufsize,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Bufsize))),
            ForceVideoEncode = ResolveBool(
                explicitValue: request.ForceVideoEncode,
                defaultValue: false,
                scenarioValue: ForceVideoEncode,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.ForceVideoEncode))),
            KeepFps = ResolveBool(
                explicitValue: request.KeepFps,
                defaultValue: false,
                scenarioValue: KeepFps,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.KeepFps))),
            UseAq = ResolveBool(
                explicitValue: request.UseAq,
                defaultValue: false,
                scenarioValue: UseAq,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.UseAq))),
            AqStrength = ResolveInt(
                explicitValue: request.AqStrength,
                defaultValue: RequestContracts.General.DefaultAqStrength,
                scenarioValue: AqStrength,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.AqStrength))),
            Denoise = ResolveBool(
                explicitValue: request.Denoise,
                defaultValue: false,
                scenarioValue: Denoise,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.Denoise))),
            FixTimestamps = ResolveBool(
                explicitValue: request.FixTimestamps,
                defaultValue: false,
                scenarioValue: FixTimestamps,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.FixTimestamps))),
            KeepSource = ResolveBool(
                explicitValue: request.KeepSource,
                defaultValue: false,
                scenarioValue: KeepSource,
                isExplicit: IsExplicit(explicitFields, nameof(RawTranscodeRequest.KeepSource)))
        };

        return _applyBehavior is null
            ? merged
            : _applyBehavior(merged, explicitFields);
    }

    public static TranscodeScenario CreateToMkvGpu()
    {
        return new TranscodeScenario(
            name: "tomkvgpu",
            targetContainer: RequestContracts.General.MkvContainer,
            encoderBackend: RequestContracts.General.GpuEncoderBackend,
            videoPreset: RequestContracts.General.DefaultVideoPreset,
            targetVideoCodec: RequestContracts.General.CopyVideoCodec,
            overlayBg: false,
            contentProfile: RequestContracts.Transcode.DefaultContentProfile,
            qualityProfile: RequestContracts.Transcode.DefaultQualityProfile,
            noAutoSample: false,
            autoSampleMode: RequestContracts.Transcode.DefaultAutoSampleMode,
            syncAudio: false,
            forceVideoEncode: false,
            keepFps: false,
            useAq: false,
            aqStrength: RequestContracts.General.DefaultAqStrength,
            denoise: false,
            fixTimestamps: false,
            keepSource: false,
            applyBehavior: static (merged, explicitFields) =>
            {
                if (!IsExplicit(explicitFields, nameof(RawTranscodeRequest.TargetVideoCodec)) &&
                    merged.TargetContainer.Equals(RequestContracts.General.Mp4Container, StringComparison.OrdinalIgnoreCase) &&
                    merged.TargetVideoCodec.Equals(RequestContracts.General.CopyVideoCodec, StringComparison.OrdinalIgnoreCase))
                {
                    return merged with { TargetVideoCodec = RequestContracts.General.H264VideoCodec };
                }

                return merged;
            });
    }

    private static bool IsExplicit(IReadOnlySet<string> explicitFields, string fieldName)
    {
        return explicitFields.Contains(fieldName);
    }

    private static string ResolveString(
        string explicitValue,
        string defaultValue,
        string? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return string.Equals(explicitValue, defaultValue, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(scenarioValue)
            ? scenarioValue
            : explicitValue;
    }

    private static string? ResolveNullableString(
        string? explicitValue,
        string? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return string.IsNullOrWhiteSpace(explicitValue) && !string.IsNullOrWhiteSpace(scenarioValue)
            ? scenarioValue
            : explicitValue;
    }

    private static bool ResolveBool(
        bool explicitValue,
        bool defaultValue,
        bool? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue == defaultValue && scenarioValue.HasValue
            ? scenarioValue.Value
            : explicitValue;
    }

    private static int ResolveInt(
        int explicitValue,
        int defaultValue,
        int? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue == defaultValue && scenarioValue.HasValue
            ? scenarioValue.Value
            : explicitValue;
    }

    private static int? ResolveNullableInt(
        int? explicitValue,
        int? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue ?? scenarioValue;
    }

    private static double? ResolveNullableDouble(
        double? explicitValue,
        double? scenarioValue,
        bool isExplicit)
    {
        if (isExplicit)
        {
            return explicitValue;
        }

        return explicitValue ?? scenarioValue;
    }
}
