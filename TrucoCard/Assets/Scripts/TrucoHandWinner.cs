using System.Collections.Generic;

/// <summary>Truco hand (3 tricks) winner rules — shared by all clients from trick result history.</summary>
public static class TrucoHandWinner
{
    /// <param name="trickResults">1 = master/player1 trick win, 2 = guest/player2, 0 = parda.</param>
    /// <param name="manoPlayer">1 or 2 — hand leader (mano) for third-trick tie-break.</param>
    /// <returns>1 or 2 when the hand is decided; null if a third trick may still be needed.</returns>
    public static int? Evaluate(IReadOnlyList<int> trickResults, int manoPlayer)
    {
        if (trickResults == null || trickResults.Count < 2)
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Rules,
                "HandWinner: need more tricks count=" + (trickResults?.Count ?? 0));
            return null;
        }

        int p1 = 0, p2 = 0;
        for (int i = 0; i < trickResults.Count; i++)
        {
            if (trickResults[i] == 1) p1++;
            else if (trickResults[i] == 2) p2++;
        }

        if (p1 >= 2)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P1 (2 tricks)", "mano=" + manoPlayer);
            return 1;
        }
        if (p2 >= 2)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P2 (2 tricks)", "mano=" + manoPlayer);
            return 2;
        }

        int t1 = trickResults[0];
        int t2 = trickResults[1];

        // P1 wins trick 1 and (wins or ties trick 2) → hand over, no third trick.
        if (t1 == 1 && (t2 == 0 || t2 == 1))
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P1 (t1 win + t2 win/parda)");
            return 1;
        }
        // P2 wins trick 1 and (wins or ties trick 2) → hand over.
        if (t1 == 2 && (t2 == 0 || t2 == 2))
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P2 (t1 win + t2 win/parda)");
            return 2;
        }
        // First trick parda: winner of trick 2 wins the hand immediately.
        if (t1 == 0 && t2 == 1)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P1 (t1 parda, t2 win)");
            return 1;
        }
        if (t1 == 0 && t2 == 2)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P2 (t1 parda, t2 win)");
            return 2;
        }

        if (trickResults.Count < 3)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner need 3rd trick", "t1=" + t1 + " t2=" + t2);
            return null;
        }

        int t3 = trickResults[2];
        if (t3 == 1)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P1 (t3)");
            return 1;
        }
        if (t3 == 2)
        {
            TrucoRulesScenarioLog.Ok("TrickHandWinner → P2 (t3)");
            return 2;
        }

        // Third trick parda → whoever won the first non-parda trick; if trick 1 had a winner, that player.
        if (t1 == 1) return 1;
        if (t1 == 2) return 2;
        return manoPlayer;
    }

    /// <summary>Mano is master (player 1) on odd roundNumber after increment in TurnManager.Awake.</summary>
    public static int GetManoPlayerNumber(int roundNumber) => roundNumber % 2 == 0 ? 2 : 1;
}
