using Transcode.Cli.Core.Parsing;
using Transcode.Scenarios.ToH264Rife.Core;

namespace Transcode.Scenarios.ToH264Rife.Cli;

/// <summary>
/// Parses <c>toh264rife</c> CLI tokens into a scenario request.
/// </summary>
internal static class ToH264RifeCliRequestParser
{
    private const string KeepSourceOptionName = "--keep-source";
    private const string TargetFpsOptionName = "--target-fps";
    private const string ContainerOptionName = "--container";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out ToH264RifeRequest request,
        out string? errorText)
    {
        request = default!;
        errorText = null;

        var keepSource = false;
        int? targetFramesPerSecond = null;
        string? outputContainer = null;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (string.Equals(token, KeepSourceOptionName, StringComparison.OrdinalIgnoreCase))
            {
                keepSource = true;
                continue;
            }

            if (string.Equals(token, TargetFpsOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadInt(
                        args,
                        ref index,
                        token,
                        $"--target-fps must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedTargetFrameRates)}.",
                        out targetFramesPerSecond,
                        out errorText))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(token, ContainerOptionName, StringComparison.OrdinalIgnoreCase))
            {
                if (!CliOptionReader.TryReadRequiredValue(args, ref index, token, out outputContainer, out errorText))
                {
                    return false;
                }

                continue;
            }

            errorText = $"Unexpected argument: {token}";
            return false;
        }

        try
        {
            request = new ToH264RifeRequest(
                keepSource: keepSource,
                targetFramesPerSecond: targetFramesPerSecond,
                outputContainer: outputContainer);
            return true;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            errorText = exception.ParamName switch
            {
                "targetFramesPerSecond" => $"--target-fps must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedTargetFrameRates)}.",
                "outputContainer" => $"--container must be one of: {CliValueFormatter.FormatList(ToH264RifeRequest.SupportedContainers)}.",
                _ => exception.Message
            };
            return false;
        }
    }
}
