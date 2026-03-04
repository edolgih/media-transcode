namespace MediaTranscodeEngine.Core.Abstractions;

public sealed record ProcessRunResult(
    int ExitCode,
    string StdOut,
    string StdErr);

public interface IProcessRunner
{
    ProcessRunResult Run(string fileName, string arguments, int timeoutMs = 30_000);

    ProcessRunResult RunWithInactivityTimeout(
        string fileName,
        string arguments,
        int timeoutMs = 30_000,
        int inactivityTimeoutMs = 0)
    {
        return Run(fileName, arguments, timeoutMs);
    }
}
