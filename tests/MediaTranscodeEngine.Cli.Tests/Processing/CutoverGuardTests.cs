using FluentAssertions;

namespace MediaTranscodeEngine.Cli.Tests.Processing;

public class CutoverGuardTests
{
    [Fact]
    public async Task Run_Help_ShowsRuntimeConfigurationKeys()
    {
        var result = await CliProcessRunner.RunAsync(["--help"]);

        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("RuntimeValues:ProfilesYamlPath");
    }
}
