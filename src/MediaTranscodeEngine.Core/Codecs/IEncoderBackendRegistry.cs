namespace MediaTranscodeEngine.Core.Codecs;

public interface IEncoderBackendRegistry
{
    bool TryGet(string backendId, out EncoderBackendDescriptor descriptor);
}
