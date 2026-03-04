namespace MediaTranscodeEngine.Core.Profiles;

public sealed record DownscaleTargetProfile(
    int TargetHeight,
    bool IsSupported,
    string? UnsupportedReason = null,
    TranscodeProfileDefinition? Profile = null);
