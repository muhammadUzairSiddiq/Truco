/// <summary>Official Truco point values — single source of truth for gameplay and tests.</summary>
public static class TrucoRulePoints
{
    public static int NoQuieroAward(ChallengeType type)
    {
        switch (type)
        {
            case ChallengeType.Truco: return 1;
            case ChallengeType.Retruco: return 2;
            case ChallengeType.Vale4: return 3;
            case ChallengeType.Envido: return 1;
            case ChallengeType.RealEnvido: return 2;
            case ChallengeType.FaltaEnvido: return 1;
            case ChallengeType.Flor: return 3;
            case ChallengeType.ContraFlor: return 4;
            default: return 1;
        }
    }

    public static bool NoQuieroEndsHand(ChallengeType type) =>
        type == ChallengeType.Truco ||
        type == ChallengeType.Retruco ||
        type == ChallengeType.Vale4;
}
