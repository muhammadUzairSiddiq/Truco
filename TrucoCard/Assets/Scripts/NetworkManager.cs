using System;
using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    [Header("1vs1: lista de salas (Paraguay / backend)")]
    [Tooltip("Si está asignado, se deja de usar búsqueda al azar y se abre el lobby de salas.")]
    [SerializeField] private OneVsOneRoomListController oneVsOneRoomList;

    [SerializeField] private int waitTime;
    [SerializeField] private MatchMakingPanel matchMakingPanel;
    
    
    private int _maximumPlayers = 2;
    private bool _clicked = false;

    void Awake()
    {
        if (matchMakingPanel == null) matchMakingPanel = FindObjectOfType<MatchMakingPanel>(true);
        if (oneVsOneRoomList == null) oneVsOneRoomList = FindObjectOfType<OneVsOneRoomListController>(true);
    }

    void Start()
    {
        if (oneVsOneRoomList != null) return;
        if (SceneManager.GetActiveScene().name != "MainMenu") return;
        TrucoOneVsOneMainMenuFactory.EnsureOnMainMenu();
        oneVsOneRoomList = FindObjectOfType<OneVsOneRoomListController>(true);
    }

    public void ConnectToMaster()
    {
        if (oneVsOneRoomList != null)
        {
            oneVsOneRoomList.Open();
            return;
        }

        AppManager.Instance.DisplayLoadingUI("Conectando al servidor…");

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
                IsOpen = true,
                PlayerTtl = 60000,
                EmptyRoomTtl = 120000
            };

            PhotonNetwork.CreateRoom(null, roomOptions);
        }
    }

    public override void OnJoinedRoom()
    {
        TrucoRoomPersistence.SaveCurrentRoom();
        if (ApiController.GetSessionUser?.Data?._id != null)
        {
            var h = new ExitGames.Client.Photon.Hashtable { ["userId"] = ApiController.GetSessionUser.Data._id };
            PhotonNetwork.LocalPlayer.SetCustomProperties(h);
        }
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
