using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseDatabaseManager : MonoBehaviour
{
    public static FirebaseDatabaseManager Instance;
    
    
    private Action<Dictionary<string,UserStats>> onUsersRetrieved;
    private Action<UserStats> onSingleUserRetrieved;
    private DatabaseReference reference;
    void Awake()
    {
        Instance = this;
        reference = FirebaseDatabase.DefaultInstance.RootReference;
    }
    
    // Update is called once per frame
    void Update()
    {
        
    }

    // Get a list of all users on database for Admin Panel
    public void GetListOfUsers(Action<Dictionary<string,UserStats>> _onComplete)
    {
        onUsersRetrieved = _onComplete;
        Dictionary<string, UserStats> userStatsDictionary = new Dictionary<string, UserStats>();
        reference.Root.Child("users").GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error retrieving users: " + task.Exception);
                return;
            }
            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                foreach (var child in snapshot.Children)
                {
                    string userId = child.Key;
                    Debug.LogWarning(child.GetValue(false));
                    UserStats userStats = JsonUtility.FromJson<UserStats>((string)child.GetValue(false));
                    userStatsDictionary.Add(userId, userStats);
                }
                onUsersRetrieved.Invoke(userStatsDictionary);
                // onUsersRetrieved = null;
            }
        });
    }

   // Retrieve data of a single user by userId 
    public void RetrieveData(string userId,Action<UserStats> _onComplete)
    {
        onSingleUserRetrieved = _onComplete;
        reference.Root.Child("users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error retrieving users: " + task.Exception);
                return;
            }
            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                UserStats userStats = JsonUtility.FromJson<UserStats>((string)snapshot.GetValue(false));
                onSingleUserRetrieved.Invoke(userStats);
            }
        });
    }
    
    // Set data of a single user by userId
    public void SetChildValue(string userId,UserStats data)
    {
        UnityEngine.Debug.LogWarning((new System.Diagnostics.StackTrace()).GetFrame(1).GetMethod().Name);
        Debug.LogWarning("Function called to set user data for user ID: " + userId);
        string jsonData = JsonUtility.ToJson(data);
        reference.Child("users").Child(userId).SetValueAsync(jsonData).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error setting user data: " + task.Exception);
                return;
            }
            Debug.Log("User data set successfully for user ID: " + userId);
        });
    }
}
