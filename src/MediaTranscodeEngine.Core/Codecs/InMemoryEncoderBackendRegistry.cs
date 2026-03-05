using MediaTranscodeEngine.Core.Engine;
using MediaTranscodeEngine.Core.Execution;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class InMemoryEncoderBackendRegistry : IEncoderBackendRegistry
{
    private readonly IReadOnlyDictionary<string, EncoderBackendDescriptor> _descriptors;

    public InMemoryEncoderBackendRegistry(IEnumerable<EncoderBackendDescriptor>? descriptors = null)
    {
        var effectiveDescriptors = descriptors?.ToArray();
        if (effectiveDescriptors is null || effectiveDescriptors.Length == 0)
        {
            effectiveDescriptors = CreateDefaultDescriptors().ToArray();
        }

        var map = new Dictionary<string, EncoderBackendDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in effectiveDescriptors)
        {
            map[descriptor.BackendId] = descriptor;
        }

        _descriptors = map;
    }

    public bool TryGet(string backendId, out EncoderBackendDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(backendId))
        {
            descriptor = null!;
            return false;
        }

        return _descriptors.TryGetValue(backendId.Trim(), out descriptor!);
    }

    private static IReadOnlyList<EncoderBackendDescriptor> CreateDefaultDescriptors()
    {
        return
        [
            new EncoderBackendDescriptor(
                backendId: RequestContracts.General.GpuEncoderBackend,
                codecStrategyKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [RequestContracts.General.H264VideoCodec] = CodecExecutionKeys.H264Gpu,
                    [RequestContracts.General.H265VideoCodec] = CodecExecutionKeys.BuildGpuEncodeKey(RequestContracts.General.H265VideoCodec)
                }),
            new EncoderBackendDescriptor(
                backendId: RequestContracts.General.CpuEncoderBackend,
                codecStrategyKeys: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase))
        ];
    }
}
