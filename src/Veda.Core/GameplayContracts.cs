namespace Veda.Core;

public enum SceneEnvironment { Unknown, Space, Planet, Station, City, Interior, Eva }
public enum PlayerActivity { Unknown, Idle, Exploring, Traveling, Working, Approaching, Fighting, Escaping }

public sealed record GameplayObservation(DateTimeOffset CapturedAt, SceneEnvironment Environment,
    PlayerActivity Activity, float Threat, float Confidence, IReadOnlyList<string> Evidence);

/// <summary>Future input contract. No capture or model is implemented in Prototype 0.1.</summary>
public interface IGameplayInterpreter
{
    ValueTask<GameplayObservation> AnalyzeAsync(GameFrame frame, CancellationToken cancellationToken);
}

/// <summary>In-memory BGRA frame; the caller owns the memory and its lifetime.</summary>
public readonly record struct GameFrame(DateTimeOffset CapturedAt, int Width, int Height,
    int RowStride, ReadOnlyMemory<byte> BgraPixels);

public interface IGameFrameSource : IAsyncDisposable
{
    IAsyncEnumerable<GameFrame> Frames(CancellationToken cancellationToken);
}
