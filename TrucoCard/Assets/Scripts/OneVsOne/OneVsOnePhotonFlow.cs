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
    Action<string> _pendingHostError;

    // Device-specific "cannot create room": some phones/networks block Photon UDP, or hold a stale
    // region override. The old OnDisconnected loop reconnected forever with no error, leaving
    // IsMatchmakingBusy stuck and the Create button dead. Now: UDP → TCP fallback, bounded retries,
    // a watchdog, and a visible failure that also rolls back the API match.
    static readonly ConnectionProtocol[] ProtocolFallbacks = { ConnectionProtocol.Udp, ConnectionProtocol.Tcp };
    const int MaxConnectFailuresPerPurpose = 4;
    const float PurposeConnectTimeoutSeconds = 30f;
    int _protocolIndex;
    int _connectFailures;
    Coroutine _purposeWatchdog;

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
        _pendingHostError = onError;
        if (string.IsNullOrEmpty(OneVsOneMatchSession.PhotonRoomName))
        {
            onError?.Invoke("Nombre de sala Photon vacío.");
            _pendingHostError = null;
            return;
        }
        IsConnecting = true;
        _matchFoundFired = false;
        CurrentPurpose = Purpose.CreateHostedRoom;
        BeginPurposeWatchdog();
        if (EnsureConnectedToFixedRegion()) return;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Photon, "StartHostPhoton → " + OneVsOneMatchSession.PhotonRoomName
                  + " region=" + (PhotonNetwork.CloudRegion ?? "?")
                  + " protocol=" + CurrentProtocol());
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
        if (!ConnectWithCurrentProtocol())
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "ConnectUsingSettings failed (host create)");
            AbortPurpose("ConnectUsingSettings=false");
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
        BeginPurposeWatchdog();
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
        if (!ConnectWithCurrentProtocol())
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.Photon, "ConnectUsingSettings failed (guest join)");
            onError?.Invoke(TrucoTextosClient.PhotonConnectFailed);
            AbortPurpose("ConnectUsingSettings=false", notifyJoin: false);
        }
    }

    // ---- Connection robustness (protocol fallback / bounded retries / watchdog) ----

    ConnectionProtocol CurrentProtocol()
    {
        var s = PhotonNetwork.PhotonServerSettings;
        return s != null && s.AppSettings != null ? s.AppSettings.Protocol : ConnectionProtocol.Udp;
    }

    void ApplyProtocol(ConnectionProtocol protocol)
    {
        var s = PhotonNetwork.PhotonServerSettings;
        if (s == null || s.AppSettings == null) return;
        if (s.AppSettings.Protocol == protocol) return;
        s.AppSettings.Protocol = protocol;
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon, "Photon protocol → " + protocol);
    }

    bool ConnectWithCurrentProtocol()
    {
        TrucoPhotonRegionSettings.ApplyToPhoton();
        ApplyProtocol(ProtocolFallbacks[Mathf.Clamp(_protocolIndex, 0, ProtocolFallbacks.Length - 1)]);
        return PhotonNetwork.ConnectUsingSettings();
    }

    static bool IsConnectFailureCause(DisconnectCause cause)
    {
        switch (cause)
        {
            case DisconnectCause.ExceptionOnConnect:
            case DisconnectCause.DnsExceptionOnConnect:
            case DisconnectCause.ServerAddressInvalid:
            case DisconnectCause.ServerTimeout:
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.Exception:
            case DisconnectCause.InvalidRegion:
            case DisconnectCause.InvalidAuthentication:
            case DisconnectCause.AuthenticationTicketExpired:
            case DisconnectCause.OperationNotAllowedInCurrentState:
            case DisconnectCause.DisconnectByOperationLimit:
            case DisconnectCause.MaxCcuReached:
                return true;
            default:
                return false;
        }
    }

    /// <summary>Pick the next transport / clear a bad region so the retry has a real chance.</summary>
    void ApplyConnectFallback(DisconnectCause cause)
    {
        if (cause == DisconnectCause.InvalidRegion || cause == DisconnectCause.InvalidAuthentication
            || cause == DisconnectCause.ServerAddressInvalid)
        {
            // A stale per-device region override (PlayerPrefs) can point at a region this app cannot use.
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon, "Photon " + cause + " — clearing region override");
            TrucoPhotonRegionSettings.ClearPlayerPrefsOverride();
        }
        if (_protocolIndex < ProtocolFallbacks.Length - 1)
        {
            _protocolIndex++;
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon,
                "Photon connect failed (" + cause + ") — falling back to " + ProtocolFallbacks[_protocolIndex]);
        }
    }

    void BeginPurposeWatchdog()
    {
        StopPurposeWatchdog();
        _connectFailures = 0;
        _purposeWatchdog = StartCoroutine(PurposeWatchdogRoutine());
    }

    void StopPurposeWatchdog()
    {
        if (_purposeWatchdog != null)
        {
            StopCoroutine(_purposeWatchdog);
            _purposeWatchdog = null;
        }
    }

    IEnumerator PurposeWatchdogRoutine()
    {
        yield return new WaitForSecondsRealtime(PurposeConnectTimeoutSeconds);
        _purposeWatchdog = null;
        if (CurrentPurpose == Purpose.None || PhotonNetwork.InRoom || _matchFoundFired) yield break;
        // Join has its own bounded retry loop once we are on the Master server.
        if (CurrentPurpose == Purpose.JoinHostedRoom && PhotonNetwork.IsConnectedAndReady
            && PhotonNetwork.Server == ServerConnection.MasterServer && _joinRetryCount > 0)
            yield break;
        TrucoDebugLog.Error(TrucoDebugLog.Category.Photon,
            "Photon purpose timeout purpose=" + CurrentPurpose
            + " state=" + PhotonNetwork.NetworkClientState
            + " protocol=" + CurrentProtocol()
            + " region=" + (PhotonNetwork.CloudRegion ?? "?"));
        AbortPurpose("timeout " + PhotonNetwork.NetworkClientState);
    }

    /// <summary>Give up the current create/join: unstick busy flags and tell the caller (create → API rollback).</summary>
    void AbortPurpose(string reason, bool notifyJoin = true)
    {
        var purpose = CurrentPurpose;
        StopPurposeWatchdog();
        StopJoinRetryRoutine();
        _joinRetryCount = 0;
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeave = false;
        _deferredCreateAfterLeaveLobby = false;
        _deferredJoinAfterLeaveLobby = false;
        _connectFailures = 0;
        // Next attempt starts from UDP again; a working TCP fallback will be re-found on failure.
        _protocolIndex = 0;
        string detail = TrucoTextosClient.PhotonConnectFailed + " (" + reason + ")";
        TrucoNotificationLog.Warning("PHOTON ABORT purpose=" + purpose + " " + reason);
        if (purpose == Purpose.CreateHostedRoom)
        {
            bool hadCallback = _pendingHostError != null;
            InvokeHostError(detail);
            if (!hadCallback) AppManager.Instance?.DisplayNotification(detail);
        }
        else if (purpose == Purpose.JoinHostedRoom && notifyJoin)
        {
            FailJoinAndClearGuestSession(detail, clearGuestSession: false);
        }
        if (PhotonNetwork.IsConnected && !PhotonNetwork.InRoom && PhotonNetwork.NetworkClientState != ClientState.ConnectedToMasterServer)
            PhotonNetwork.Disconnect();
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
        _connectFailures = 0;
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon,
            "Photon OnConnectedToMaster region=" + (PhotonNetwork.CloudRegion ?? "?")
            + " protocol=" + CurrentProtocol() + " purpose=" + CurrentPurpose);
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
        StopPurposeWatchdog();
        IsConnecting = false;
        CurrentPurpose = Purpose.None;
        _deferredCreateAfterLeave = false;
        _deferredJoinAfterLeave = false;
        _deferredCreateAfterLeaveLobby = false;
        _deferredJoinAfterLeaveLobby = false;
        string detail = string.Format(TrucoTextosClient.PhotonCreateFailed, message);
        AppManager.Instance.DisplayNotification(detail);
        InvokeHostError(detail);
    }

    void InvokeHostError(string err)
    {
        var cb = _pendingHostError;
        _pendingHostError = null;
        cb?.Invoke(err);
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
        StopPurposeWatchdog();
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
        if (cause == DisconnectCause.ApplicationQuit) return;
        TrucoDebugLog.Always(TrucoDebugLog.Category.Photon,
            "Photon OnDisconnected cause=" + cause + " purpose=" + CurrentPurpose
            + " protocol=" + CurrentProtocol() + " failures=" + _connectFailures);
        bool connectFailure = IsConnectFailureCause(cause);
        // Rotate transport even without a purpose so the next lobby/create attempt is not stuck on blocked UDP.
        if (connectFailure) ApplyConnectFallback(cause);
        if (CurrentPurpose == Purpose.None || PhotonNetwork.OfflineMode) return;

        if (cause != DisconnectCause.DisconnectByClientLogic)
        {
            _connectFailures++;
            if (_connectFailures >= MaxConnectFailuresPerPurpose)
            {
                AbortPurpose(cause.ToString());
                return;
            }
        }
        if (!ConnectWithCurrentProtocol())
            AbortPurpose(cause + " / reconnect rejected");
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
            ConnectWithCurrentProtocol();
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
        _connectFailures = 0;
        StopJoinRetryRoutine();
        StopPurposeWatchdog();
        if (CurrentPurpose == Purpose.CreateHostedRoom)
            _pendingHostError = null;
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
        StopPurposeWatchdog();
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
