using MediaTranscodeEngine.Core.Quality;

namespace MediaTranscodeEngine.Core.Sampling;

public interface IAutoSamplingStrategy
{
    QualitySettings Resolve(AutoSamplingContext context);
}
