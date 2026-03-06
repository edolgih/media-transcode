using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;
using MediaTranscodeEngine.Core.Classification;
using MediaTranscodeEngine.Core.Compatibility;
using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Resolutions;
using MediaTranscodeEngine.Core.Sampling;

namespace MediaTranscodeEngine.Core.Scenarios.ToMkvGpu;

public sealed class H264GpuCodecExecutionStrategy : ICodecExecutionStrategy
{
    private static readonly HashSet<string> CopyVideoCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private readonly IProbeReader _probeReader;
    private readonly H264CommandBuilder _h264CommandBuilder;
    private readonly H264RemuxEligibilityPolicy _remuxEligibilityPolicy;
    private readonly H264TimestampPolicy _timestampPolicy;
    private readonly H264AudioPolicy _audioPolicy;
    private readonly H264RateControlPolicy _rateControlPolicy;
    private readonly ContainerPolicySelector _containerPolicySelector;
    private readonly IInputClassifier _inputClassifier;
    private readonly IResolutionPolicyRepository _resolutionPolicyRepository;
    private readonly IQualityStrategy _qualityStrategy;
    private readonly IAutoSamplingStrategy _autoSamplingStrategy;
    private readonly IStreamCompatibilityPolicy _streamCompatibilityPolicy;
    private readonly IAutoSampleReductionProvider? _autoSampleReductionProvider;

    public H264GpuCodecExecutionStrategy(
        IProbeReader probeReader,
        H264CommandBuilder h264CommandBuilder,
        H264RemuxEligibilityPolicy remuxEligibilityPolicy,
        H264TimestampPolicy timestampPolicy,
        H264AudioPolicy audioPolicy,
        H264RateControlPolicy rateControlPolicy,
        ContainerPolicySelector containerPolicySelector,
        IInputClassifier inputClassifier,
        IResolutionPolicyRepository resolutionPolicyRepository,
        IQualityStrategy qualityStrategy,
        IAutoSamplingStrategy autoSamplingStrategy,
        IStreamCompatibilityPolicy streamCompatibilityPolicy,
        IAutoSampleReductionProvider? autoSampleReductionProvider = null)
    {
        _probeReader = probeReader;
        _h264CommandBuilder = h264CommandBuilder;
        _remuxEligibilityPolicy = remuxEligibilityPolicy;
        _timestampPolicy = timestampPolicy;
        _audioPolicy = audioPolicy;
        _rateControlPolicy = rateControlPolicy;
        _containerPolicySelector = containerPolicySelector;
        _inputClassifier = inputClassifier;
        _resolutionPolicyRepository = resolutionPolicyRepository;
        _qualityStrategy = qualityStrategy;
        _autoSamplingStrategy = autoSamplingStrategy;
        _streamCompatibilityPolicy = streamCompatibilityPolicy;
        _autoSampleReductionProvider = autoSampleReductionProvider;
    }

    public string Key => CodecExecutionKeys.H264Gpu;

    public string Process(TranscodeRequest request, ProbeResult? probeOverride, bool useProbeOverride)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inputPath = request.InputPath;
        var probe = useProbeOverride
            ? probeOverride
            : _probeReader.Read(inputPath);
        if (probe is null || probe.Streams.Count == 0)
        {
            return $"REM ffprobe failed: {inputPath}";
        }

