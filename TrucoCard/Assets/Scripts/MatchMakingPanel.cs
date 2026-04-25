using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchMakingPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject playerAImage;
    [SerializeField] private GameObject playerBImage;
    [SerializeField] private GameObject lookingForMatch;
    [SerializeField] private GameObject matchFoundText;

    public void Initialize()
    {
        panel.SetActive(true);
        playerAImage.SetActive(true);
        playerBImage.SetActive(false);
        lookingForMatch.SetActive(true);
        matchFoundText.SetActive(false);
    }
    
    public void MatchFound()
    {
        playerBImage.SetActive(true);
        lookingForMatch.SetActive(false);
        matchFoundText.SetActive(true);
        
        Invoke(nameof(LoadGame),3);
        
    }
    
    private void LoadGame()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            //ApiController.GetSessionUser.Data.stats.matchesPlayed++;
            //DataHandler.Instance.SaveData();
            //PlayfabDataManager.Instance.SetUserData();
            PhotonNetwork.LoadLevel("Gameplay");
        }
    }
    
}
