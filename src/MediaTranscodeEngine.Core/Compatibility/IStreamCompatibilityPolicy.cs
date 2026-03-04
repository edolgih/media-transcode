namespace MediaTranscodeEngine.Core.Compatibility;

public interface IStreamCompatibilityPolicy
{
    StreamCompatibilityDecision Decide(StreamCompatibilityInput input);
}
