using FluentAssertions;
using Transcode.Cli.Core;

namespace Transcode.Cli.Tests;

public sealed class CliPathResolverTests
{
    [Fact]
    public void ResolveExecutable_WhenConfiguredValueIsBareCommand_KeepsBareCommand()
    {
        var actual = CliPathResolver.ResolveExecutable(
            "rife-ncnn-vulkan",
            appBaseDirectory: @"C:\repo\src\Transcode.Cli\bin\Debug\net9.0",
            currentDirectory: @"C:\repo");

        actual.Should().Be("rife-ncnn-vulkan");
    }

    [Fact]
    public void ResolveExecutable_WhenConfiguredValueIsAbsolutePath_ReturnsAbsolutePath()
    {
        var actual = CliPathResolver.ResolveExecutable(
            @"D:\tools\rife-ncnn-vulkan\rife-ncnn-vulkan.exe",
            appBaseDirectory: @"C:\repo\src\Transcode.Cli\bin\Debug\net9.0",
            currentDirectory: @"C:\repo");

        actual.Should().Be(Path.GetFullPath(@"D:\tools\rife-ncnn-vulkan\rife-ncnn-vulkan.exe"));
    }

    [Fact]
    public void ResolveExecutable_WhenConfiguredValueIsRepoRelative_ResolvesAgainstRepositoryRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var appBase = Path.Combine(repoRoot, "src", "Transcode.Cli", "bin", "Debug", "net9.0");
        var exePath = Path.Combine(repoRoot, "tools", "third_party", "rife", "rife-ncnn-vulkan.exe");

        Directory.CreateDirectory(appBase);
        Directory.CreateDirectory(Path.GetDirectoryName(exePath)!);
        File.WriteAllText(Path.Combine(repoRoot, "Transcode.sln"), string.Empty);
        File.WriteAllText(exePath, string.Empty);

        try
        {
            var actual = CliPathResolver.ResolveExecutable(
                @"tools/third_party/rife/rife-ncnn-vulkan.exe",
                appBaseDirectory: appBase,
                currentDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

            actual.Should().Be(Path.GetFullPath(exePath));
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }
}
