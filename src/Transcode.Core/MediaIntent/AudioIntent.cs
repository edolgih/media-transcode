namespace Transcode.Core.MediaIntent;

/*
Это модель, которая представляет a normalized audio intent for a scenario decision
*/
/// <summary>
/// Represents a normalized audio intent for a scenario decision.
/// </summary>
public abstract record AudioIntent;

/*
Это модель, которая представляет an audio-copy intent with no encode-specific processing
*/
/// <summary>
/// Represents an audio-copy intent with no encode-specific processing.
/// </summary>
public sealed record CopyAudioIntent : AudioIntent;

/*
Это модель, которая представляет an explicit audio-encode intent and its repair mode
*/
/// <summary>
/// Represents an explicit audio-encode intent and its repair mode.
/// </summary>
public record EncodeAudioIntent : AudioIntent;

/*
Это модель, которая представляет an audio-encode intent that also requires timestamp repair
*/
/// <summary>
/// Represents an audio-encode intent that also requires timestamp repair.
/// </summary>
public record RepairAudioIntent : EncodeAudioIntent;

/*
Это модель, которая представляет an audio-encode intent that also requires the sync-safe path
*/
/// <summary>
/// Represents an audio-encode intent that also requires the sync-safe path.
/// </summary>
public sealed record SynchronizeAudioIntent : RepairAudioIntent;
