using System;
using System.Collections;
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

    /// <summary>
    /// True while create/join is still connecting — do not JoinLobby over this.
    /// Once the host is already InRoom waiting for a guest, we are NOT busy for lobby UI
    /// (otherwise JOIN on other rooms silently fails with photonBusy=True forever).
    /// </summary>
    public bool IsMatchmakingBusy =>
        IsConnecting
        || _matchFoundFired
        || CurrentPurpose == Purpose.JoinHostedRoom
        || (CurrentPurpose == Purpose.CreateHostedRoom && !PhotonNetwork.InRoom);

    public static bool IsMatchmakingBusyGlobally =>
        Instance != null && Instance.IsMatchmakingBusy;

    bool _deferredJoinAfterLeave;
    bool _deferredCreateAfterLeave;
    bool _deferredJoinAfterLeaveLobby;
    bool _deferredCreateAfterLeaveLobby;
    int _deferredCreateMaxPlayers;
    bool _matchFoundFired;
    int _joinRetryCount;
    Coroutine _joinRetryRoutine;

    static int MaxJoinRetries => TrucoClientSettings.PhotonJoinMaxAttempts;
    float JoinRetryDelaySeconds => TrucoClientSettings.PhotonJoinRetryIntervalSeconds;

    static bool _applicationQuitting;

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
        Application.quitting += () => _applicationQuitting = true;
    }

    void OnApplicationQuit() => _applicationQuitting = true;

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
        if (EnsureConnectedToFixedRegion()) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "StartHostPhoton → " + OneVsOneMatchSession.PhotonRoomName
                  + " region=" + (PhotonNetwork.CloudRegion ?? "?"));
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
            ProceedToCreateRoom(OneVsOneMatchSession.PhotonRoomName, maxPlayers);
            return;
        }
        if (PhotonNetwork.IsConnected)
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "Waiting for Photon MasterServer before CreateRoom…");
            return;
        }
        if (!PhotonNetwork.ConnectUsingSettings())
        {
            IsConnecting = false;
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "ConnectUsingSettings failed (host create)");
            onError?.Invoke(TrucoTextosClient.PhotonConnectFailed);
        }
    }
    public void StartJoinPhoton(Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            onError?.Invoke(TrucoTextosClient.ErrorUnirse);
            return;
        }
        IsConnecting = true;
        _matchFoundFired = false;
        _joinRetryCount = 0;
        StopJoinRetryRoutine();
        CurrentPurpose = Purpose.JoinHostedRoom;
        if (EnsureConnectedToFixedRegion()) return;
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon, "StartJoinPhoton → " + OneVsOneMatchSession.PhotonRoomName
                  + " region=" + (PhotonNetwork.CloudRegion ?? "?")
                  + " inLobby=" + PhotonNetwork.InLobby
                  + " retries=" + MaxJoinRetries + "x" + JoinRetryDelaySeconds + "s");
        PhotonNetwork.AutomaticallySyncScene = true;
        if (PhotonNetwork.InRoom)
        {
            _deferredJoinAfterLeave = true;
            PhotonNetwork.LeaveRoom();
            return;
        }
        if (PhotonNetwork.IsConnected && PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
        {
            ProceedToJoinRoom();
            return;
        }
        if (PhotonNetwork.IsConnected)
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "Waiting for Photon MasterServer before JoinRoom…");
            return;
        }
        if (!PhotonNetwork.ConnectUsingSettings())
        {
            IsConnecting = false;
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "ConnectUsingSettings failed (guest join)");
            onError?.Invoke(TrucoTextosClient.PhotonConnectFailed);
        }
    }

    public override void OnLeftRoom()
    {
        if (_deferredJoinAfterLeave)
        {
            _deferredJoinAfterLeave = false;
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer)
                ProceedToJoinRoom();
            return;
        }
        if (_deferredCreateAfterLeave)
        {
            _deferredCreateAfterLeave = false;
            ProceedToCreateRoom(OneVsOneMatchSession.PhotonRoomName, _deferredCreateMaxPlayers);
            return;
        }
        // Do NOT auto-cancel the backend match here. Accidental LeaveRoom (lobby
        // refresh/purge) used to wipe the room the host just created. Intentional
        // leave goes through Delete Room / Back confirm / app quit watchdog.
        if (_matchFoundFired || OneVsOneMatchSession.GameStarted) return;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (otherPlayer == null || otherPlayer.IsLocal) return;
        if (OneVsOneMatchSession.GameStarted || _matchFoundFired) return;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId)) return;
        // Pre-game: opponent left before cards — cancel once so both get refund path.
        int others = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
            "PreGame OnPlayerLeftRoom other=" + otherPlayer.ActorNumber
            + " roomPlayers=" + others + " match=" + OneVsOneMatchSession.CurrentMatchId);
        if (others >= 2) return;
        string matchId = OneVsOneMatchSession.CurrentMatchId;
        TrucoRulesScenarioLog.Backend("PreGame opponent left → CancelLobbyMatch", "match=" + matchId);
        _ = OneVsOneMatchLifecycle.CancelLobbyMatchAsync(matchId);
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.SalaEliminada);
    }

    public override void OnConnectedToMaster()
    {
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
        if (CurrentPurpose == Purpose.CreateHostedRoom)
            ProceedToCreateRoom(OneVsOneMatchSession.PhotonRoomName, OneVsOneMatchSession.MaxPlayersPhoton);
        else if (CurrentPurpose == Purpose.JoinHostedRoom)
            ProceedToJoinRoom();
        else if (!PhotonNetwork.InRoom && !PhotonNetwork.InLobby && !PhotonNetwork.OfflineMode)
            PhotonNetwork.JoinLobby();
    }

    void ProceedToCreateRoom(string name, int maxPlayers)
    {
        if (PhotonNetwork.InLobby)
        {
            _deferredCreateAfterLeaveLobby = true;
            _deferredCreateMaxPlayers = maxPlayers;
            PhotonNetwork.LeaveLobby();
            return;
        }
        CreateRoomWithOptions(name, maxPlayers);
    }

    void ProceedToJoinRoom()
    {
        if (PhotonNetwork.InLobby)
        {
            _deferredJoinAfterLeaveLobby = true;
            PhotonNetwork.LeaveLobby();
            return;
        }
        AttemptJoinTargetRoom();
    }

    public override void OnLeftLobby()
    {
        _lobbyRoomPlayerCount.Clear();
        _lobbySyncReceived = false;
        if (_deferredJoinAfterLeaveLobby)
        {
            _deferredJoinAfterLeaveLobby = false;
            if (CurrentPurpose == Purpose.JoinHostedRoom)
                AttemptJoinTargetRoom();
            return;
        }
        if (_deferredCreateAfterLeaveLobby)
        {
            _deferredCreateAfterLeaveLobby = false;
            CreateRoomWithOptions(OneVsOneMatchSession.PhotonRoomName, _deferredCreateMaxPlayers);
        }
    }

    private void CreateRoomWithOptions(string name, int maxPlayers)
    {
        var props = new Hashtable
        {
            ["onev1"] = true,
            ["matchId"] = OneVsOneMatchSession.CurrentMatchId ?? string.Empty,
            ["targetScore"] = OneVsOneMatchSession.TargetScore
        };
        var opts = new RoomOptions
        {
            MaxPlayers = (byte)Mathf.Clamp(maxPlayers, 2, 16),
            IsVisible = true,
            IsOpen = true,
            PlayerTtl = 60000,
            EmptyRoomTtl = 120000,
            CustomRoomProperties = props,
            CustomRoomPropertiesForLobby = new[] { "onev1", "matchId", "targetScore" }
        };
        PhotonNetwork.JoinOrCreateRoom(name, opts, TypedLobby.Default);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        // 32766 = GameIdAlreadyExists — reclaim by joining instead of failing the host flow.
        bool alreadyExists = returnCode == 32766
            || (!string.IsNullOrEmpty(message)
                && message.IndexOf("already exist", StringComparison.OrdinalIgnoreCase) >= 0);
        if (alreadyExists && CurrentPurpose == Purpose.CreateHostedRoom
            && !string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
                "CreateRoom already exists — JoinRoom instead: " + OneVsOneMatchSession.PhotonRoomName);
            IsConnecting = true;
            CurrentPurpose = Purpose.CreateHostedRoom;
            PhotonNetwork.JoinRoom(OneVsOneMatchSession.PhotonRoomName);
            return;
        }

        TrucoDebugLog.Error(TrucoDebugLog.Category.Photon,
            "CreateRoomFailed code=" + returnCode + " msg=" + message);
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeave = false;
        _deferredCreateAfterLeaveLobby = false;
        _deferredJoinAfterLeaveLobby = false;
        AppManager.Instance.DisplayNotification(string.Format(TrucoTextosClient.PhotonCreateFailed, message));
    }

    void AttemptJoinTargetRoom()
    {
        string room = OneVsOneMatchSession.PhotonRoomName;
        if (string.IsNullOrEmpty(room))
        {
            IsConnecting = false;
            CurrentPurpose = Purpose.None;
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "AttemptJoinTargetRoom: empty room name");
            return;
        }
        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.Server != ServerConnection.MasterServer)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
                "AttemptJoinTargetRoom deferred — not on Master yet (ready="
                + PhotonNetwork.IsConnectedAndReady + " server=" + PhotonNetwork.Server + ")");
            IsConnecting = true;
            return;
        }
        if (PhotonNetwork.InLobby)
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "AttemptJoinTargetRoom: still InLobby → LeaveLobby");
            _deferredJoinAfterLeaveLobby = true;
            PhotonNetwork.LeaveLobby();
            return;
        }
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon,
            $"JoinRoom attempt #{_joinRetryCount + 1}/{MaxJoinRetries}: {room}"
            + " region=" + (PhotonNetwork.CloudRegion ?? "?")
            + " appVersion=" + (PhotonNetwork.PhotonServerSettings?.AppSettings?.AppVersion ?? "?"));
        bool sent = PhotonNetwork.JoinRoom(room);
        if (!sent)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
                "JoinRoom() returned false — will retry. State=" + PhotonNetwork.NetworkClientState);
            IsConnecting = true;
            if (CurrentPurpose == Purpose.JoinHostedRoom && _joinRetryCount < MaxJoinRetries)
            {
                _joinRetryCount++;
                StopJoinRetryRoutine();
                _joinRetryRoutine = StartCoroutine(JoinRetryRoutine());
            }
            else
                FailJoinAndClearGuestSession("JoinRoom() rejected locally", clearGuestSession: false);
        }
    }

    bool ShouldRetryJoin(short returnCode, string message) =>
        OneVsOneLobbyFlowRules.ShouldRetryPhotonJoin(returnCode, message, _joinRetryCount, MaxJoinRetries);

    void StopJoinRetryRoutine()
    {
        if (_joinRetryRoutine != null)
        {
            StopCoroutine(_joinRetryRoutine);
            _joinRetryRoutine = null;
        }
    }

    IEnumerator JoinRetryRoutine()
    {
        yield return new WaitForSecondsRealtime(JoinRetryDelaySeconds);
        _joinRetryRoutine = null;
        if (CurrentPurpose != Purpose.JoinHostedRoom) yield break;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName)) yield break;
        if (!PhotonNetwork.IsConnectedAndReady || PhotonNetwork.Server != ServerConnection.MasterServer) yield break;
        AttemptJoinTargetRoom();
    }

    void FailJoinAndClearGuestSession(string message, bool clearGuestSession = true)
    {
        StopJoinRetryRoutine();
        _joinRetryCount = 0;
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredJoinAfterLeave = false;
        if (!OneVsOneMatchSession.IsHost && clearGuestSession)
        {
            OneVsOneMatchSession.Clear();
            TrucoRoomPersistence.Clear();
        }
        string userMsg = TrucoUserFacingErrors.ForApiOrPhoton(message);
        if (string.IsNullOrEmpty(message))
            userMsg = TrucoTextosClient.ErrorUnirse;
        if (!clearGuestSession)
            userMsg = TrucoTextosClient.ErrorUnirse + " " + TrucoLocalization.T(TrucoLocalization.Key.RejoinPartida);
        TrucoDebugLog.Error(TrucoDebugLog.Category.OneVsOne,
            "Join abandoned: " + message + " clearSession=" + clearGuestSession);
        TrucoNotificationLog.Warning("JOIN ABANDONED: " + message);
        AppManager.Instance.DisplayNotification(userMsg);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
            "JoinRoomFailed code=" + returnCode + " msg=" + message + " attempt=" + (_joinRetryCount + 1) +
            "/" + MaxJoinRetries);

        // Host reclaim after "already exists" — room vanished; create fresh.
        if (CurrentPurpose == Purpose.CreateHostedRoom
            && !string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "Host Join failed — CreateRoom again");
            CreateRoomWithOptions(OneVsOneMatchSession.PhotonRoomName, OneVsOneMatchSession.MaxPlayersPhoton);
            return;
        }

        if (CurrentPurpose == Purpose.JoinHostedRoom && ShouldRetryJoin(returnCode, message) && _joinRetryCount < MaxJoinRetries)
        {
            _joinRetryCount++;
            if (_joinRetryCount == 1)
                AppManager.Instance?.DisplayNotification(TrucoTextosClient.EsperandoAnfitrionPhoton);
            StopJoinRetryRoutine();
            _joinRetryRoutine = StartCoroutine(JoinRetryRoutine());
            return;
        }
        FailJoinAndClearGuestSession(message, clearGuestSession: false);
    }

    /// <summary>Disconnect when connected to the wrong cloud region (SO FixedRegion must match).</summary>
    bool EnsureConnectedToFixedRegion()
    {
        TrucoPhotonRegionSettings.ApplyToPhoton();
        string target = TrucoPhotonRegionSettings.RegionCode;
        if (string.IsNullOrEmpty(target) || !PhotonNetwork.IsConnected) return false;
        if (string.IsNullOrEmpty(PhotonNetwork.CloudRegion) || PhotonNetwork.CloudRegion == target) return false;
        TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
            "Reconnecting Photon: was " + PhotonNetwork.CloudRegion + " → " + target);
        PhotonNetwork.Disconnect();
        return true;
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        StopJoinRetryRoutine();
        _joinRetryCount = 0;
        _deferredJoinAfterLeave = false;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeaveLobby = false;
        _deferredCreateAfterLeaveLobby = false;
        _lobbyRoomPlayerCount.Clear();
        _lobbySyncReceived = false;
        if (_applicationQuitting || Application.isPlaying == false) return;
        if (CurrentPurpose != Purpose.None && !PhotonNetwork.OfflineMode)
        {
            TrucoPhotonRegionSettings.ApplyToPhoton();
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnJoinedLobby()
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
        if (IsMatchmakingBusy) return;
        if (PhotonNetwork.InRoom) return;
        if (!string.IsNullOrEmpty(OneVsOneMatchSession.CurrentMatchId) && !OneVsOneMatchSession.GameStarted)
            return;
        if (!PhotonNetwork.IsConnected)
        {
            TrucoPhotonRegionSettings.ApplyToPhoton();
            PhotonNetwork.ConnectUsingSettings();
            return;
        }
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

    public override void OnCreatedRoom()
    {
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon,
            "Photon room created: " + OneVsOneMatchSession.PhotonRoomName
            + " region=" + (PhotonNetwork.CloudRegion ?? "?"));
    }

    public override void OnJoinedRoom()
    {
        IsConnecting = false;
        _joinRetryCount = 0;
        StopJoinRetryRoutine();
        if (PhotonNetwork.CurrentRoom != null
            && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("targetScore", out object targetValue))
        {
            if (targetValue is int target)
                OneVsOneMatchSession.SetTargetScore(target);
            else if (targetValue is byte targetByte)
                OneVsOneMatchSession.SetTargetScore(targetByte);
        }
        TrucoRoomPersistence.SaveCurrentRoom();
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon,
            "OnJoinedRoom name=" + (PhotonNetwork.CurrentRoom?.Name ?? "?")
            + " players=" + (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0)
            + " master=" + PhotonNetwork.IsMasterClient
            + " purpose=" + CurrentPurpose
            + " region=" + (PhotonNetwork.CloudRegion ?? "?"));
        if (PhotonNetwork.CurrentRoom == null) return;

        if (TrucoLobbyMatchmakingUi.IsRoomListWaitingMode() ||
            OneVsOneMatchLifecycle.IsWaitingInPreGameLobby())
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
        if (PhotonNetwork.CurrentRoom == null) return;
        if (!OneVsOneLobbyFlowRules.IsReadyToLaunchGameplay(
                PhotonNetwork.CurrentRoom.PlayerCount, OneVsOneMatchSession.MaxPlayersGameplay))
            return;
        _matchFoundFired = true;
        CurrentPurpose = Purpose.None;
        StopJoinRetryRoutine();
        TrucoLobbyMatchmakingUi.HideWaitingOverlay();
        TrucoDebugLog.Always(TrucoDebugLog.Category.OneVsOne,
            "FireMatchFound players=" + PhotonNetwork.CurrentRoom.PlayerCount
            + " master=" + PhotonNetwork.IsMasterClient
            + " match=" + (OneVsOneMatchSession.CurrentMatchId ?? "?"));

        bool roomListMode = TrucoLobbyMatchmakingUi.IsRoomListWaitingMode();
        bool launchedUi = false;
        if (!roomListMode)
        {
            if (_sessionUi == null) _sessionUi = FindObjectOfType<OneVsOnePhotonSessionUi>(true);
            if (_sessionUi != null && _sessionUi.gameObject != null)
            {
                if (!_sessionUi.gameObject.activeInHierarchy)
                    _sessionUi.gameObject.SetActive(true);
                _sessionUi.MatchFound();
                launchedUi = true;
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
                    launchedUi = true;
                }
            }
        }

        if (roomListMode || !launchedUi)
            StartCoroutine(LaunchGameplayCountdownRoutine());
    }

    IEnumerator LaunchGameplayCountdownRoutine()
    {
        AppManager.Instance?.DisplayNotification(TrucoTextosClient.PartidaEncontrada);
        yield return new WaitForSecondsRealtime(1f);
        AppManager.Instance?.HideNotification();
        yield return new WaitForSecondsRealtime(2f);
        TrucoOneVsOneGameplayLaunch.LoadFromCurrentRoom();
        if (!PhotonNetwork.IsMasterClient)
            StartCoroutine(GuestWaitForSyncedGameplay());
    }

    IEnumerator GuestWaitForSyncedGameplay()
    {
        float deadline = Time.unscaledTime + 12f;
        while (Time.unscaledTime < deadline)
        {
            if (TrucoOneVsOneGameplayLaunch.IsGameplaySceneActive) yield break;
            if (!PhotonNetwork.InRoom) yield break;
            yield return null;
        }
        if (!TrucoOneVsOneGameplayLaunch.IsGameplaySceneActive && PhotonNetwork.InRoom)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.OneVsOne,
                "Guest scene sync timeout — loading Gameplay locally.");
            TrucoSceneTransition.Go("Gameplay");
        }
    }

    public void ResetPurpose()
    {
        StopJoinRetryRoutine();
        _joinRetryCount = 0;
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
