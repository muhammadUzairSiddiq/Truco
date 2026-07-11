using Photon.Pun;
using UnityEngine;

public class MatchMakingPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject playerAImage;
    [SerializeField] private GameObject playerBImage;
    [SerializeField] private GameObject lookingForMatch;
    [SerializeField] private GameObject matchFoundText;

    public void ResetState()
    {
        CancelInvoke(nameof(LoadGame));
        if (panel != null) panel.SetActive(false);
        if (playerAImage != null) playerAImage.SetActive(false);
        if (playerBImage != null) playerBImage.SetActive(false);
        if (lookingForMatch != null) lookingForMatch.SetActive(false);
        if (matchFoundText != null) matchFoundText.SetActive(false);
    }

    public void Initialize()
    {
        CancelInvoke(nameof(LoadGame));
        if (panel != null) panel.SetActive(true);
        if (playerAImage != null) playerAImage.SetActive(true);
        if (playerBImage != null) playerBImage.SetActive(false);
        if (lookingForMatch != null) lookingForMatch.SetActive(true);
        if (matchFoundText != null) matchFoundText.SetActive(false);
    }

    public void MatchFound()
    {
        CancelInvoke(nameof(LoadGame));
        if (panel != null) panel.SetActive(true);
        if (playerAImage != null) playerAImage.SetActive(true);
        if (playerBImage != null) playerBImage.SetActive(true);
        if (lookingForMatch != null) lookingForMatch.SetActive(false);
        if (matchFoundText != null) matchFoundText.SetActive(true);
        Invoke(nameof(LoadGame), 3f);
    }

    void LoadGame() => TrucoOneVsOneGameplayLaunch.LoadFromCurrentRoom();
}
