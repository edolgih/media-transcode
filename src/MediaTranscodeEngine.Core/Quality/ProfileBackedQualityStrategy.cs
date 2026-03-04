using MediaTranscodeEngine.Core.Profiles;

namespace MediaTranscodeEngine.Core.Quality;

public sealed class ProfileBackedQualityStrategy : IQualityStrategy
{
    private readonly IProfileDefinitionRepository _profileRepository;
    private readonly ProfilePolicy _policy;

    public ProfileBackedQualityStrategy(
        IProfileDefinitionRepository profileRepository,
        ProfilePolicy policy)
    {
        _profileRepository = profileRepository;
        _policy = policy;
    }

    public QualitySettings Resolve(QualitySelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profile = _profileRepository.GetDefaultProfile();
        return _policy.ResolveBaseSettings(profile, context);
    }
}
