using Microsoft.Extensions.Configuration;

namespace Transcode.Cli.Core;

/*
Это резолв: configured executable paths, keeping bare command names intact and converting
*/
/// <summary>
/// Resolves configured executable paths, keeping bare command names intact and converting
/// repo-local relative paths to absolute file-system paths when possible.
/// </summary>
public static class CliPathResolver
{
    /*
    Это чтение: a required configuration value and resolves it to an effective executable token
    */
    /// <summary>
    /// Reads a required configuration value and resolves it to an effective executable token.
    /// </summary>
    /// <param name="configuration">Resolved configuration.</param>
    /// <param name="key">Configuration key.</param>
    /// <param name="consumer">Human-readable consumer name used in error messages.</param>
    /// <returns>Effective executable token.</returns>
    public static string GetRequiredExecutable(IConfiguration configuration, string key, string consumer)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration key '{key}' is required for {consumer}.");
        }

        return ResolveExecutable(value, AppContext.BaseDirectory, Directory.GetCurrentDirectory());
    }

    internal static string ResolveExecutable(
        string configuredValue,
        string appBaseDirectory,
        string currentDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        var normalized = configuredValue.Trim().Trim('"');
        if (!LooksLikePath(normalized))
        {
            return normalized;
        }

        if (Path.IsPathRooted(normalized))
        {
            return Path.GetFullPath(normalized);
        }

        var candidates = EnumerateRelativeCandidates(normalized, appBaseDirectory, currentDirectory).ToArray();
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return candidates.Length == 0
            ? normalized
            : Path.GetFullPath(candidates[0]);
    }

    internal static string? TryFindRepositoryRoot(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);

        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Transcode.sln")) ||
                Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateRelativeCandidates(
        string relativePath,
        string appBaseDirectory,
        string currentDirectory)
    {
        var repoRoot = TryFindRepositoryRoot(appBaseDirectory);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            yield return Path.Combine(repoRoot, relativePath);
        }

        yield return Path.Combine(currentDirectory, relativePath);
        yield return Path.Combine(appBaseDirectory, relativePath);
    }

    private static bool LooksLikePath(string value)
    {
        return value.Contains('\\') ||
               value.Contains('/') ||
               value.Contains(':') ||
               value.Any(char.IsWhiteSpace);
    }
}
