using System.Diagnostics;
using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void Run_WhenProcessExceedsTimeout_ReturnsTimeoutResultQuickly()
    {
        var sut = new ProcessRunner();
        var sw = Stopwatch.StartNew();

        var actual = sut.Run(
            fileName: "cmd.exe",
            arguments: "/c ping -n 4 127.0.0.1 > nul",
            timeoutMs: 300);

        sw.Stop();

        actual.ExitCode.Should().Be(-1);
        actual.StdErr.Should().Contain("Process timeout");
        sw.ElapsedMilliseconds.Should().BeLessThan(2_500);
    }

    [Fact]
    public void Run_WhenProcessWritesToStdOutAndStdErr_CapturesBothStreams()
    {
        var sut = new ProcessRunner();

        var actual = sut.Run(
            fileName: "cmd.exe",
            arguments: "/c echo out-line && echo err-line 1>&2",
            timeoutMs: 5_000);

        actual.ExitCode.Should().Be(0);
        actual.StdOut.Should().Contain("out-line");
        actual.StdErr.Should().Contain("err-line");
    }

    [Fact]
    public void RunWithInactivityTimeout_WhenNoOutputForTooLong_ReturnsInactivityTimeoutResult()
    {
        var sut = new ProcessRunner();
        var sw = Stopwatch.StartNew();

        var actual = sut.RunWithInactivityTimeout(
            fileName: "cmd.exe",
            arguments: "/c ping -n 6 127.0.0.1 > nul",
            timeoutMs: 10_000,
            inactivityTimeoutMs: 300);

        sw.Stop();

        actual.ExitCode.Should().Be(-1);
        actual.StdErr.Should().Contain("inactivity timeout");
        sw.ElapsedMilliseconds.Should().BeLessThan(3_000);
    }

    [Fact]
    public void RunWithInactivityTimeout_WhenProcessProducesOutput_DoesNotTimeoutByInactivity()
    {
        var sut = new ProcessRunner();

        var actual = sut.RunWithInactivityTimeout(
            fileName: "cmd.exe",
            arguments: "/c ping -n 3 127.0.0.1",
            timeoutMs: 10_000,
            inactivityTimeoutMs: 2_000);

        actual.ExitCode.Should().Be(0);
        actual.StdOut.Should().NotBeNullOrWhiteSpace();
    }
}
