using Photon.Pun;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PlayerScoreHandler : MonoBehaviourPun
{
    [SerializeField] private TMP_Text coinsText;

    private int currentScore = 0;

    public int GetCurrentScore() => currentScore;

    /// <summary>Score changes are synced by GameManager.RPC_ApplyMatchScores / RPC_NetworkAwardPoints.</summary>
    public void UpdateScore(int score, bool _silent = true)
    {
        currentScore += score;
        currentScore = TrucoMatchRules.ClampScoreToTarget(
            currentScore, OneVsOneMatchSession.TargetScore);
        if (coinsText != null)
            coinsText.text = currentScore.ToString();
    }

    public void SetScore(int _score)
    {
        currentScore = TrucoMatchRules.ClampScoreToTarget(
            _score, OneVsOneMatchSession.TargetScore);
        if (coinsText != null)
            coinsText.text = currentScore.ToString();
    }
}
