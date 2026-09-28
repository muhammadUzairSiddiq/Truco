using UnityEngine;

/// <summary>Client-side idempotent tournament finalize so double-win / walkover retries do not POST twice.</summary>
public static class TournamentFinalizeGuard
{
    public static bool AlreadyDone(string tournamentId, string subject, string winnerId)
    {
        return PlayerPrefs.GetInt(Key(tournamentId, subject, winnerId), 0) == 1;
    }

    public static void MarkDone(string tournamentId, string subject, string winnerId)
    {
        PlayerPrefs.SetInt(Key(tournamentId, subject, winnerId), 1);
        PlayerPrefs.Save();
    }

    static string Key(string tournamentId, string subject, string winnerId)
    {
        return "truco_tf_" + (tournamentId ?? "") + "_" + (subject ?? "") + "_" + (winnerId ?? "");
    }
}
