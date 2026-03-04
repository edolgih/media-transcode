namespace MediaTranscodeEngine.Core.Profiles;

public interface IProfileDefinitionRepository
{
    TranscodeProfileDefinition GetDefaultProfile();

    DownscaleTargetProfile? GetTargetProfile(int targetHeight);

    IReadOnlyCollection<DownscaleTargetProfile> GetTargetProfiles();
}
