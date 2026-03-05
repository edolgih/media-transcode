using MediaTranscodeEngine.Core.Engine;
namespace MediaTranscodeEngine.Core.Codecs;

public sealed class InMemoryCodecDescriptorRegistry : ICodecDescriptorRegistry
{
    private readonly IReadOnlyDictionary<string, CodecDescriptor> _descriptors;

    public InMemoryCodecDescriptorRegistry(IEnumerable<CodecDescriptor>? descriptors = null)
    {
        var effectiveDescriptors = descriptors?.ToArray();
        if (effectiveDescriptors is null || effectiveDescriptors.Length == 0)
        {
            effectiveDescriptors = CreateDefaultDescriptors().ToArray();
        }

        var map = new Dictionary<string, CodecDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in effectiveDescriptors)
        {
            map[BuildKey(descriptor.CodecId)] = descriptor;
        }

        _descriptors = map;
    }

    public bool TryGet(string codecId, out CodecDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(codecId))
        {
            descriptor = null!;
            return false;
        }

        return _descriptors.TryGetValue(BuildKey(codecId), out descriptor!);
    }

    private static string BuildKey(string codecId)
    {
        return codecId.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<CodecDescriptor> CreateDefaultDescriptors()
    {
        return
        [
            new CodecDescriptor(
                codecId: RequestContracts.General.H264VideoCodec,
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container]),
            new CodecDescriptor(
                codecId: RequestContracts.General.H265VideoCodec,
                supportedContainers: [RequestContracts.General.MkvContainer, RequestContracts.General.Mp4Container])
        ];
    }
}
