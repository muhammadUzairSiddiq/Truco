/// <summary>Resets persisted match score / mano index when a brand-new 1v1 match begins.</summary>
public static class TrucoMatchProgress
{
    static bool _freshMatchScores;

    /// <summary>True once after <see cref="ResetForNewMatch"/> — consumed by <see cref="GameManager"/> on load.</summary>
    public static bool ConsumeFreshMatchScores()
    {
        if (!_freshMatchScores) return false;
        _freshMatchScores = false;
        return true;
    }

    public static void ResetForNewMatch()
    {
        _freshMatchScores = true;
        if (DataHandler.Instance != null)
        {
            DataHandler.Instance.points = 0;
            DataHandler.Instance.opponentPoints = 0;
            DataHandler.Instance.roundNumber = 0;
        }
    }

    /// <summary>Full wipe after match ends or abandoned lobby — next game must be 0-0.</summary>
    public static void ClearAllMatchMemory()
    {
        _freshMatchScores = true;
        if (DataHandler.Instance != null)
        {
            DataHandler.Instance.points = 0;
            DataHandler.Instance.opponentPoints = 0;
            DataHandler.Instance.roundNumber = 0;
        }
        OneVsOneMatchSession.ClearSavedRoomPersistence();
        TrucoActiveHostMatchStore.Clear();
    }
}
