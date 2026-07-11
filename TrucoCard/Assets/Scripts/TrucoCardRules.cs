/// <summary>Shared Truco card rank / envido helpers — used by gameplay and edit-mode tests.</summary>
public static class TrucoCardRules
{
    public static int TrucoRank(int rank, CardSuit suit)
    {
        if (rank == 1 && suit == CardSuit.Swords) return 14;
        if (rank == 1 && suit == CardSuit.Clubs) return 13;
        if (rank == 7 && suit == CardSuit.Swords) return 12;
        if (rank == 7 && suit == CardSuit.Coins) return 11;
        if (rank == 3) return 10;
        if (rank == 2) return 9;
        if (rank == 1) return 8;
        if (rank == 12) return 7;
        if (rank == 11) return 6;
        if (rank == 10) return 5;
        if (rank == 7) return 4;
        if (rank == 6) return 3;
        if (rank == 5) return 2;
        if (rank == 4) return 1;
        return 0;
    }

    public static int EnvidoValue(int rank) => rank >= 10 ? 0 : rank;

    /// <summary>Two-card envido total (+20 for same suit pair).</summary>
    public static int EnvidoPairTotal(int rankA, int rankB) =>
        EnvidoValue(rankA) + EnvidoValue(rankB) + 20;
}
