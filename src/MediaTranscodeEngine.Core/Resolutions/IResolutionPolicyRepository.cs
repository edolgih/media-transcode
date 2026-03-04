namespace MediaTranscodeEngine.Core.Resolutions;

public interface IResolutionPolicyRepository
{
    ResolutionPolicyResult Resolve(ResolutionPolicyRequest request);
}
