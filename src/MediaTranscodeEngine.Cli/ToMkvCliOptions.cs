using System.Globalization;

namespace MediaTranscodeEngine.Cli;

public sealed record ToMkvCliOptions(
    IReadOnlyList<string> Inputs,
    bool Info,
    bool OverlayBg,
    int? Downscale,
    string? DownscaleAlgo,
    string ContentProfile,
    string QualityProfile,
    bool NoAutoSample,
    string AutoSampleMode,
    bool SyncAudio,
    bool ForceVideoEncode,
    int? Cq,
    double? Maxrate,
    double? Bufsize,
    string NvencPreset,
    string? ProfilesYamlPath);

public sealed record ToMkvCliParseResult(
    bool IsValid,
    bool ShowHelp,
    string? ErrorMessage,
    ToMkvCliOptions? Options);

public sealed class ToMkvCliArgumentParser
{
    public ToMkvCliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var inputs = new List<string>();
        var info = false;
        var overlayBg = false;
        var downscale = (int?)null;
        var downscaleAlgo = (string?)null;
        var contentProfile = "film";
        var qualityProfile = "default";
        var noAutoSample = false;
        var autoSampleMode = "accurate";
        var syncAudio = false;
        var forceVideoEncode = false;
        var cq = (int?)null;
        var maxrate = (double?)null;
        var bufsize = (double?)null;
        var nvencPreset = "p6";
        var profilesYamlPath = (string?)null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    return new ToMkvCliParseResult(IsValid: true, ShowHelp: true, ErrorMessage: null, Options: null);

                case "--input":
                    if (!TryReadRequiredValue(args, ref i, out var inputValue))
                    {
                        return Invalid("--input requires a value.");
                    }
                    inputs.Add(inputValue);
                    break;

                case "--info":
                    info = true;
                    break;

                case "--overlay-bg":
                    overlayBg = true;
                    break;

                case "--downscale":
                    if (!TryReadRequiredValue(args, ref i, out var downscaleValue))
                    {
                        return Invalid("--downscale requires a value.");
                    }
                    if (!int.TryParse(downscaleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDownscale) ||
                        (parsedDownscale != 576 && parsedDownscale != 720))
                    {
                        return Invalid("--downscale must be 576 or 720.");
                    }
                    downscale = parsedDownscale;
                    break;

                case "--downscale-algo":
                    if (!TryReadRequiredValue(args, ref i, out var algoValue))
                    {
                        return Invalid("--downscale-algo requires a value.");
                    }
                    if (!IsOneOf(algoValue, "bicubic", "lanczos", "bilinear"))
                    {
                        return Invalid("--downscale-algo must be bicubic|lanczos|bilinear.");
                    }
                    downscaleAlgo = algoValue;
                    break;

                case "--content-profile":
                    if (!TryReadRequiredValue(args, ref i, out var contentValue))
                    {
                        return Invalid("--content-profile requires a value.");
                    }
                    if (!IsOneOf(contentValue, "anime", "mult", "film"))
                    {
                        return Invalid("--content-profile must be anime|mult|film.");
                    }
                    contentProfile = contentValue;
                    break;

                case "--quality-profile":
                    if (!TryReadRequiredValue(args, ref i, out var qualityValue))
                    {
                        return Invalid("--quality-profile requires a value.");
                    }
                    if (!IsOneOf(qualityValue, "high", "default", "low"))
                    {
                        return Invalid("--quality-profile must be high|default|low.");
                    }
                    qualityProfile = qualityValue;
                    break;

                case "--no-auto-sample":
                    noAutoSample = true;
                    break;

                case "--auto-sample-mode":
                    if (!TryReadRequiredValue(args, ref i, out var modeValue))
                    {
                        return Invalid("--auto-sample-mode requires a value.");
                    }
                    if (!IsOneOf(modeValue, "accurate", "fast", "hybrid"))
                    {
                        return Invalid("--auto-sample-mode must be accurate|fast|hybrid.");
                    }
                    autoSampleMode = modeValue;
                    break;

                case "--sync-audio":
                    syncAudio = true;
                    break;

                case "--force-video-encode":
                    forceVideoEncode = true;
                    break;

                case "--cq":
                    if (!TryReadRequiredValue(args, ref i, out var cqValue))
                    {
                        return Invalid("--cq requires a value.");
                    }
                    if (!int.TryParse(cqValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCq) ||
                        parsedCq < 0 ||
                        parsedCq > 51)
                    {
                        return Invalid("--cq must be in range 0..51.");
                    }
                    cq = parsedCq;
                    break;

                case "--maxrate":
                    if (!TryReadRequiredValue(args, ref i, out var maxrateValue))
                    {
                        return Invalid("--maxrate requires a value.");
                    }
                    if (!double.TryParse(maxrateValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMaxrate) ||
                        parsedMaxrate <= 0)
                    {
                        return Invalid("--maxrate must be a positive number.");
                    }
                    maxrate = parsedMaxrate;
                    break;

                case "--bufsize":
                    if (!TryReadRequiredValue(args, ref i, out var bufsizeValue))
                    {
                        return Invalid("--bufsize requires a value.");
                    }
                    if (!double.TryParse(bufsizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBufsize) ||
                        parsedBufsize <= 0)
                    {
                        return Invalid("--bufsize must be a positive number.");
                    }
                    bufsize = parsedBufsize;
                    break;

                case "--nvenc-preset":
                    if (!TryReadRequiredValue(args, ref i, out var presetValue))
                    {
                        return Invalid("--nvenc-preset requires a value.");
                    }
                    if (!IsOneOf(presetValue, "p1", "p2", "p3", "p4", "p5", "p6", "p7"))
                    {
                        return Invalid("--nvenc-preset must be p1..p7.");
                    }
                    nvencPreset = presetValue;
                    break;

                case "--profiles-yaml":
                    if (!TryReadRequiredValue(args, ref i, out var yamlPathValue))
                    {
                        return Invalid("--profiles-yaml requires a value.");
                    }
                    profilesYamlPath = yamlPathValue;
                    break;

                default:
                    return Invalid($"Unknown option: {arg}");
            }
        }

        return new ToMkvCliParseResult(
            IsValid: true,
            ShowHelp: false,
            ErrorMessage: null,
            Options: new ToMkvCliOptions(
                Inputs: inputs,
                Info: info,
                OverlayBg: overlayBg,
                Downscale: downscale,
                DownscaleAlgo: downscaleAlgo,
                ContentProfile: contentProfile,
                QualityProfile: qualityProfile,
                NoAutoSample: noAutoSample,
                AutoSampleMode: autoSampleMode,
                SyncAudio: syncAudio,
                ForceVideoEncode: forceVideoEncode,
                Cq: cq,
                Maxrate: maxrate,
                Bufsize: bufsize,
                NvencPreset: nvencPreset,
                ProfilesYamlPath: profilesYamlPath));
    }

    private static bool TryReadRequiredValue(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    private static bool IsOneOf(string value, params string[] options)
    {
        return options.Any(option => option.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private static ToMkvCliParseResult Invalid(string message)
    {
        return new ToMkvCliParseResult(
            IsValid: false,
            ShowHelp: false,
            ErrorMessage: message,
            Options: null);
    }
}
