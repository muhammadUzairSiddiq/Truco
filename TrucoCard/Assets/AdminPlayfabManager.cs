using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class AdminPlayfabManager : MonoBehaviour
{
    public static AdminPlayfabManager Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    
    public void GetAllPlayers(string token = null)
    {
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "getAllPlayers",
            FunctionParameter = new {
                pageSize = 10,
                continuationToken = token
            },
            GeneratePlayStreamEvent = false
        };

        PlayFabClientAPI.ExecuteCloudScript(request, OnCloudScriptSuccess, OnError);
    }

    void OnCloudScriptSuccess(ExecuteCloudScriptResult result)
    {
        if (result.Error != null)
        {
            Debug.LogError("Cloud Script Error: " + result.Error.Message);
        }

        Debug.Log(result);
        var json = result.FunctionResult.ToString();
        Debug.Log("Players JSON: " + json);
    }

    void OnError(PlayFabError error)
    {
        Debug.LogError("Error: " + error.GenerateErrorReport());
    }
}
