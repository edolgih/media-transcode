using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Classification;
using MediaTranscodeEngine.Core.Compatibility;
using MediaTranscodeEngine.Core.Quality;
using MediaTranscodeEngine.Core.Resolutions;
using MediaTranscodeEngine.Core.Sampling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaTranscodeEngine.Core.Execution;

public sealed class CopyCodecExecutionStrategy : ICodecExecutionStrategy
{
    private static readonly HashSet<string> CopyVideoCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "h264",
        "mpeg4"
    };

    private readonly IProbeReader _probeReader;
    private readonly FfmpegCommandBuilder _ffmpegCommandBuilder;
    private readonly IInputClassifier _inputClassifier;
    private readonly IResolutionPolicyRepository _resolutionPolicyRepository;
    private readonly IQualityStrategy _qualityStrategy;
    private readonly IAutoSamplingStrategy _autoSamplingStrategy;
    private readonly IStreamCompatibilityPolicy _streamCompatibilityPolicy;
    private readonly IAutoSampleReductionProvider? _autoSampleReductionProvider;
    private readonly string _encodedOutputCodecSuffix;
    private readonly ILogger<CopyCodecExecutionStrategy> _logger;

    public CopyCodecExecutionStrategy(
        IProbeReader probeReader,
        FfmpegCommandBuilder ffmpegCommandBuilder,
        IInputClassifier inputClassifier,
        IResolutionPolicyRepository resolutionPolicyRepository,
        IQualityStrategy qualityStrategy,
        IAutoSamplingStrategy autoSamplingStrategy,
        IStreamCompatibilityPolicy streamCompatibilityPolicy,
        string encodedOutputCodecSuffix = RequestContracts.General.H264VideoCodec,
        IAutoSampleReductionProvider? autoSampleReductionProvider = null,
        ILogger<CopyCodecExecutionStrategy>? logger = null)
    {
        _probeReader = probeReader;
        _ffmpegCommandBuilder = ffmpegCommandBuilder;
        _inputClassifier = inputClassifier;
        _resolutionPolicyRepository = resolutionPolicyRepository;
        _qualityStrategy = qualityStrategy;
        _autoSamplingStrategy = autoSamplingStrategy;
        _streamCompatibilityPolicy = streamCompatibilityPolicy;
        _encodedOutputCodecSuffix = encodedOutputCodecSuffix;
        _autoSampleReductionProvider = autoSampleReductionProvider;
        _logger = logger ?? NullLogger<CopyCodecExecutionStrategy>.Instance;
    }

    public string Key => CodecExecutionKeys.Copy;

    public string Process(TranscodeRequest request, ProbeResult? probeOverride, bool useProbeOverride)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileName = request.InputPath;
        var displayName = Path.GetFileName(fileName);
        var extension = Path.GetExtension(fileName);
        var isMkvInput = extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase);

        var probe = useProbeOverride
            ? probeOverride
            : _probeReader.Read(fileName);
        if (probe is null || probe.Streams.Count == 0)
        {
            _logger.LogWarning("Probe failed or returned no streams for {InputPath}", fileName);
            return request.Info
                ? $"{displayName}: [ffprobe failed]"
                : $"REM ffprobe failed: {fileName}";
        }

        var video = probe.Streams.FirstOrDefault(static stream =>
            stream.CodecType.Equals("video", StringComparison.OrdinalIgnoreCase));
        if (video is null)
        {
            return request.Info
                ? $"{displayName}: [no video stream]"
                : $"REM Нет видеопотока: {fileName}";
        }

        var sourceFps = ProbeRateParser.ResolveSourceFps(video);
        var classification = _inputClassifier.Classify(video.Height, sourceFps);
        var baseQuality = _qualityStrategy.Resolve(new QualitySelectionContext(
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
            return request.Info
                ? $"{displayName}: [{hint}]"
                : $"REM {hint}";
        }

        var audioStreams = probe.Streams
            .Where(static stream => stream.CodecType.Equals("audio", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var applyDownscale = resolutionPolicy.ApplyDownscale;
        var qualitySettings = applyDownscale
            ? (resolutionPolicy.Settings ?? baseQuality)
            : new QualitySettings(
                Cq: 21,
                Maxrate: 3.0,
                Bufsize: 6.0,
                DownscaleAlgo: request.DownscaleAlgo);
        var shouldAutoSample = applyDownscale &&
                               _autoSampleReductionProvider is not null &&
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
                        new AutoSampleReductionInput(fileName, cq, maxrate, bufsize)),
                FastReductionProvider: (cq, maxrate, bufsize) =>
                    _autoSampleReductionProvider!.EstimateFast(
                        new AutoSampleReductionInput(fileName, cq, maxrate, bufsize))));
        }

        var videoCodec = video.CodecName.ToLowerInvariant();
        var isVideoCopyCompatible = CopyVideoCodecs.Contains(videoCodec);
        var needVideoEncode = !isVideoCopyCompatible || request.OverlayBg || applyDownscale || request.ForceVideoEncode;
        var compatibility = _streamCompatibilityPolicy.Decide(new StreamCompatibilityInput(
            IsMkvInput: isMkvInput,
            HasAudioStream: audioStreams.Length > 0,
            IsVideoCopyCompatible: isVideoCopyCompatible,
            HasNonAacAudio: audioStreams.Any(static stream =>
                !stream.CodecName.Equals("aac", StringComparison.OrdinalIgnoreCase)),
            ForceSyncAudio: request.SyncAudio,
            NeedVideoEncode: needVideoEncode));

        if (request.Info)
        {
            var parts = new List<string>();
            if (compatibility.NeedContainerChange)
            {
                parts.Add($"container {extension}→mkv");
            }

            if (!isVideoCopyCompatible)
            {
                parts.Add($"vcodec {video.CodecName}");
            }

            if (request.ForceVideoEncode)
            {
                parts.Add("force video encode");
            }

            if (compatibility.Reasons.Contains("audio non-aac", StringComparer.OrdinalIgnoreCase))
            {
                parts.Add("audio non-AAC");
            }

            if (compatibility.ForceSyncAudio && audioStreams.Length > 0)
            {
                parts.Add("sync audio");
            }

            return parts.Count == 0
                ? string.Empty
                : $"{displayName}: [{string.Join("] [", parts)}]";
        }

        if (compatibility.IsCopyPath)
        {
            return string.Empty;
        }

        var downscaleTarget = request.Downscale ?? 0;
        var (outputPath, postOperation) = ResolveCopyOutputPathAndPostOperation(
            inputPath: fileName,
            isMkvInput: isMkvInput,
            keepSource: request.KeepSource,
            applyDownscale: applyDownscale,
            downscaleTarget: downscaleTarget,
            needVideoEncode: needVideoEncode,
            encodedOutputCodecSuffix: _encodedOutputCodecSuffix);

        var commandInput = new FfmpegCommandInput(
            InputPath: fileName,
            OutputPath: outputPath,
            PostOperation: postOperation,
            NeedVideoEncode: needVideoEncode,
            NeedAudioEncode: compatibility.NeedAudioEncode,
            NeedContainer: compatibility.NeedContainerChange,
            ForceSyncAudio: compatibility.ForceSyncAudio,
            ApplyDownscale: applyDownscale,
            DownscaleTarget: downscaleTarget,
            OverlayBg: request.OverlayBg,
            SourceWidth: video.Width,
            SourceHeight: video.Height,
            Cq: request.Cq ?? qualitySettings.Cq,
            Maxrate: qualitySettings.Maxrate,
            Bufsize: qualitySettings.Bufsize,
            DownscaleAlgo: qualitySettings.DownscaleAlgo,
            SourceFps: sourceFps,
            NvencPreset: request.VideoPreset);

        return _ffmpegCommandBuilder.Build(commandInput);
    }

    private static (string OutputPath, string PostOperation) ResolveCopyOutputPathAndPostOperation(
        string inputPath,
        bool isMkvInput,
        bool keepSource,
        bool applyDownscale,
        int downscaleTarget,
        bool needVideoEncode,
        string encodedOutputCodecSuffix)
    {
        if (keepSource)
        {
            var downscaleSuffix = applyDownscale && downscaleTarget > 0
                ? $"{downscaleTarget}p"
                : null;
            var codecSuffix = needVideoEncode ? encodedOutputCodecSuffix : null;
            var outputPath = OutputPathBuilder.BuildKeepSourceOutputPath(
                inputPath,
                outputExtension: ".mkv",
                downscaleSuffix,
                codecSuffix);
            return (outputPath, string.Empty);
        }

        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var finalOutputPath = Path.Combine(directory, $"{baseName}.mkv");
        if (!isMkvInput)
        {
            return (finalOutputPath, $"&& del \"{inputPath}\"");
        }

        var tempOutputPath = Path.Combine(directory, $"{baseName}_temp.mkv");
        var postOperation = $"&& del \"{inputPath}\" && ren \"{tempOutputPath}\" \"{baseName}.mkv\"";
        return (tempOutputPath, postOperation);
    }
}
