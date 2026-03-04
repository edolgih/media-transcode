namespace MediaTranscodeEngine.Core.Compatibility;

public sealed record StreamCompatibilityInput(
    bool IsMkvInput,
    bool HasAudioStream,
    bool IsVideoCopyCompatible,
    bool HasNonAacAudio,
    bool ForceSyncAudio,
    bool NeedVideoEncode);
