using System;
using System.Collections.Generic;
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
    bool _matchFoundFired;

    /// <summary>Salas visibles en el lobby de Photon (nombre → jugadores en tiempo real).</summary>
    readonly Dictionary<string, int> _lobbyRoomPlayerCount = new Dictionary<string, int>(32, StringComparer.Ordinal);
    bool _lobbySyncReceived;

    /// <summary>La lista de salas del menú debe refrescar filas cuando llega <see cref="OnRoomListUpdate"/>.</summary>
    public static event Action OnLobbyRoomCountsChanged;

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
        _matchFoundFired = false;
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
            onError?.Invoke(TrucoTextosClient.PhotonConnectFailed);
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
        _matchFoundFired = false;
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
            onError?.Invoke(TrucoTextosClient.PhotonConnectFailed);
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
        else if (!PhotonNetwork.InRoom && !PhotonNetwork.InLobby && !PhotonNetwork.OfflineMode)
            PhotonNetwork.JoinLobby();
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
        // Photon room name collision (previous match room still alive) — join it instead of failing.
        if (CurrentPurpose == Purpose.CreateHostedRoom
            && !string.IsNullOrEmpty(message)
            && message.IndexOf("already exist", StringComparison.OrdinalIgnoreCase) >= 0
            && !string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            Debug.LogWarning("[OneVsOnePhoton] Room exists — joining instead: " + OneVsOneMatchSession.PhotonRoomName);
            CurrentPurpose = Purpose.JoinHostedRoom;
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
                PhotonNetwork.JoinRoom(OneVsOneMatchSession.PhotonRoomName);
            return;
        }
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeave = false;
        AppManager.Instance.DisplayNotification(string.Format(TrucoTextosClient.PhotonCreateFailed, message));
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
        _lobbyRoomPlayerCount.Clear();
        _lobbySyncReceived = false;
    }

    public override void OnJoinedLobby()
    {
        _lobbyRoomPlayerCount.Clear();
        _lobbySyncReceived = false;
    }

    public override void OnLeftLobby()
    {
        _lobbyRoomPlayerCount.Clear();
        _lobbySyncReceived = false;
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (roomList == null) return;
        _lobbySyncReceived = true;
        for (int i = 0; i < roomList.Count; i++)
        {
            var ri = roomList[i];
            if (ri == null || string.IsNullOrEmpty(ri.Name)) continue;
            if (ri.RemovedFromList)
            {
                _lobbyRoomPlayerCount.Remove(ri.Name);
            }
            else
            {
                _lobbyRoomPlayerCount[ri.Name] = ri.PlayerCount;
            }
        }
        OnLobbyRoomCountsChanged?.Invoke();
    }

    /// <summary>Conecta a Master y entra al lobby por defecto para recibir <see cref="OnRoomListUpdate"/> (conteo real de jugadores).</summary>
    public void EnsureLobbyForRoomList()
    {
        if (PhotonNetwork.OfflineMode) return;
        if (PhotonNetwork.InRoom) return;
        if (!PhotonNetwork.IsConnected) { PhotonNetwork.ConnectUsingSettings(); return; }
        if (PhotonNetwork.Server != ServerConnection.MasterServer) return;
        if (!PhotonNetwork.InLobby) PhotonNetwork.JoinLobby(TypedLobby.Default);
    }

    /// <summary>Conteo en vivo según el lobby de Photon. Si el nombre no está y ya hubo sync, la sala no existe (0 jugadores).</summary>
    public bool TryGetLiveLobbyPlayerCount(string photonRoomName, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(photonRoomName) || !PhotonNetwork.InLobby) return false;
        if (_lobbyRoomPlayerCount.TryGetValue(photonRoomName, out int c))
        {
            count = Mathf.Clamp(c, 0, 2);
            return true;
        }
        if (_lobbySyncReceived)
        {
            count = 0;
            return true;
        }
        return false;
    }

    public static bool TryGetLiveLobbyPlayerCountForMatch(string photonRoomName, out int count)
    {
        count = 0;
        if (Instance == null) return false;
        return Instance.TryGetLiveLobbyPlayerCount(photonRoomName, out count);
    }

    public override void OnJoinedRoom()
    {
        IsConnecting = false;
        TrucoRoomPersistence.SaveCurrentRoom();
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
        if (PhotonNetwork.CurrentRoom == null) return;

        if (TrucoLobbyMatchmakingUi.IsRoomListWaitingMode())
        {
            TrucoLobbyMatchmakingUi.HideWaitingOverlay();
            OnLobbyRoomCountsChanged?.Invoke();
        }
        else
        {
            if (_sessionUi == null) _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
            if (_sessionUi != null) _sessionUi.Initialize();
            else if (_matchMakingPanel != null) _matchMakingPanel.Initialize();
        }

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
        if (_matchFoundFired) return;
        _matchFoundFired = true;
        TrucoLobbyMatchmakingUi.ShowMatchFoundOverlay();
        if (_sessionUi == null) _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
        if (_sessionUi != null)
        {
            if (_sessionUi.gameObject != null && !_sessionUi.gameObject.activeInHierarchy)
                _sessionUi.gameObject.SetActive(true);
            _sessionUi.MatchFound();
        }
        else
        {
            if (_matchMakingPanel == null)
                _matchMakingPanel = FindObjectOfType<MatchMakingPanel>(true);
            if (_matchMakingPanel != null)
            {
                if (_matchMakingPanel.gameObject != null && !_matchMakingPanel.gameObject.activeInHierarchy)
                    _matchMakingPanel.gameObject.SetActive(true);
                _matchMakingPanel.MatchFound();
            }
        }
    }

    public void ResetPurpose()
    {
        CurrentPurpose = Purpose.None;
        _matchFoundFired = false;
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
