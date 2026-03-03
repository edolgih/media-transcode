namespace MediaTranscodeEngine.Core.Engine;

internal static class OutputPathBuilder
{
    public static string BuildKeepSourceOutputPath(
        string inputPath,
        string outputExtension,
        params string?[] suffixes)
    {
        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = ".";
        }

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var normalizedExtension = outputExtension.StartsWith(".", StringComparison.Ordinal)
            ? outputExtension
            : $".{outputExtension}";

        var normalizedSuffixes = suffixes
            .Where(static suffix => !string.IsNullOrWhiteSpace(suffix))
            .Select(static suffix => NormalizeSuffix(suffix!))
            .Where(static suffix => suffix.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suffixToken = normalizedSuffixes.Length == 0
            ? string.Empty
            : "_" + string.Join("_", normalizedSuffixes);
        var candidatePath = Path.Combine(directory, $"{baseName}{suffixToken}{normalizedExtension}");

        if (PathEquals(candidatePath, inputPath))
        {
            return Path.Combine(directory, $"{baseName}_out{normalizedExtension}");
        }

        return candidatePath;
    }

    private static string NormalizeSuffix(string suffix)
    {
        var trimmed = suffix.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var chars = trimmed
            .Select(static ch =>
            {
                if (char.IsLetterOrDigit(ch))
                {
                    return ch;
                }

                return ch is '-' or '_' ? ch : '-';
            })
            .ToArray();

        return new string(chars).Trim('-');
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            var leftFullPath = Path.GetFullPath(left);
            var rightFullPath = Path.GetFullPath(right);
            return string.Equals(leftFullPath, rightFullPath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
