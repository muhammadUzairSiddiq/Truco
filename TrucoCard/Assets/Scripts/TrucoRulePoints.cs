/// <summary>Official Truco point values — single source of truth for gameplay and tests.</summary>
public static class TrucoRulePoints
{
    public static bool IsEnvidoFamily(ChallengeType type) =>
        type == ChallengeType.Envido ||
        type == ChallengeType.RealEnvido ||
        type == ChallengeType.FaltaEnvido;

    /// <summary>Quiero point value contributed by one raise in an Envido chain.</summary>
    public static int EnvidoRaiseQuieroValue(ChallengeType type)
    {
        switch (type)
        {
            case ChallengeType.Envido: return 2;
            case ChallengeType.RealEnvido: return 3;
            default: return 0;
        }
    }

    /// <summary>
    /// No-Quiero points for Envido / Real Envido / Falta Envido chains (Argentine 1v1 acceptance).
    /// First canto of any type → 1. Declining a later raise → previous stake.
    /// Declining Falta after a chain → full accumulated Quiero points on the table.
    /// </summary>
    public static int EnvidoNoQuieroAward(ChallengeType declinedType, int challengePoints, int envidoCantoCount)
    {
        if (!IsEnvidoFamily(declinedType)) return NoQuieroAward(declinedType);
        if (envidoCantoCount <= 1) return 1;
        if (declinedType == ChallengeType.FaltaEnvido)
            return challengePoints > 0 ? challengePoints : 1;
        int raiseValue = EnvidoRaiseQuieroValue(declinedType);
        int previous = challengePoints - raiseValue;
        return previous > 0 ? previous : 1;
    }

    public static int NoQuieroAward(ChallengeType type)
    {
        switch (type)
        {
            case ChallengeType.Truco: return 1;
            case ChallengeType.Retruco: return 2;
            case ChallengeType.Vale4: return 3;
            case ChallengeType.Envido: return 1;
            // First Real Envido alone declined is 1 (not 2). Raised chains use EnvidoNoQuieroAward.
            case ChallengeType.RealEnvido: return 1;
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

    /// <summary>
    /// MAZO after an accepted Truco chain: Truco=2, Retruco=3, Vale4=4.
    /// Unaccepted / unplayed Truco part is always 1. Never reuse Envido/Flor stakes.
    /// </summary>
    public static int MazoAwardForAcceptedTrucoLevel(int acceptedLevel)
    {
        switch (acceptedLevel)
        {
            case 1: return 2;
            case 2: return 3;
            case 3: return 4;
            default: return 1;
        }
    }

    /// <summary>0=none, 1=Truco accepted, 2=Retruco accepted, 3=Vale4 accepted.</summary>
    public static int AcceptedTrucoLevel(bool trucoPlayed, bool retrucoInvoked, bool vale4Invoked)
    {
        if (!trucoPlayed) return 0;
        if (vale4Invoked) return 3;
        if (retrucoInvoked) return 2;
        return 1;
    }
}
