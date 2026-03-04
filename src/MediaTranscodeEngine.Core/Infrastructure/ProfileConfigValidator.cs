using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Infrastructure;

public static class ProfileConfigValidator
{
    public static void Validate576Config(TranscodePolicyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.ContentProfiles.Count == 0)
        {
            throw new InvalidOperationException("Profile config is invalid: ContentProfiles is empty.");
        }

        if (config.RateModel.CqStepToMaxrateStep <= 0)
        {
            throw new InvalidOperationException("Profile config is invalid: RateModel.CqStepToMaxrateStep must be positive.");
        }

        if (config.RateModel.BufsizeMultiplier <= 0)
        {
            throw new InvalidOperationException("Profile config is invalid: RateModel.BufsizeMultiplier must be positive.");
        }

        foreach (var contentProfile in config.ContentProfiles)
        {
            ValidateContentProfile(contentProfile.Key, contentProfile.Value);
        }

        ValidateAutoSampling(config.AutoSampling);
        ValidateDownscaleTargets(config.DownscaleTargets);
    }

    private static void ValidateContentProfile(string contentProfileName, ContentProfileSettings contentProfile)
    {
        if (string.IsNullOrWhiteSpace(contentProfile.AlgoDefault))
        {
            throw new InvalidOperationException(
                $"Profile config is invalid: '{contentProfileName}' has empty AlgoDefault.");
        }

        if (contentProfile.Defaults.Count == 0)
        {
            throw new InvalidOperationException(
                $"Profile config is invalid: '{contentProfileName}' has empty Defaults.");
        }

        if (contentProfile.Limits.Count == 0)
        {
            throw new InvalidOperationException(
                $"Profile config is invalid: '{contentProfileName}' has empty Limits.");
        }

        foreach (var quality in contentProfile.Defaults.Keys)
        {
            if (!contentProfile.Limits.ContainsKey(quality))
            {
                throw new InvalidOperationException(
                    $"Profile config is invalid: '{contentProfileName}/{quality}' is missing Limits.");
            }
        }

        foreach (var quality in contentProfile.Limits.Keys)
        {
            if (!contentProfile.Defaults.ContainsKey(quality))
            {
                throw new InvalidOperationException(
                    $"Profile config is invalid: '{contentProfileName}/{quality}' is missing Defaults.");
            }
        }

        foreach (var entry in contentProfile.Limits)
        {
            var quality = entry.Key;
            var limits = entry.Value;

            if (limits.CqMin > limits.CqMax)
            {
                throw new InvalidOperationException(
                    $"Profile config is invalid: '{contentProfileName}/{quality}' has CqMin > CqMax.");
            }

            if (limits.MaxrateMin > limits.MaxrateMax)
            {
                throw new InvalidOperationException(
                    $"Profile config is invalid: '{contentProfileName}/{quality}' has MaxrateMin > MaxrateMax.");
            }
        }
    }

    private static void ValidateAutoSampling(AutoSamplingSettings? autoSampling)
    {
        if (autoSampling is null)
        {
            return;
        }

        if (autoSampling.MaxIterations <= 0)
        {
            throw new InvalidOperationException("Profile config is invalid: AutoSampling.MaxIterations must be positive.");
        }

        if (autoSampling.HybridAccurateIterations <= 0)
        {
            throw new InvalidOperationException("Profile config is invalid: AutoSampling.HybridAccurateIterations must be positive.");
        }

        if (autoSampling.MediumVideoThresholdSeconds <= 0 || autoSampling.LongVideoThresholdSeconds <= 0)
        {
            throw new InvalidOperationException("Profile config is invalid: AutoSampling thresholds must be positive.");
        }

        if (autoSampling.LongVideoThresholdSeconds < autoSampling.MediumVideoThresholdSeconds)
        {
            throw new InvalidOperationException("Profile config is invalid: AutoSampling.LongVideoThresholdSeconds must be >= MediumVideoThresholdSeconds.");
        }

        ValidateAnchors("AutoSampling.LongVideoAnchors", autoSampling.LongVideoAnchors);
        ValidateAnchors("AutoSampling.MediumVideoAnchors", autoSampling.MediumVideoAnchors);
        ValidateAnchors("AutoSampling.ShortVideoAnchors", autoSampling.ShortVideoAnchors);
    }

    private static void ValidateDownscaleTargets(IReadOnlyDictionary<int, DownscaleTargetSettings>? targets)
    {
        if (targets is null)
        {
            return;
        }

        foreach (var entry in targets)
        {
            if (entry.Key <= 0)
            {
                throw new InvalidOperationException("Profile config is invalid: DownscaleTargets keys must be positive.");
            }
        }
    }

    private static void ValidateAnchors(string name, IReadOnlyList<double>? anchors)
    {
        if (anchors is null || anchors.Count == 0)
        {
            return;
        }

        foreach (var anchor in anchors)
        {
            if (anchor <= 0 || anchor >= 1)
            {
                throw new InvalidOperationException($"Profile config is invalid: {name} values must be in range (0; 1).");
            }
        }
    }
}
