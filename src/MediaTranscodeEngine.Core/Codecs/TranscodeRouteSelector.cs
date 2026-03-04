using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Codecs;

public sealed class TranscodeRouteSelector
{
    private readonly IReadOnlyList<ITranscodeRoute> _routes;

    public TranscodeRouteSelector(IEnumerable<ITranscodeRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes = routes.ToArray();
    }

    public ITranscodeRoute Select(TranscodeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var route = _routes.FirstOrDefault(candidate => candidate.CanHandle(request));
        if (route is null)
        {
            throw new InvalidOperationException(
                $"No transcode route was found for compute '{request.ComputeMode}' and container '{request.TargetContainer}'.");
        }

        return route;
    }
}
