namespace MediaTranscodeEngine.Core.Codecs;

public sealed class EncoderBackendDescriptor
{
    private readonly IReadOnlyDictionary<string, string> _codecStrategyKeys;

    public EncoderBackendDescriptor(
        string backendId,
        IReadOnlyDictionary<string, string> codecStrategyKeys)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            throw new ArgumentException("Backend id is required.", nameof(backendId));
        }

        ArgumentNullException.ThrowIfNull(codecStrategyKeys);
        BackendId = backendId.Trim().ToLowerInvariant();
        _codecStrategyKeys = new Dictionary<string, string>(
            codecStrategyKeys
                .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(
                    static pair => pair.Key.Trim().ToLowerInvariant(),
                    static pair => pair.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    public string BackendId { get; }

    public bool TryGetStrategyKey(string codecId, out string strategyKey)
    {
        if (string.IsNullOrWhiteSpace(codecId))
        {
            strategyKey = string.Empty;
            return false;
        }

        return _codecStrategyKeys.TryGetValue(codecId.Trim(), out strategyKey!);
    }
}
