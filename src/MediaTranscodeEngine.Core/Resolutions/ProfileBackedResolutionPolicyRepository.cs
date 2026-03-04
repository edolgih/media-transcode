using MediaTranscodeEngine.Core.Profiles;
using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Resolutions;

public sealed class ProfileBackedResolutionPolicyRepository : IResolutionPolicyRepository
{
    private readonly IProfileDefinitionRepository _profileRepository;
    private readonly ProfilePolicy _policy;

    public ProfileBackedResolutionPolicyRepository(
        IProfileDefinitionRepository profileRepository,
        ProfilePolicy policy)
    {
        _profileRepository = profileRepository;
        _policy = policy;
    }

    public ResolutionPolicyResult Resolve(ResolutionPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Transform.TargetHeight.HasValue)
        {
            return new ResolutionPolicyResult(
                IsSupported: true,
                ApplyDownscale: false);
        }

        var targetHeight = request.Transform.TargetHeight.Value;
        var targetProfile = _profileRepository.GetTargetProfile(targetHeight);
        if (targetProfile is null)
        {
            return new ResolutionPolicyResult(
                IsSupported: false,
                ApplyDownscale: false,
                Error: $"Downscale {targetHeight} is not supported.");
        }

        if (!targetProfile.IsSupported || targetProfile.Profile is null)
        {
            return new ResolutionPolicyResult(
                IsSupported: false,
                ApplyDownscale: false,
                Error: string.IsNullOrWhiteSpace(targetProfile.UnsupportedReason)
                    ? $"Downscale {targetHeight} is not supported."
                    : targetProfile.UnsupportedReason);
        }

        var sourceHeight = request.Transform.SourceHeight;
        if (!sourceHeight.HasValue || sourceHeight.Value <= targetHeight)
        {
            return new ResolutionPolicyResult(
                IsSupported: true,
                ApplyDownscale: false);
        }

        var profile = targetProfile.Profile;
        var settings = _policy.ResolveBaseSettings(
            profile,
            new QualitySelectionContext(
                ContentProfile: request.ContentProfile,
                QualityProfile: request.QualityProfile,
                Cq: request.Cq,
                Maxrate: request.Maxrate,
                Bufsize: request.Bufsize,
                DownscaleAlgo: request.DownscaleAlgo));
        var sourceBucket = _policy.ResolveSourceBucket(profile, sourceHeight);
        if (sourceBucket is null)
        {
            return new ResolutionPolicyResult(
                IsSupported: false,
                ApplyDownscale: true,
                Settings: settings,
                Error: "576 source bucket missing.");
        }

        var validationError = _policy.GetSourceBucketMatrixValidationError(
            profile,
            sourceBucket,
            request.ContentProfile,
            request.QualityProfile);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return new ResolutionPolicyResult(
                IsSupported: false,
                ApplyDownscale: true,
                SourceBucketName: sourceBucket.Name,
                Settings: settings,
                Error: validationError);
        }

        return new ResolutionPolicyResult(
            IsSupported: true,
            ApplyDownscale: true,
            SourceBucketName: sourceBucket.Name,
            Settings: settings);
    }
}
