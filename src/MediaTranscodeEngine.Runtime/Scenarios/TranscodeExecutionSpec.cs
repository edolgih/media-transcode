namespace MediaTranscodeEngine.Runtime.Scenarios;

/*
Это маркер для scenario-specific execution payload.
Он нужен, когда общего плана недостаточно и сценарий хочет передать инструменту локальные детали исполнения.
*/
/// <summary>
/// Represents an optional scenario-specific execution payload that a tool can consume alongside a shared transcode plan.
/// </summary>
public abstract class TranscodeExecutionSpec
{
}
