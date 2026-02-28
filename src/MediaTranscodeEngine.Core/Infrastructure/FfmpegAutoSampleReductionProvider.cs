using System.Globalization;
using MediaTranscodeEngine.Core.Abstractions;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class FfmpegAutoSampleReductionProvider : IAutoSampleReductionProvider
{
    private const double Megabit = 1_000_000d;

    private readonly IProbeReader _probeReader;
    private readonly IProcessRunner _processRunner;
    private readonly string _ffmpegPath;
    private readonly int _timeoutMs;
    private readonly int _sampleEncodeInactivityTimeoutMs;
    private readonly int _sampleDurationSeconds;
    private readonly string _nvencPreset;
    private readonly int _sampleEncodeMaxRetries;

    public FfmpegAutoSampleReductionProvider(
        IProbeReader probeReader,
        IProcessRunner processRunner,
        string ffmpegPath = "ffmpeg",
        int timeoutMs = 30_000,
        int sampleEncodeInactivityTimeoutMs = 12_000,
        int sampleDurationSeconds = 15,
        string nvencPreset = "p6",
        int sampleEncodeMaxRetries = 0)
    {
        ArgumentNullException.ThrowIfNull(probeReader);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(nvencPreset);
        if (timeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be greater than zero.");
        }

        if (sampleDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleDurationSeconds), "Sample duration must be greater than zero.");
        }

        if (sampleEncodeInactivityTimeoutMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleEncodeInactivityTimeoutMs), "Inactivity timeout must be greater than zero.");
        }

        if (sampleEncodeMaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleEncodeMaxRetries), "Retry count cannot be negative.");
        }

        _probeReader = probeReader;
        _processRunner = processRunner;
        _ffmpegPath = ffmpegPath;
        _timeoutMs = timeoutMs;
        _sampleEncodeInactivityTimeoutMs = sampleEncodeInactivityTimeoutMs;
        _sampleDurationSeconds = sampleDurationSeconds;
        _nvencPreset = nvencPreset;
        _sampleEncodeMaxRetries = sampleEncodeMaxRetries;
    }

    public double? EstimateAccurate(AutoSampleReductionInput input)
    {
        if (!TryResolveDurationAndSourceSize(input.InputPath, out var durationSeconds, out var sourceBytes))
        {
            return null;
        }

        var windows = ResolveSampleWindows(durationSeconds);
        if (windows.Count == 0)
        {
            return null;
        }

        var sampleWorkDir = CreateSampleWorkDirectory(input.InputPath);
        try
        {
            long encodedBytesTotal = 0;
            double sourceWindowBytesTotal = 0;
            for (var index = 0; index < windows.Count; index++)
            {
                var window = windows[index];
                var outputPath = Path.Combine(
                    sampleWorkDir,
                    $"sample-{index + 1}-{Guid.NewGuid():N}.mkv");

                try
                {
                    if (!TryRunSampleEncode(input, window.StartSeconds, window.DurationSeconds, outputPath))
                    {
                        return null;
                    }

                    var encodedBytes = new FileInfo(outputPath).Length;
                    encodedBytesTotal += encodedBytes;
                    sourceWindowBytesTotal += sourceBytes * (window.DurationSeconds / durationSeconds);
                }
                finally
                {
                    TryDelete(outputPath);
                }
            }

            if (sourceWindowBytesTotal <= 0)
            {
                return null;
            }

            var reduction = (1d - encodedBytesTotal / sourceWindowBytesTotal) * 100d;
            return Math.Round(reduction, 3, MidpointRounding.AwayFromZero);
        }
        finally
        {
            TryDeleteDirectory(sampleWorkDir);
        }
    }

    private bool TryRunSampleEncode(
        AutoSampleReductionInput input,
        double startSeconds,
        double durationSeconds,
        string outputPath)
    {
        var arguments = BuildSampleEncodeArguments(
            input.InputPath,
            outputPath,
            input,
            startSeconds,
            durationSeconds);

        var attempts = _sampleEncodeMaxRetries + 1;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var run = _processRunner.RunWithInactivityTimeout(
                _ffmpegPath,
                arguments,
                _timeoutMs,
                _sampleEncodeInactivityTimeoutMs);
            if (run.ExitCode == 0 && File.Exists(outputPath))
            {
                return true;
            }

            TryDelete(outputPath);
        }

        return false;
    }

    public double? EstimateFast(AutoSampleReductionInput input)
    {
        if (!TryResolveSourceBitrateBps(input.InputPath, out var sourceBitrateBps))
        {
            return null;
        }

        if (sourceBitrateBps <= 0 || input.Maxrate <= 0)
        {
            return null;
        }

        var targetBitrateBps = input.Maxrate * Megabit;
        var reduction = (1d - targetBitrateBps / sourceBitrateBps) * 100d;
        return Math.Round(reduction, 3, MidpointRounding.AwayFromZero);
    }

    private bool TryResolveSourceBitrateBps(string inputPath, out double bitrateBps)
    {
        bitrateBps = 0;
        var probe = _probeReader.Read(inputPath);
        var format = probe?.Format;
        if (format?.DurationSeconds is not > 0)
        {
            return false;
        }

        if (format.BitrateBps is > 0)
        {
            bitrateBps = format.BitrateBps.Value;
            return true;
        }

        if (!File.Exists(inputPath))
        {
            return false;
        }

        var bytes = new FileInfo(inputPath).Length;
        if (bytes <= 0)
        {
            return false;
        }

        bitrateBps = bytes * 8d / format.DurationSeconds.Value;
        return true;
    }

    private bool TryResolveDurationAndSourceSize(string inputPath, out double durationSeconds, out long sourceBytes)
    {
        durationSeconds = 0;
        sourceBytes = 0;

        if (!File.Exists(inputPath))
        {
            return false;
        }

        var probe = _probeReader.Read(inputPath);
        var format = probe?.Format;
        if (format?.DurationSeconds is not > 0)
        {
            return false;
        }

        var bytes = new FileInfo(inputPath).Length;
        if (bytes <= 0)
        {
            return false;
        }

        durationSeconds = format.DurationSeconds.Value;
        sourceBytes = bytes;
        return true;
    }

    private string BuildSampleEncodeArguments(
        string inputPath,
        string outputPath,
        AutoSampleReductionInput input,
        double startSeconds,
        double durationSeconds)
    {
        var startToken = startSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var durationToken = durationSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        var cqToken = input.Cq.ToString(CultureInfo.InvariantCulture);

        return $"-hide_banner -y -ss {startToken} -t {durationToken} -i {Quote(inputPath)} " +
               $"-map 0:v:0 -c:v h264_nvenc -preset {_nvencPreset} -rc vbr_hq -cq {cqToken} -b:v 0 " +
               $"-maxrate {ToRateToken(input.Maxrate)} -bufsize {ToRateToken(input.Bufsize)} -an -sn {Quote(outputPath)}";
    }

    private IReadOnlyList<SampleWindow> ResolveSampleWindows(double durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return [];
        }

        var windowDuration = Math.Min(_sampleDurationSeconds, durationSeconds);
        var anchors = durationSeconds switch
        {
            >= 5_400 => new[] { 0.15, 0.50, 0.85 },
            >= 1_800 => new[] { 0.30, 0.70 },
            _ => new[] { 0.50 }
        };

        var result = new List<SampleWindow>(anchors.Length);
        foreach (var anchor in anchors)
        {
            var center = durationSeconds * anchor;
            var start = Math.Max(0, center - windowDuration / 2d);
            if (start + windowDuration > durationSeconds)
            {
                start = Math.Max(0, durationSeconds - windowDuration);
            }

            result.Add(new SampleWindow(start, windowDuration));
        }

        return result;
    }

    private static string ToRateToken(double value)
    {
        return $"{value.ToString("0.###", CultureInfo.InvariantCulture)}M";
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup for temporary files.
        }
    }

    private static string CreateSampleWorkDirectory(string inputPath)
    {
        var sourceDirectory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            sourceDirectory = Path.GetTempPath();
        }

        var workDir = Path.Combine(sourceDirectory, $".mte-autosample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        return workDir;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temporary directories.
        }
    }

    private sealed record SampleWindow(double StartSeconds, double DurationSeconds);
}
