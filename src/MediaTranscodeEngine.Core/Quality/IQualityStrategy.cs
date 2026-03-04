namespace MediaTranscodeEngine.Core.Quality;

public interface IQualityStrategy
{
    QualitySettings Resolve(QualitySelectionContext context);
}
