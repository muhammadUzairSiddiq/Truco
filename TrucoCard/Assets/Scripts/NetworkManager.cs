using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private int waitTime;
    [SerializeField] private MatchMakingPanel matchMakingPanel;
    
    
    private int _maximumPlayers = 2;
    private bool _clicked = false;

    public void ConnectToMaster()
    {
        AppManager.Instance.DisplayLoadingUI("Connecting to Server");

        DataHandler.Instance.points = 0;
        DataHandler.Instance.roundNumber = 0;
        _clicked = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    
    public void DisableClicked()
    {
        _clicked = false;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (_clicked)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }
    
    public override void OnConnectedToMaster()
    {
        if (_clicked)
        {
            StartCoroutine(TryToFindMatch());
        }
    }

    IEnumerator TryToFindMatch()
    {

        matchMakingPanel.Initialize();
        yield return new WaitForSeconds(waitTime);

        AppManager.Instance.HideLoadingUI();


        PhotonNetwork.JoinRandomRoom();
        
    }
    
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (_clicked)
            CreateRoom();
    }

    private void CreateRoom()
    {
        if (_clicked)
        {
            Debug.Log("Creating a new room...");
            RoomOptions roomOptions = new RoomOptions
            {
                MaxPlayers = (byte)_maximumPlayers,
                IsVisible = true,
                IsOpen = true
            };

            PhotonNetwork.CreateRoom(null, roomOptions);
        }
    }

    public override void OnJoinedRoom()
    {
        if (_clicked)
        {

            StopCoroutine(TryToFindMatch());
            if (PhotonNetwork.CurrentRoom.PlayerCount >= _maximumPlayers)
            {
                matchMakingPanel.MatchFound();
            }
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (_clicked)
        {
            if (PhotonNetwork.CurrentRoom.PlayerCount >= _maximumPlayers)
            {
                matchMakingPanel.MatchFound();
            }
        }
    }
}
