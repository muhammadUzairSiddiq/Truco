using Photon.Pun;

/// <summary>
/// Green-tick scenario logs for gameplay rules — Console + truco_debug.log + Android logcat.
/// Format: ✓ [RULE] Scenario | detail | score me=… opp=… …
/// Opponent-originated events use ✓ OPP. Failures use ✗.
/// </summary>
public static class TrucoRulesScenarioLog
{
    public static string Snapshot()
    {
        int me = 0, opp = 0, round = 0;
        if (DataHandler.Instance != null)
        {
            me = DataHandler.Instance.points;
            opp = DataHandler.Instance.opponentPoints;
            round = DataHandler.Instance.roundNumber;
        }
        var gm = GameManager.Instance;
        string last = gm != null ? gm.lastChallengeType.ToString() : "?";
        int ch = gm != null ? gm.challengePoints : 0;
        int mz = gm != null ? gm.mazoPoints : 0;
        bool myTurn = gm != null && gm.IsMyTurn();
        bool handDone = gm != null && gm.HandResolved;
        bool ended = gm != null && gm._gameEnded;
        int local = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        bool master = PhotonNetwork.IsMasterClient;
        return "score me=" + me + " opp=" + opp
               + " round=" + round
               + " last=" + last
               + " challengePts=" + ch
               + " mazoPts=" + mz
               + " myTurn=" + myTurn
               + " handResolved=" + handDone
               + " gameEnded=" + ended
               + " actor=" + local
               + " master=" + master;
    }

    public static void Ok(string scenario, string detail = null)
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.Rules,
            "✓ " + scenario + FormatDetail(detail) + " | " + Snapshot());
    }

    /// <summary>Opponent (or other actor) caused this — still a success path for rules sync.</summary>
    public static void Opp(string scenario, string detail = null)
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.Rules,
            "✓ OPP " + scenario + FormatDetail(detail) + " | " + Snapshot());
    }

    public static void Fail(string scenario, string detail = null)
    {
        TrucoDebugLog.Error(TrucoDebugLog.Category.Rules,
            "✗ " + scenario + FormatDetail(detail) + " | " + Snapshot());
    }

    public static void Backend(string scenario, string detail = null)
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.Api,
            "✓ BACKEND " + scenario + FormatDetail(detail) + " | " + Snapshot());
    }

    public static void BackendFail(string scenario, string detail = null)
    {
        TrucoDebugLog.Error(TrucoDebugLog.Category.Api,
            "✗ BACKEND " + scenario + FormatDetail(detail) + " | " + Snapshot());
    }

    static string FormatDetail(string detail) =>
        string.IsNullOrEmpty(detail) ? "" : " | " + detail;
}
