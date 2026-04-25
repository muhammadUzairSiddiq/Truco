using System;
using System.Collections.Generic;
using UnityEngine;

public class UsersPanel : MonoBehaviour
{
    [SerializeField] private GameObject loadingText;
    [SerializeField] private Transform userBarParent;
    [SerializeField] private GameObject userBar;

    private void OnEnable()
    {
        AdminPlayfabManager.Instance.GetAllPlayers();
    }

    private void OnUsersRetrieved(Dictionary<string, UserStats> userStatsDictionary)
    {
        loadingText.SetActive(false);
        foreach (Transform child in userBarParent)
        {
            Destroy(child.gameObject);
        }
        foreach (var userStat in userStatsDictionary)
        {
            GameObject newUserBar = Instantiate(userBar, userBarParent);
            Userbar userbarComponent = newUserBar.GetComponent<Userbar>();
            userbarComponent.InitializeUserBar(userStat.Value);
        }
    }
    
    
}
