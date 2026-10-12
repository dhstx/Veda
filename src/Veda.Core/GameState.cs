namespace Veda.Core;

public enum GameState { CalmSpace, Travel, Uncertain, Combat, Escape, Relief }

public static class GameStateLabels
{
    public static string Display(GameState state) => state switch
    {
        GameState.CalmSpace => "Calm space",
        GameState.Travel => "Travel",
        GameState.Uncertain => "Uncertain",
        GameState.Combat => "Combat",
        GameState.Escape => "Escape",
        GameState.Relief => "Relief",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
