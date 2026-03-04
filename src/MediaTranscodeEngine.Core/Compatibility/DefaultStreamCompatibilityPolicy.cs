namespace MediaTranscodeEngine.Core.Compatibility;

public sealed class DefaultStreamCompatibilityPolicy : IStreamCompatibilityPolicy
{
    public StreamCompatibilityDecision Decide(StreamCompatibilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var reasons = new List<string>();
        var needAudio = input.HasAudioStream && input.HasNonAacAudio;
        if (!input.IsVideoCopyCompatible)
        {
            reasons.Add("video incompatible");
        }

        if (needAudio)
        {
            reasons.Add("audio non-aac");
        }

        if (input.ForceSyncAudio && input.HasAudioStream)
        {
            reasons.Add("sync audio");
        }

        var needAudioEncode = input.HasAudioStream && (needAudio || input.NeedVideoEncode || input.ForceSyncAudio);
        var needContainerChange = !input.IsMkvInput;
        var isCopyPath = input.IsMkvInput && !input.NeedVideoEncode && !needAudioEncode;

        return new StreamCompatibilityDecision(
            NeedAudioEncode: needAudioEncode,
            NeedContainerChange: needContainerChange,
            IsCopyPath: isCopyPath,
            ForceSyncAudio: input.ForceSyncAudio,
            Reasons: reasons);
    }
}
