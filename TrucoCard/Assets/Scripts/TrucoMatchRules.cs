/// <summary>Deterministic match-level rules shared by gameplay, lobby, and tests.</summary>
public static class TrucoMatchRules
{
    public const int DefaultTargetScore = 15;
    public const int ExtendedTargetScore = 30;

    public static int NormalizeTargetScore(int value) =>
        value == ExtendedTargetScore ? ExtendedTargetScore : DefaultTargetScore;

    public static bool HasReachedTarget(int score, int targetScore) =>
        score >= NormalizeTargetScore(targetScore);

    public static int ClampScoreToTarget(int score, int targetScore) =>
        UnityEngine.Mathf.Clamp(score, 0, NormalizeTargetScore(targetScore));

    public static bool ShouldRevealScoringCards(bool handEnded, bool folded) =>
        handEnded || folded;

    public static int ResolveEnvidoWinnerActor(int localScore, int rivalScore,
        int localActor, int rivalActor, int manoActor)
    {
        if (localScore > rivalScore) return localActor;
        if (rivalScore > localScore) return rivalActor;
        return manoActor;
    }
}
