using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine.SceneManagement;

public class FirebaseManager : MonoBehaviour
{
    Firebase.Auth.FirebaseAuth auth;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
    }

    public void SignInAnonymously()
    {
        //    if (DataHandler.Instance.IngameData.loggedIn)
        //    {
        //        Debug.LogWarning("Loading MainMenu, already logged in.");
        //        SceneManager.LoadScene("MainMenu");
        //    }
        //    else
        //    {
        //        Debug.LogWarning("Not Logged In, attempting to sign in anonymously.");
        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled)
            {
                Debug.LogError("SignInAnonymouslyAsync was canceled.");
                return;
            }
            if (task.IsFaulted)
            {
                Debug.LogError("SignInAnonymouslyAsync encountered an error: " + task.Exception);
                return;
            }
            Firebase.Auth.AuthResult result = task.Result;
            //DataHandler.Instance.IngameData.loggedIn = true;
            //DataHandler.Instance.IngameData.userId = result.User.UserId;
            //DataHandler.Instance.IngameData.userStats = new UserStats(result.User.UserId);
            //DataHandler.Instance.SaveData();
            //FirebaseDatabaseManager.Instance.
            //    SetChildValue(DataHandler.Instance.IngameData.userId,
            //DataHandler.Instance.IngameData.userStats);
            SceneManager.LoadScene("MainMenu");
        });
    }
}
