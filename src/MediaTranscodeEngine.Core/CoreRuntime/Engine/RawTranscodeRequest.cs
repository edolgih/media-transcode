namespace MediaTranscodeEngine.Core.Engine;

public sealed record RawTranscodeRequest(
    string InputPath,
    string? Scenario = null,
    string TargetContainer = RequestContracts.General.DefaultContainer,
    string EncoderBackend = RequestContracts.General.DefaultEncoderBackend,
    string VideoPreset = RequestContracts.General.DefaultVideoPreset,
    string TargetVideoCodec = RequestContracts.General.DefaultTargetVideoCodec,
    bool Info = false,
    bool OverlayBg = false,
    int? Downscale = null,
    string? DownscaleAlgo = null,
    string ContentProfile = RequestContracts.Transcode.DefaultContentProfile,
    string QualityProfile = RequestContracts.Transcode.DefaultQualityProfile,
    bool NoAutoSample = false,
    string AutoSampleMode = RequestContracts.Transcode.DefaultAutoSampleMode,
    bool SyncAudio = false,
    int? Cq = null,
    double? Maxrate = null,
    double? Bufsize = null,
    bool ForceVideoEncode = false,
    bool KeepFps = false,
    bool UseAq = false,
    int AqStrength = RequestContracts.General.DefaultAqStrength,
    bool Denoise = false,
    bool FixTimestamps = false,
    bool KeepSource = false)
{
    public TranscodeRequest ToDomain()
    {
        return TranscodeRequest.Create(
            InputPath: InputPath,
            TargetContainer: TargetContainer,
            EncoderBackend: EncoderBackend,
            VideoPreset: VideoPreset,
            TargetVideoCodec: TargetVideoCodec,
            Info: Info,
            OverlayBg: OverlayBg,
            Downscale: Downscale,
            DownscaleAlgo: DownscaleAlgo,
            ContentProfile: ContentProfile,
            QualityProfile: QualityProfile,
            NoAutoSample: NoAutoSample,
            AutoSampleMode: AutoSampleMode,
            SyncAudio: SyncAudio,
            Cq: Cq,
            Maxrate: Maxrate,
            Bufsize: Bufsize,
            ForceVideoEncode: ForceVideoEncode,
            KeepFps: KeepFps,
            UseAq: UseAq,
            AqStrength: AqStrength,
            Denoise: Denoise,
            FixTimestamps: FixTimestamps,
            KeepSource: KeepSource);
    }
}
