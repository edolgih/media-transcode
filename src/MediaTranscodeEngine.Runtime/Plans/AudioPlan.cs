namespace MediaTranscodeEngine.Runtime.Plans;

/// <summary>
/// Represents a normalized audio intent for a transcode plan.
/// </summary>
public abstract record AudioPlan;

/// <summary>
/// Represents an audio-copy intent with no encode-specific processing.
/// </summary>
public sealed record CopyAudioPlan : AudioPlan;

/// <summary>
/// Represents an explicit audio-encode intent and its repair mode.
/// </summary>
public record EncodeAudioPlan : AudioPlan;

/// <summary>
/// Represents an audio-encode intent that also requires timestamp repair.
/// </summary>
public record RepairAudioPlan : EncodeAudioPlan;

/// <summary>
/// Represents an audio-encode intent that also requires the sync-safe path.
/// </summary>
public sealed record SynchronizeAudioPlan : RepairAudioPlan;
