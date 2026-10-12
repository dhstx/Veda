using System.Collections.Frozen;

namespace Veda.Core;

public enum TransitionBoundary { Immediate, NextBeat, NextBar }

public sealed record MixPlan(GameState State, FrozenDictionary<string, float> Gains,
    TransitionBoundary Boundary, double FadeBeats);

/// <summary>Turns a situation into an arrangement; it does not infer gameplay.</summary>
public sealed class MusicDirector(LoadedSoundtrack soundtrack)
{
    public MixPlan Plan(GameState state, float drama = 0.5f, float responsiveness = 0.5f)
    {
        if (!Enum.IsDefined(state) || !float.IsFinite(drama) || !float.IsFinite(responsiveness))
            throw new ArgumentException("Invalid state or control value.");
        drama = Math.Clamp(drama, 0, 1); responsiveness = Math.Clamp(responsiveness, 0, 1);
        var (boundary, beats) = state switch
        {
            GameState.CalmSpace => (TransitionBoundary.NextBar, 8d),
            GameState.Travel => (TransitionBoundary.NextBar, 4d),
            GameState.Uncertain => (TransitionBoundary.NextBeat, 2d),
            GameState.Combat => (TransitionBoundary.Immediate, 0.5d),
            GameState.Escape => (TransitionBoundary.NextBeat, 2d),
            GameState.Relief => (TransitionBoundary.NextBar, 8d),
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
        var baseLayer = soundtrack.Layers[0].Id;
        var gains = soundtrack.StateGains[state].ToFrozenDictionary(x => x.Key,
            x => x.Key == baseLayer ? x.Value : Math.Clamp(x.Value * (0.5f + drama), 0, 1), StringComparer.Ordinal);
        return new MixPlan(state, gains, boundary, beats * (1.6d - 1.05d * responsiveness));
    }
}
