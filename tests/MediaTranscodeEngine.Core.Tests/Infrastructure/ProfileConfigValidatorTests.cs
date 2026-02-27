using FluentAssertions;
using MediaTranscodeEngine.Core.Infrastructure;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Infrastructure;

public class ProfileConfigValidatorTests
{
    [Fact]
    public void Validate576Config_WithValidConfig_DoesNotThrow()
    {
        var config = CreateValidConfig();

        var action = () => ProfileConfigValidator.Validate576Config(config);

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.0, 2.0, "CqStepToMaxrateStep")]
    [InlineData(-0.1, 2.0, "CqStepToMaxrateStep")]
    [InlineData(0.4, 0.0, "BufsizeMultiplier")]
    [InlineData(0.4, -1.0, "BufsizeMultiplier")]
    public void Validate576Config_WhenRateModelInvalid_ThrowsInvalidOperationException(
        double cqStepToMaxrateStep,
        double bufsizeMultiplier,
        string expectedMessagePart)
    {
        var config = CreateValidConfig() with
        {
            RateModel = new RateModelSettings(cqStepToMaxrateStep, bufsizeMultiplier)
        };

        var action = () => ProfileConfigValidator.Validate576Config(config);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessagePart}*");
    }

    [Fact]
    public void Validate576Config_WhenDefaultsAndLimitsMismatch_ThrowsInvalidOperationException()
    {
        var mismatchedProfile = new ContentProfileSettings(
            AlgoDefault: "bilinear",
            Defaults: new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new ProfileDefaults(Cq: 26, Maxrate: 3.4, Bufsize: 6.9)
            },
            Limits: new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
            {
                ["high"] = new ProfileLimits(CqMin: 16, CqMax: 33, MaxrateMin: 2.0, MaxrateMax: 8.0)
            });

        var config = CreateValidConfig() with
        {
            ContentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
            {
                ["film"] = mismatchedProfile
            }
        };

        var action = () => ProfileConfigValidator.Validate576Config(config);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*missing Limits*");
    }

    private static TranscodePolicyConfig CreateValidConfig()
    {
        var contentProfiles = new Dictionary<string, ContentProfileSettings>(StringComparer.OrdinalIgnoreCase)
        {
            ["film"] = new ContentProfileSettings(
                AlgoDefault: "bilinear",
                Defaults: new Dictionary<string, ProfileDefaults>(StringComparer.OrdinalIgnoreCase)
                {
                    ["default"] = new ProfileDefaults(Cq: 26, Maxrate: 3.4, Bufsize: 6.9)
                },
                Limits: new Dictionary<string, ProfileLimits>(StringComparer.OrdinalIgnoreCase)
                {
                    ["default"] = new ProfileLimits(CqMin: 18, CqMax: 35, MaxrateMin: 1.6, MaxrateMax: 8.0)
                })
        };

        return new TranscodePolicyConfig(
            ContentProfiles: contentProfiles,
            RateModel: new RateModelSettings(CqStepToMaxrateStep: 0.4, BufsizeMultiplier: 2.0));
    }
}
