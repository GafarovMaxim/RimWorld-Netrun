using Verse;

namespace Netrun;

/// <summary>
/// The game component of type <typeparamref name="T"/>, found once per game instead of a search through the game's
/// components on every call (docs/PERF_AUDIT_4.md, item 28). A new or loaded game finds it again.
/// </summary>
public static class GameComp<T> where T : GameComponent
{
    private static Game _game;
    private static T _instance;

    public static T Get()
    {
        Game game = Current.Game;
        if (game != _game || _instance == null && game != null)
        {
            _game = game;
            _instance = game?.GetComponent<T>();
        }
        return _instance;
    }
}
