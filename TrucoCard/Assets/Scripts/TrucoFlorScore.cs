using System.Collections.Generic;

/// <summary>
/// Flor points for three same-suit cards: 20 + every card's Envido value (10/11/12 = 0).
/// 7-5-2 → 34, 6-3-4 → 33, 12-10-4 → 24, 10-11-1 → 21. Three face cards → 30 (house rule).
/// </summary>
public static class TrucoFlorScore
{
    public const int Base = 20;
    public const int ThreeFaceCards = 30;

    public static int CardValue(int rank) => rank >= 10 ? 0 : rank;

    public static int Compute(IEnumerable<int> ranks)
    {
        if (ranks == null) return 0;
        int sum = 0;
        int count = 0;
        int faces = 0;
        foreach (int rank in ranks)
        {
            count++;
            int v = CardValue(rank);
            if (v == 0) faces++;
            sum += v;
        }
        if (count == 0) return 0;
        if (count == 3 && faces == 3) return ThreeFaceCards;
        return Base + sum;
    }
}
