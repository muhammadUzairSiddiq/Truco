#if UNITY_EDITOR
using System;

/// <summary>Editor assembly supplies the handler. Neither this hook nor its handler exists in a player.</summary>
public static class TrucoEditorTournamentHooks
{
    public static Func<bool> Enabled;
    public static Action Open;
    public static bool TryOpen()
    {
        if (Enabled?.Invoke() != true || Open == null) return false;
        Open(); return true;
    }
}
#endif