        var video = probe.Streams.FirstOrDefault(static stream =>
            stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase));
        if (video is null)
        {
            return $"REM Нет видеопотока: {inputPath}";
        }

        var audio = probe.Streams.FirstOrDefault(static stream =>
            stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase));

        var sourceFps = ProbeRateParser.ResolveSourceFps(video);
        var classification = _inputClassifier.Classify(video.Height, sourceFps);
        var qualitySettings = _qualityStrategy.Resolve(new QualitySelectionContext(
            ContentProfile: request.ContentProfile,
            QualityProfile: request.QualityProfile,
            Cq: request.Cq,
            Maxrate: request.Maxrate,
            Bufsize: request.Bufsize,
            DownscaleAlgo: request.DownscaleAlgoOverridden ? request.DownscaleAlgo : null));
        var resolutionPolicy = _resolutionPolicyRepository.Resolve(new ResolutionPolicyRequest(
            Transform: new ResolutionTransform(
                SourceHeight: classification.SourceHeight,
                TargetHeight: request.Downscale),
            ContentProfile: request.ContentProfile,
            QualityProfile: request.QualityProfile,
            Cq: request.Cq,
            Maxrate: request.Maxrate,
            Bufsize: request.Bufsize,
            DownscaleAlgo: request.DownscaleAlgoOverridden ? request.DownscaleAlgo : null));

        if (!resolutionPolicy.IsSupported)
        {
            var hint = string.IsNullOrWhiteSpace(resolutionPolicy.Error)
                ? "downscale is not supported."
                : resolutionPolicy.Error;
            return $"REM {hint}";
        }

        var useDownscale = request.Downscale.HasValue &&
                           video.Height.HasValue &&
                           video.Height.Value > request.Downscale.Value;
        var shouldAutoSample = _autoSampleReductionProvider is not null &&
                               useDownscale &&
                               !request.NoAutoSample &&
                               !request.Cq.HasValue &&
                               !request.Maxrate.HasValue &&
                               !request.Bufsize.HasValue &&
                               probe.Format?.DurationSeconds is > 0;
        if (shouldAutoSample)
        {
            var mode = string.IsNullOrWhiteSpace(request.AutoSampleMode)
                ? AutoSamplingMode.Accurate
                : request.AutoSampleMode;
            qualitySettings = _autoSamplingStrategy.Resolve(new AutoSamplingContext(
                ContentProfile: request.ContentProfile,
                QualityProfile: request.QualityProfile,
                BaseSettings: qualitySettings,
                SourceHeight: classification.SourceHeight,
                Mode: mode,
                AccurateReductionProvider: (cq, maxrate, bufsize) =>
                    _autoSampleReductionProvider!.EstimateAccurate(
                        new AutoSampleReductionInput(inputPath, cq, maxrate, bufsize)),
                FastReductionProvider: (cq, maxrate, bufsize) =>
                    _autoSampleReductionProvider!.EstimateFast(
                        new AutoSampleReductionInput(inputPath, cq, maxrate, bufsize))));
        }

        var compatibility = _streamCompatibilityPolicy.Decide(new StreamCompatibilityInput(
            IsMkvInput: Path.GetExtension(inputPath).Equals(".mkv", StringComparison.OrdinalIgnoreCase),
            HasAudioStream: audio is not null,
            IsVideoCopyCompatible: CopyVideoCodecs.Contains(video.CodecName),
            HasNonAacAudio: audio is not null && !audio.CodecName.Equals("aac", StringComparison.OrdinalIgnoreCase),
            ForceSyncAudio: request.SyncAudio,
            NeedVideoEncode: true));
        _ = (resolutionPolicy, qualitySettings, compatibility);

        var fixTimestamps = _timestampPolicy.ShouldFixTimestamps(new H264TimestampInput(
            InputPath: inputPath,
            FormatName: probe.Format?.FormatName,
            ForceFixTimestamps: request.FixTimestamps));
        var remuxEligibilityInput = new H264RemuxEligibilityInput(
            InputExtension: Path.GetExtension(inputPath),
            FormatName: probe.Format?.FormatName,
            VideoCodec: video.CodecName,
            AudioCodec: audio?.CodecName,
            RFrameRate: video.RFrameRate,
            AvgFrameRate: video.AvgFrameRate,
            Denoise: request.Denoise,
            FixTimestamps: fixTimestamps,
            UseDownscale: useDownscale);
        var canRemux = _remuxEligibilityPolicy.CanRemux(remuxEligibilityInput);

        var containerPolicy = _containerPolicySelector.Select(request.TargetContainer);
        var outputPaths = containerPolicy.ResolveOutputPaths(
            inputPath: inputPath,
            keepSource: request.KeepSource,
            useDownscale: useDownscale,
            downscaleTarget: request.Downscale,
            willEncode: !canRemux);

        if (canRemux)
        {
            return _h264CommandBuilder.BuildRemux(new H264RemuxCommandInput(
                InputPath: inputPath,
                OutputPath: outputPaths.OutputPath,
                TempOutputPath: outputPaths.TempOutputPath,
                ContainerPolicy: containerPolicy,
                ReplaceInput: !request.KeepSource));
        }

        var rateControl = _rateControlPolicy.Resolve(new H264RateControlInput(
            Video: video,
            UseDownscale: useDownscale,
            KeepFps: request.KeepFps,
            CqOverride: request.Cq));
        var copyAudio = _audioPolicy.CanCopyAudio(new H264AudioInput(
            AudioCodec: audio?.CodecName,
            FixTimestamps: fixTimestamps));

        return _h264CommandBuilder.BuildEncode(new H264EncodeCommandInput(
            InputPath: inputPath,
            OutputPath: outputPaths.OutputPath,
            TempOutputPath: outputPaths.TempOutputPath,
            NvencPreset: request.VideoPreset,
            Cq: rateControl.Cq,
            FpsToken: rateControl.FpsToken,
            Gop: rateControl.Gop,
            ContainerPolicy: containerPolicy,
            ApplyDownscale: useDownscale,
            DownscaleTarget: request.Downscale ?? 0,
            DownscaleAlgo: request.DownscaleAlgo,
            UseAq: request.UseAq,
            AqStrength: request.AqStrength,
            Denoise: request.Denoise,
            FixTimestamps: fixTimestamps,
            CopyAudio: copyAudio,
            ReplaceInput: !request.KeepSource));
    }
}
