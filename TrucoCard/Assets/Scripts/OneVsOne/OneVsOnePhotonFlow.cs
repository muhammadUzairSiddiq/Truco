using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Conexión PUN 1v1: crear sala o unirse por nombre, luego la misma transición a Gameplay que el panel clásico.</summary>
public class OneVsOnePhotonFlow : MonoBehaviourPunCallbacks
{
    [SerializeField] private MatchMakingPanel _matchMakingPanel;
    private OneVsOnePhotonSessionUi _sessionUi;

    public static OneVsOnePhotonFlow Instance { get; private set; }

    public enum Purpose
    {
        None,
        CreateHostedRoom,
        JoinHostedRoom
    }

    public Purpose CurrentPurpose { get; private set; } = Purpose.None;
    public bool IsConnecting { get; private set; }

    bool _deferredJoinAfterLeave;
    bool _deferredCreateAfterLeave;
    int _deferredCreateMaxPlayers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (_matchMakingPanel == null)
        {
            var found = FindObjectOfType<MatchMakingPanel>(true);
            if (found != null) _matchMakingPanel = found;
        }
        _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
    }

    /// <summary>Llamar después de player-create y RegisterPhoton; session ya rellenada con SetHostContext.</summary>
    public void StartHostPhoton(int maxPlayers, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            onError?.Invoke("Nombre de sala Photon vacío.");
            return;
        }
        IsConnecting = true;
        CurrentPurpose = Purpose.CreateHostedRoom;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom)
        {
            _deferredCreateAfterLeave = true;
            _deferredCreateMaxPlayers = maxPlayers;
            PhotonNetwork.LeaveRoom();
            return;
        }
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
        {
            CreateRoomWithOptions(OneVsOneMatchSession.PhotonRoomName, maxPlayers);
            return;
        }
        if (PhotonNetwork.IsConnected) return; // will continue in OnConnectedToMaster
        if (!PhotonNetwork.ConnectUsingSettings())
        {
            IsConnecting = false;
            onError?.Invoke("No se pudo conectar a Photon.");
        }
    }

    /// <summary>Tras join en backend; session rellenada con SetGuestContext.</summary>
    public void StartJoinPhoton(Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            onError?.Invoke(TrucoTextosClient.ErrorUnirse);
            return;
        }
        IsConnecting = true;
        CurrentPurpose = Purpose.JoinHostedRoom;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom)
        {
            _deferredJoinAfterLeave = true;
            PhotonNetwork.LeaveRoom();
            return;
        }
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
        {
            if (!PhotonNetwork.JoinRoom(OneVsOneMatchSession.PhotonRoomName)) IsConnecting = false;
            return;
        }
        if (PhotonNetwork.IsConnected) return;
        if (!PhotonNetwork.ConnectUsingSettings())
        {
            IsConnecting = false;
            onError?.Invoke("No se pudo conectar a Photon.");
        }
    }

    public override void OnLeftRoom()
    {
        if (_deferredJoinAfterLeave)
        {
            _deferredJoinAfterLeave = false;
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
                PhotonNetwork.JoinRoom(OneVsOneMatchSession.PhotonRoomName);
            return;
        }
        if (_deferredCreateAfterLeave)
        {
            _deferredCreateAfterLeave = false;
            CreateRoomWithOptions(OneVsOneMatchSession.PhotonRoomName, _deferredCreateMaxPlayers);
        }
    }

    public override void OnConnectedToMaster()
    {
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
        if (CurrentPurpose == Purpose.CreateHostedRoom)
            CreateRoomWithOptions(OneVsOneMatchSession.PhotonRoomName, OneVsOneMatchSession.MaxPlayersPhoton);
        else if (CurrentPurpose == Purpose.JoinHostedRoom)
            PhotonNetwork.JoinRoom(OneVsOneMatchSession.PhotonRoomName);
    }

    private void CreateRoomWithOptions(string name, int maxPlayers)
    {
        var props = new Hashtable
        {
            ["onev1"] = true,
            ["matchId"] = OneVsOneMatchSession.CurrentMatchId ?? string.Empty
        };
        var opts = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 2, 16),
            IsVisible = true,
            IsOpen = true,
            PlayerTtl = 60000,
            EmptyRoomTtl = 120000,
            CustomRoomProperties = props,
            CustomRoomPropertiesForLobby = new[] { "onev1", "matchId" }
        };
        PhotonNetwork.CreateRoom(name, opts, TypedLobby.Default);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeave = false;
        AppManager.Instance.DisplayNotification("No se pudo crear la sala en Photon: " + message);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredJoinAfterLeave = false;
        AppManager.Instance.DisplayNotification(TrucoTextosClient.ErrorUnirse + " " + message);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        _deferredJoinAfterLeave = false;
        _deferredCreateAfterLeave = false;
    }

    public override void OnJoinedRoom()
    {
        IsConnecting = false;
        TrucoRoomPersistence.SaveCurrentRoom();
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
        if (PhotonNetwork.CurrentRoom == null) return;
        if (_sessionUi == null) _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
        if (_sessionUi != null) _sessionUi.Initialize();
        else if (_matchMakingPanel != null) _matchMakingPanel.Initialize();
        if (PhotonNetwork.CurrentRoom.PlayerCount >= OneVsOneMatchSession.MaxPlayersGameplay)
            FireMatchFound();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.PlayerCount >= OneVsOneMatchSession.MaxPlayersGameplay)
            FireMatchFound();
    }

    void FireMatchFound()
    {
        if (_sessionUi == null) _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
        if (_sessionUi != null) _sessionUi.MatchFound();
        else _matchMakingPanel?.MatchFound();
    }

    public void ResetPurpose()
    {
        CurrentPurpose = Purpose.None;
    }

    /// <summary>Creates a DontDestroyOnLoad flow if none exists (1v1 lobby / join without scene wiring).</summary>
    public static OneVsOnePhotonFlow EnsureInstance()
    {
        if (Instance != null) return Instance;
        var f = UnityEngine.Object.FindObjectOfType<OneVsOnePhotonFlow>(true);
        if (f != null) return f;
        var go = new GameObject(nameof(OneVsOnePhotonFlow));
        return go.AddComponent<OneVsOnePhotonFlow>();
    }
}
