/// <summary>Set before connecting as admin spectator. Cleared on leave.</summary>
public static class SpectatorContext
{
    public static bool IsSpectator { get; set; }
    public static string PendingPhotonRoomName { get; set; }
    public static string PendingMatchId { get; set; }

    public static void BeginSpectateSession(string photonRoomName, string matchId = null)
    {
        IsSpectator = true;
        PendingPhotonRoomName = photonRoomName;
        PendingMatchId = matchId;
    }

    public static void Clear()
    {
        IsSpectator = false;
        PendingPhotonRoomName = null;
        PendingMatchId = null;
    }
}
