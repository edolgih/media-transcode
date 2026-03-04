namespace MediaTranscodeEngine.Core.Compatibility;

public sealed record StreamCompatibilityDecision(
    bool NeedAudioEncode,
    bool NeedContainerChange,
    bool IsCopyPath,
    bool ForceSyncAudio,
    IReadOnlyList<string> Reasons);
