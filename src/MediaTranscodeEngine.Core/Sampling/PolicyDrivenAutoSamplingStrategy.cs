using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Sampling;

public sealed class PolicyDrivenAutoSamplingStrategy : IAutoSamplingStrategy
{
    private readonly IProfileDefinitionRepository _profileRepository;
    private readonly ProfilePolicy _policy;

    public PolicyDrivenAutoSamplingStrategy(
        IProfileDefinitionRepository profileRepository,
        ProfilePolicy policy)
    {
        _profileRepository = profileRepository;
        _policy = policy;
    }

    public QualitySettings Resolve(AutoSamplingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profile = _profileRepository.GetDefaultProfile();
        return _policy.ResolveAutoSampleSettings(profile, context);
    }
}
