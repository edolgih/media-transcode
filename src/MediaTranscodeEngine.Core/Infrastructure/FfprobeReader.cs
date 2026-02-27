using MediaTranscodeEngine.Core.Abstractions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Infrastructure;

public sealed class FfprobeReader : IProbeReader
{
    private readonly IProcessRunner _processRunner;
    private readonly string _ffprobePath;
    private readonly int _timeoutMs;

    public FfprobeReader(
        IProcessRunner processRunner,
        string ffprobePath = "ffprobe",
        int timeoutMs = 30_000)
    {
        _processRunner = processRunner;
        _ffprobePath = ffprobePath;
        _timeoutMs = timeoutMs;
    }

    public ProbeResult? Read(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var arguments = $"-v error -print_format json -show_format -show_streams {Quote(inputPath)}";
        var run = _processRunner.Run(_ffprobePath, arguments, _timeoutMs);

        if (run.ExitCode != 0 || string.IsNullOrWhiteSpace(run.StdOut))
        {
            return null;
        }

        return ProbeJsonParser.Parse(run.StdOut);
    }

    private static string Quote(string value)
    {
        var escaped = value.Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
