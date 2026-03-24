namespace Transcode.Core.VideoSettings;

/// <summary>
/// Represents resolved profile-driven video settings defaults without autosample adjustments.
/// </summary>
public sealed record ResolvedVideoSettingsDefaults(
    string ContentProfile,
    string QualityProfile,
    int Cq,
    decimal Maxrate,
    decimal Bufsize)
{
    /// <summary>
    /// Gets the resolved VBV maxrate in kilobits per second.
    /// </summary>
    public int MaxrateKbps => ToKbps(Maxrate);

    /// <summary>
    /// Gets the resolved VBV bufsize in kilobits per second.
    /// </summary>
    public int BufsizeKbps => ToKbps(Bufsize);

    private static int ToKbps(decimal value)
    {
        return (int)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    }
}
