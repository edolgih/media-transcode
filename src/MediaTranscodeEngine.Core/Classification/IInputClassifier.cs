namespace MediaTranscodeEngine.Core.Classification;

public interface IInputClassifier
{
    InputClassification Classify(int? sourceHeight, double? sourceFps);
}
