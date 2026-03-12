namespace MediaTranscodeEngine.Runtime.Scenarios;

/*
Это общий базовый тип для scenario-specific execution payload.
Он нужен только тогда, когда общего TranscodePlan недостаточно и сценарий хочет передать tool-адаптеру локальные детали исполнения.
*/
/// <summary>
/// Represents an optional scenario-specific execution payload that a tool can consume alongside a shared transcode plan.
/// </summary>
public abstract class TranscodeExecutionSpec
{
}
