using Photon.Pun;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PlayerScoreHandler : MonoBehaviourPun
{
    [SerializeField] private TMP_Text coinsText;


    private int currentScore = 0;

    // This function returns the current score of the player
    public int GetCurrentScore()
    {
        return currentScore;
    }
    
    // This function Updates the score of the player and sends the update to other players
    public void UpdateScore(int score, bool _silent = false)
    {
        currentScore += score;
        currentScore = Mathf.Min(currentScore, 15);
        coinsText.text = currentScore.ToString();
        if (!_silent)
        {
            CancelInvoke(nameof(SendScoreUpdate));
            InvokeRepeating(nameof(SendScoreUpdate), 0.1f, 0.1f);
        }
    }

    public void SetScore(int _score)
    {
        currentScore = _score;
        currentScore = Mathf.Min(currentScore, 15);
        coinsText.text = currentScore.ToString();
    }
    
    private void SendScoreUpdate()
    {
        if (photonView == null || !PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RPC_UpdateScore), RpcTarget.OthersBuffered, currentScore);
    }

    [PunRPC]
    private void RPC_UpdateScore(int _score)
    {
        GameManager.Instance.ShowOtherPlayerScore(_score);
    }
    
}
