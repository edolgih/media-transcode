using System.Globalization;
using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Commanding;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Engine;

public sealed class H264TranscodeEngine
{
    private static readonly HashSet<string> AudioCopyCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "aac",
        "mp3"
    };

    private readonly IProbeReader _probeReader;
    private readonly H264RemuxEligibilityPolicy _remuxEligibilityPolicy;
    private readonly H264CommandBuilder _commandBuilder;

    public H264TranscodeEngine(
        IProbeReader probeReader,
        H264RemuxEligibilityPolicy remuxEligibilityPolicy,
        H264CommandBuilder commandBuilder)
    {
        _probeReader = probeReader;
        _remuxEligibilityPolicy = remuxEligibilityPolicy;
        _commandBuilder = commandBuilder;
    }

    public string Process(H264TranscodeRequest request)
    {
        return ProcessCore(request, probeOverride: null, useProbeOverride: false);
    }

    public string ProcessWithProbeResult(H264TranscodeRequest request, ProbeResult? probe)
    {
        return ProcessCore(request, probe, useProbeOverride: true);
    }

    public string ProcessWithProbeJson(H264TranscodeRequest request, string? probeJson)
    {
        var parsedProbe = ProbeJsonParser.Parse(probeJson);
        return ProcessCore(request, parsedProbe, useProbeOverride: true);
    }

    private string ProcessCore(
        H264TranscodeRequest request,
        ProbeResult? probeOverride,
        bool useProbeOverride)
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

        var useDownscale = request.Downscale.HasValue &&
                           video.Height.HasValue &&
                           video.Height.Value > request.Downscale.Value;

        var fixTimestamps = request.FixTimestamps ||
                            IsWmvOrAsfExtension(inputPath) ||
                            ContainsFormatToken(probe.Format?.FormatName, "asf");

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
        var (outputPath, tempOutputPath) = ResolveOutputPaths(
            inputPath: inputPath,
            outputMkv: request.OutputMkv,
            keepSource: request.KeepSource,
            useDownscale: useDownscale,
            downscaleTarget: request.Downscale,
            willEncode: !canRemux);
        if (canRemux)
        {
            return _commandBuilder.BuildRemux(new H264RemuxCommandInput(
                InputPath: inputPath,
                OutputPath: outputPath,
                TempOutputPath: tempOutputPath,
                OutputMkv: request.OutputMkv,
                ReplaceInput: !request.KeepSource));
        }

        var fpsToken = ResolveFpsToken(video, useDownscale, request.KeepFps);
        var fpsValue = ParseFpsToken(fpsToken) ?? 30.0;
        var gop = (int)Math.Max(12, Math.Round(fpsValue * 2.0));
        var copyAudio = audio is not null &&
                        !fixTimestamps &&
                        AudioCopyCodecs.Contains(audio.CodecName);

        return _commandBuilder.BuildEncode(new H264EncodeCommandInput(
            InputPath: inputPath,
            OutputPath: outputPath,
            TempOutputPath: tempOutputPath,
            NvencPreset: request.NvencPreset,
            Cq: request.Cq ?? 19,
            FpsToken: fpsToken,
            Gop: gop,
            OutputMkv: request.OutputMkv,
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

    private static (string OutputPath, string TempOutputPath) ResolveOutputPaths(
        string inputPath,
        bool outputMkv,
        bool keepSource,
        bool useDownscale,
        int? downscaleTarget,
        bool willEncode)
    {
        var outputExtension = outputMkv ? ".mkv" : ".mp4";
        if (keepSource)
        {
            var downscaleSuffix = useDownscale && downscaleTarget.HasValue
                ? $"{downscaleTarget.Value}p"
                : null;
            var codecSuffix = willEncode ? "h264" : null;
            var outputPath = OutputPathBuilder.BuildKeepSourceOutputPath(
                inputPath,
                outputExtension: outputExtension,
                downscaleSuffix,
                codecSuffix);
            return (outputPath, outputPath);
        }

        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        return (
            OutputPath: Path.Combine(directory, $"{baseName}{outputExtension}"),
            TempOutputPath: Path.Combine(directory, $"{baseName} (h264){outputExtension}"));
    }

    private static bool IsWmvOrAsfExtension(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);
        return extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".asf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsFormatToken(string? formatName, string expectedToken)
    {
        if (string.IsNullOrWhiteSpace(formatName))
        {
            return false;
        }

        var tokens = formatName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Any(token => token.Equals(expectedToken, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFpsToken(ProbeStream video, bool useDownscale, bool keepFps)
    {
        var sourceFpsToken = video.RFrameRate;
        if (string.IsNullOrWhiteSpace(sourceFpsToken) || sourceFpsToken == "0/0")
        {
            sourceFpsToken = video.AvgFrameRate;
        }

        if (string.IsNullOrWhiteSpace(sourceFpsToken) || sourceFpsToken == "0/0")
        {
            sourceFpsToken = "30/1";
        }

        var sourceFps = ParseFpsToken(sourceFpsToken) ?? 30.0;
        if (useDownscale && !keepFps && sourceFps > 30.0)
        {
            return "30000/1001";
        }

        return sourceFpsToken;
    }

    private static double? ParseFpsToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator > 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var fps)
            ? fps
            : null;
    }
}
