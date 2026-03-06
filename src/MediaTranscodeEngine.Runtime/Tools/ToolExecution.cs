namespace MediaTranscodeEngine.Runtime.Tools;

/// <summary>
/// Represents the executable command sequence prepared by a concrete transcode tool.
/// </summary>
public sealed record ToolExecution(
    string ToolName,
    IReadOnlyList<string> Commands);
