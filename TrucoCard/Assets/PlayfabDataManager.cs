using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayfabDataManager : MonoBehaviour
{
    
    public static PlayfabDataManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    //public void SetUserData()
    //{
    //    Dictionary<string, string> userData = new Dictionary<string, string>
    //    {
    //        { "coins", DataHandler.Instance.IngameData.wallet.balance.ToString() },
    //        { "wins", DataHandler.Instance.IngameData.stats.wins.ToString() },
    //        { "loses", DataHandler.Instance.IngameData.stats.losses.ToString() },
    //        { "played", DataHandler.Instance.IngameData.stats.matchesPlayed.ToString() }
    //    };
        
    //    var request = new UpdateUserDataRequest
    //    {
    //        Data = userData
    //    };

    //    PlayFabClientAPI.UpdateUserData(request, result => {
    //        Debug.Log("User data updated successfully.");
    //    }, OnError);
    //}
    
    public void SetInitialData()
    {
        //ApiController.GetSessionUser.Data.wallet.balance = 100;
        //ApiController.GetSessionUser.Data.role = "player";

        //DataHandler.Instance.SaveData();

        //Dictionary<string, string> initialData = new Dictionary<string, string>
        //{
        //    { "coins", "100" },
        //    { "wins", "0" },
        //    { "loses", "0" },
        //    { "played", "0"},
        //    { "role" ,"player"}
        //};
        //var request = new UpdateUserDataRequest
        //{
        //    Data = initialData
        //};

        //PlayFabClientAPI.UpdateUserData(request, result => {
        //    Debug.Log("Data saved.");
        //}, OnError);
    }
    
    void OnError(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
    }
    
    // Add methods to interact with PlayFab here, such as login, data retrieval, etc.
    
    public void GetUserData()
    {
        // Implement PlayFab API call to retrieve user data
        // Call onComplete with the retrieved UserDataResult
        //PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result => {
            Debug.Log("User data retrieved successfully.");

        //string coins = result.Data["coins"].Value;
        //string wins = result.Data["wins"].Value;
        //string loses = result.Data["loses"].Value;
        //string totalPlayed = result.Data["played"].Value;
        //DataHandler.Instance.IngameData.stats = new StatsDto
        //{
        //    wins = ApiController.GetSessionUser.Data.stats.wins,
        //    losses = ApiController.GetSessionUser.Data.stats.losses,
        //    matchesPlayed = ApiController.GetSessionUser.Data.stats.matchesPlayed
        //};
            
        //DataHandler.Instance.IngameData.wallet = new WalletDto
        //{
        //    balance = ApiController.GetSessionUser.Data.wallet.balance
        //};

        //}, OnError);
    }
    
    public void CheckIfAdmin(string userId, System.Action<bool> onComplete)
    {
        // Implement PlayFab API call to retrieve user data
        // Call onComplete with the retrieved UserStats
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result => {
            
            string roleResult = result.Data["role"].Value;
            
            if (roleResult == "admin")
            {
                Debug.Log("User is admin");
                onComplete.Invoke(true);
                // Show admin dashboard
            }
            else
            {
                Debug.Log("User is not admin");
                onComplete.Invoke(false);
            }
        }, OnError);
    }
    
    
    
}
