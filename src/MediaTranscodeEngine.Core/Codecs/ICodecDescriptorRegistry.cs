namespace MediaTranscodeEngine.Core.Codecs;

public interface ICodecDescriptorRegistry
{
    bool TryGet(string codecId, out CodecDescriptor descriptor);
}
