namespace MH.Multiplayer {
    using Photon.Pun;
    using Photon.Realtime;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEngine;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    public static class MultiplayerController 
    {
        #region Fields and Properties
        // Private fields
        private static TournamentRequest _currentTournamentRequest;
        private static List<User> _tournamentPlayers = new List<User>();
        private static MultiplayerCallbacks _multiplayerCallbacks;
        private static MultiplayerRPC _multiplayerRPC;
        private static string _pendingMatchRoomName;
        private static string _pendingHubThenMatch;
        private static bool _publishBracketOnNextHub;
        private static int _currentBracketLevel = 0;
        private static int _currentPlayerCounts = 0;
        private static bool _hasEnteredTournament = false;
        private static bool _isInTournamentMatch = false;
        private static int _generation;
        private static bool _starting;
        private static bool _loadingTable;
        private static bool _advancing;
        
        public static TournamentRequest CurrentTournamentRequest => _currentTournamentRequest;
        public static (int roundNumber, string bracketTitle) CurrentBracketLevel => (_currentBracketLevel, GetBracketTitle(_currentBracketLevel));
        public static bool IsTournamentActive => _currentTournamentRequest != null;
        public static TournamentMatch ActiveMatch;
        public static TournamentMatch NextMatch;
        /// <summary>Client presentation of the active bracket; it contains no economy/result authority.</summary>
        public static TournamentBracketState BracketState { get; private set; }
        public static void ReplaceBracketState(TournamentBracketState state)
        {
            if (state != null) BracketState = state;
        }

        public static void ApplyBracketFromCurrentRoom()
        {
            if (PhotonNetwork.CurrentRoom?.CustomProperties == null) return;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("bracketJson", out object raw)) return;
            var parsed = TournamentBracketState.FromJson(raw as string);
            if (parsed == null) return;
            parsed.MergeResults(BracketState);
            BracketState = parsed;
            for (int r = 0; r < parsed.rounds.Count; r++)
                for (int m = 0; m < parsed.rounds[r].Count; m++)
                    if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("result_" + r + "_" + m, out object result))
                    {
                        var finished = JsonUtility.FromJson<TournamentBracketState.BracketMatch>(result as string);
                        if (finished != null) parsed.RecordWinner(r, m, finished.winnerId, finished.winnerName);
                    }
            TournamentBracketOverlay.RefreshIfOpen();
        }

        public static void PublishBracketToCurrentRoom()
        {
            if (!PhotonNetwork.InRoom || BracketState == null) return;
            var changes = new Hashtable();
            if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("bracketJson"))
                changes["bracketJson"] = BracketState.ToJson();
            for (int r = 0; r < BracketState.rounds.Count; r++)
                for (int m = 0; m < BracketState.rounds[r].Count; m++)
                    if (BracketState.rounds[r][m].IsFinished)
                        changes["result_" + r + "_" + m] = JsonUtility.ToJson(BracketState.rounds[r][m]);
            PhotonNetwork.CurrentRoom.SetCustomProperties(changes);
        }

        public static void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged == null) return;
            if (propertiesThatChanged.Keys.OfType<string>().Any(k => k.StartsWith("result_")))
                ApplyBracketFromCurrentRoom();
            if (propertiesThatChanged.ContainsKey("backendMatchId") && ActiveMatch != null)
            {
                ActiveMatch.matchId = propertiesThatChanged["backendMatchId"] as string;
                ValidateRoom();
            }
            if (propertiesThatChanged.ContainsKey("tournamentFailure"))
            {
                string message = propertiesThatChanged["tournamentFailure"] as string;
                LeaveTournamentMatchmaking();
                AppManager.Instance?.DisplayNotification(message);
                return;
            }
            if (propertiesThatChanged.ContainsKey("bracketJson")) ApplyBracketFromCurrentRoom();
            if (propertiesThatChanged.ContainsKey("openingTables")) ApplyOpeningTables();
        }

        #endregion

        #region Events
        public static event Action<int> OnPlayerCountUpdated;
        public static event Action OnTournamentBrackedMatch;
        public static event Action<string> OnTournamentMatchFinalized;
        #endregion

        #region Initialization and Setup
        public static void InitMultiplayerSession_Tournament(TournamentRequest tournamentRequest, Action<string> onErrorAction = null) {
            if (_currentTournamentRequest != null) return;
            _generation++;
            PhotonNetwork.AutomaticallySyncScene = true;
            OneVsOnePhotonFlow.Instance?.ResetPurpose();
            ValidateMultiplayerCallbacks();

            Action JoinTournamentMatchMaking = () => {
                _multiplayerCallbacks.EnableTournamentMode();
                _currentTournamentRequest = tournamentRequest;
                _currentBracketLevel = CalculateBracketLevels(tournamentRequest.MaxPlayerCount);
                _currentPlayerCounts = tournamentRequest.MaxPlayerCount;
                _hasEnteredTournament = false;
                _isInTournamentMatch = false;
                PhotonNetwork.NickName = ApiController.GetSessionUser.Data.username;
                if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom)
                    JoinTournamentLobby(tournamentRequest);
                else if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            };

            if (!PhotonNetwork.IsConnectedAndReady) {
                Debug.Log("[MultiplayerController] - Connecting to Photon...");
                PhotonNetwork.AutomaticallySyncScene = true;
                TrucoPhotonRegionSettings.ApplyToPhoton();
                if (PhotonNetwork.ConnectUsingSettings()) {
                    Debug.Log("[MultiplayerController] - Connected to Photon.");
                    JoinTournamentMatchMaking();
                } else {
                    onErrorAction?.Invoke("Failed to connect to multiplayer server.");
                }
            } else {
                JoinTournamentMatchMaking();
            }
        }

        private static void JoinTournamentLobby(TournamentRequest request) {
            try {
                Hashtable roomProperties = new Hashtable {
                    ["tournamentId"] = request.TournamentId,
                    ["maxPlayers"] = _currentPlayerCounts,
                    ["entryFee"] = request.EntryFee,
                    ["prize"] = request.Prize,
                    ["withFlor"] = request.WithFlor,
                    ["targetScore"] = request.TargetScore,
                    ["bracketLevel"] = _currentBracketLevel,
                    ["tournamentName"] = request.TournamentName,
                    ["isTournament"] = true
                };

                Hashtable playerProperties = new Hashtable {
                    ["userId"] = ApiController.GetSessionUser.Data._id,
                    ["username"] = ApiController.GetSessionUser.Data.username,
                    ["isReady"] = false,
                    ["isTournamentPlayer"] = true
                };

                PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);
                string roomName = $"TournamentMatchmaking_{request.TournamentId}_{_currentBracketLevel}";

                RoomOptions roomOptions = new RoomOptions {
                    MaxPlayers = (byte)request.MaxPlayerCount,
                    PlayerTtl = 60000,
                    EmptyRoomTtl = 120000,
                    CustomRoomProperties = roomProperties,
                    CustomRoomPropertiesForLobby = new string[] { "tournamentId", "maxPlayers", "bracketLevel", "tournamentName", "isTournament" }
                };

                PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
                Debug.Log($"[MultiplayerController] - Joining tournament matchmaking room: {roomName}");
            } catch (Exception ex) {
                Debug.Log($"[MultiplayerController] - Error joining matchmaking room: {ex.Message}");
                AppManager.Instance.DisplayNotification("An error occurred while joining the matchmaking room.");
                TournamentManager.Instance.HideTournamentMatchmakingUI();
            }
        }

        public static void ValidateMultiplayerCallbacks() {
            if (_multiplayerCallbacks != null) return;
            GameObject callbacksObject = new GameObject("[MultiplayerCallbacks]");
            _multiplayerCallbacks = callbacksObject.AddComponent<MultiplayerCallbacks>();
            UnityEngine.Object.DontDestroyOnLoad(callbacksObject);
            Debug.Log($"[MultiplayerController] - MultiplayerCallbacks GameObject created!");
        }
        public static void ValidateMultiplayerRPC() {
            if (_multiplayerRPC != null) return;
            GameObject callbacksObject = PhotonNetwork.Instantiate("MultiplayerRPC", Vector3.zero, Quaternion.identity);
            _multiplayerRPC = callbacksObject.GetComponent<MultiplayerRPC>();
            UnityEngine.Object.DontDestroyOnLoad(callbacksObject);
            Debug.Log($"[MultiplayerController] MultiplayerRPC instantiated!");
        }

        #endregion

        #region Tournament Management
        [Serializable] sealed class OpeningTables { public TournamentMatch[] tables; }

        static void ApplyOpeningTables()
        {
            if (_isInTournamentMatch || PhotonNetwork.CurrentRoom == null) return;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("openingTables", out object raw)) return;
            var plan = JsonUtility.FromJson<OpeningTables>(raw as string);
            string id = ApiController.GetSessionUser?.Data?._id;
            if (plan?.tables == null || string.IsNullOrEmpty(id)) return;
            ApplyBracketFromCurrentRoom();
            foreach (var table in plan.tables)
                if (table.playerIds != null && table.playerIds.Contains(id))
                { JoinSpecificTournamentMatch(table, null); return; }
        }

        private async static void StartTournamentNow()
        {
            if (_starting || _isInTournamentMatch || _currentTournamentRequest == null) return;
            int generation = _generation;
            var room = PhotonNetwork.CurrentRoom;
            string tournamentId = _currentTournamentRequest.TournamentId;
            _starting = true;
            try
            {
                if (!_hasEnteredTournament)
                {
                    bool entered = await ApiController.EnterTournament(tournamentId);
                    if (generation != _generation || PhotonNetwork.CurrentRoom != room) return;
                    if (!entered) throw new InvalidOperationException("No se pudo confirmar la inscripción.");
                    _hasEnteredTournament = true;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["tournamentRegistered"] = tournamentId });
                }
                if (room.CustomProperties.ContainsKey("openingTables")) { ApplyOpeningTables(); return; }
                if (!PhotonNetwork.IsMasterClient) return;
                var seats = room.Players.Values.Where(p => !p.IsInactive).OrderBy(p => p.ActorNumber).ToList();
                if (seats.Count != _currentTournamentRequest.MaxPlayerCount
                    || seats.Any(p => !p.CustomProperties.TryGetValue("tournamentRegistered", out object r)
                                     || (string)r != tournamentId)) return;
                OnTournamentBrackedMatch?.Invoke();
                _tournamentPlayers = seats.Select(p => new User {
                    _id = p.CustomProperties["userId"] as string, username = p.NickName }).ToList();
                if (_tournamentPlayers.Any(p => string.IsNullOrEmpty(p._id))
                    || _tournamentPlayers.Select(p => p._id).Distinct().Count() != seats.Count)
                    throw new InvalidOperationException("Jugadores del torneo inválidos.");
                BracketState = new TournamentBracketState(seats.Count);
                BracketState.Seed(_tournamentPlayers);
                var tasks = new List<Task<match>>();
                for (int n = 0; n < seats.Count; n += 2)
                    tasks.Add(ApiController.CreateTournamentMatch(tournamentId,
                        new List<string> { _tournamentPlayers[n]._id, _tournamentPlayers[n + 1]._id }));
                var created = await Task.WhenAll(tasks);
                if (generation != _generation || PhotonNetwork.CurrentRoom != room) return;
                if (created.Any(m => string.IsNullOrEmpty(m?._id)))
                    throw new InvalidOperationException("No se pudieron crear todas las partidas. El torneo no inició.");
                var plan = new OpeningTables { tables = new TournamentMatch[created.Length] };
                for (int n = 0; n < created.Length; n++)
                    plan.tables[n] = new TournamentMatch {
                        matchId = created[n]._id,
                        roomName = TournamentBracketState.MatchRoomName(tournamentId, 0, n),
                        playerIds = new List<string> { _tournamentPlayers[n * 2]._id, _tournamentPlayers[n * 2 + 1]._id },
                        playerNames = new List<string> { _tournamentPlayers[n * 2].username, _tournamentPlayers[n * 2 + 1].username },
                        round = 0, matchIndex = n };
                // Publish all seats in one event before any local RPC can leave the lobby.
                room.SetCustomProperties(new Hashtable { ["openingTables"] = JsonUtility.ToJson(plan),
                    ["bracketJson"] = BracketState.ToJson() });
            }
            catch (Exception ex)
            {
                if (generation == _generation)
                {
                    room?.SetCustomProperties(new Hashtable { ["tournamentFailure"] = ex.Message });
                    AppManager.Instance?.DisplayNotification(ex.Message);
                }
            }
            finally { if (generation == _generation) _starting = false; }
        }
        public static async Task PlayNextBracket()
        {
            if (_advancing || ActiveMatch == null || _currentTournamentRequest == null) return;
            _advancing = true;
            int generation = _generation;
            try
            {
                if (_currentTournamentRequest == null)
                {
                    Debug.LogWarning("[MultiplayerController] - Cannot play next bracket: tournament not active.");
                    return;
                }

                string myId = ApiController.GetSessionUser?.Data?._id;
                string myName = ApiController.GetSessionUser?.Data?.username;
                int round = ActiveMatch != null ? ActiveMatch.round : 0;
                int index = ActiveMatch != null ? ActiveMatch.matchIndex : 0;
                if (BracketState == null || !await ApiController.FinalizeTournamentMatch(
                        _currentTournamentRequest.TournamentId, ActiveMatch.matchId, myId))
                {
                    if (generation != _generation) return;
                    TrucoConfirmDialog.Show("RESULTADO PENDIENTE", "El servidor no confirmó el resultado. No se avanzó ni se acreditó un premio.",
                        "SALIR", "REINTENTAR", () => { _ = PlayNextBracket(); },
                        () => { LeaveTournamentMatchmaking(); TrucoSceneTransition.Go("MainMenu"); });
                    return;
                }
                if (generation != _generation) return;
                BracketState.RecordWinner(round, index, myId, myName);

                _currentBracketLevel = Math.Max(0, _currentBracketLevel - 1);
                _currentPlayerCounts = Math.Max(1, _currentPlayerCounts / 2);

                int nextRound = round + 1;
                bool isFinal = BracketState == null || nextRound >= BracketState.rounds.Count;
                if (isFinal)
                {
                    int prize = _currentTournamentRequest != null ? _currentTournamentRequest.Prize : 0;
                    bool paid = await ApiController.FinalizeTournament(_currentTournamentRequest.TournamentId, myId);
                    if (generation != _generation) return;
                    TournamentChampionOverlay.Show(prize, _currentTournamentRequest?.TournamentName,
                        paid && TournamentFinalizeGuard.AlreadyDone(_currentTournamentRequest.TournamentId, "prize-paid", myId));
                    _publishBracketOnNextHub = true;
                    _pendingMatchRoomName = TournamentBracketState.HubRoomName(_currentTournamentRequest.TournamentId);
                    _isInTournamentMatch = false;
                    if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
                    return;
                }

                var next = new TournamentMatch
                {
                    round = nextRound,
                    matchIndex = index / 2,
                    matchId = null,
                    roomName = TournamentBracketState.MatchRoomName(_currentTournamentRequest.TournamentId, nextRound, index / 2),
                    playerIds = new List<string> { myId },
                    playerNames = new List<string> { myName }
                };
                ActiveMatch = next;
                _loadingTable = false;
                TrucoMatchProgress.ResetForNewMatch();
                UnityEngine.SceneManagement.SceneManager.LoadScene("TempScene");
                NextMatch = null;
                _publishBracketOnNextHub = true;
                _pendingHubThenMatch = next.roomName;
                _isInTournamentMatch = false;
                _pendingMatchRoomName = TournamentBracketState.HubRoomName(_currentTournamentRequest.TournamentId);
                TournamentBracketOverlay.Show(false);
                TournamentManager.Instance?.DisplayTournamentWaitingRoomOverlay();
                Debug.Log($"[MultiplayerController] - Hub then next table: {_pendingMatchRoomName} → {_pendingHubThenMatch}");
                if (PhotonNetwork.InRoom)
                    PhotonNetwork.LeaveRoom();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerController] - Error in PlayNextBracket: {ex.Message}");
            }
            finally { if (generation == _generation) _advancing = false; }
        }
        public static async Task FinalizeCurrentMatch(bool tournamentChampion = false)
        {
            string userID = ApiController.GetSessionUser.Data._id;

            if (!string.IsNullOrEmpty(ActiveMatch?.matchId))
            {
                bool finalizeActiveMatch = await ApiController.FinalizeTournamentMatch(CurrentTournamentRequest.TournamentId, ActiveMatch.matchId, userID);
            }

            if (!tournamentChampion) return;

            if (!string.IsNullOrEmpty(CurrentTournamentRequest.TournamentId))
            {
                bool finalizeActiveMatch = await ApiController.FinalizeTournament(CurrentTournamentRequest.TournamentId, userID);
            }
        }

        public static void SpectateAfterLoss()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Gameplay")
                UnityEngine.SceneManagement.SceneManager.LoadScene("TempScene");
            ActiveMatch = null;
            _isInTournamentMatch = false;
            _publishBracketOnNextHub = false;
            _pendingHubThenMatch = null;
            if (_currentTournamentRequest != null)
                _pendingMatchRoomName = TournamentBracketState.HubRoomName(_currentTournamentRequest.TournamentId);
            else
                _pendingMatchRoomName = null;

            ValidateMultiplayerCallbacks();
            _multiplayerCallbacks?.EnableTournamentMode();

            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
                return;
            }
            if (!string.IsNullOrEmpty(_pendingMatchRoomName) && PhotonNetwork.IsConnectedAndReady)
            {
                JoinOrCreateTournamentRoom(_pendingMatchRoomName);
                return;
            }
            if (!PhotonNetwork.IsConnected && _currentTournamentRequest != null)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
                TrucoPhotonRegionSettings.ApplyToPhoton();
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public static void LeaveTournamentMatchmaking(Action onCompleteAction = null)
        {
            _generation++;
            _starting = _loadingTable = _advancing = false;
            _currentTournamentRequest = null;
            _tournamentPlayers.Clear();
            _pendingMatchRoomName = null;
            _pendingHubThenMatch = null;
            _publishBracketOnNextHub = false;
            _currentBracketLevel = 0;
            _hasEnteredTournament = false;
            _isInTournamentMatch = false;
            _multiplayerCallbacks?.DisableTournamentMode();
            ActiveMatch = null;
            NextMatch = null;
            BracketState = null;

            TournamentBracketOverlay.Hide();
            TournamentChampionOverlay.Hide();
            TournamentManager.Instance?.HideTournamentWaitingRoomOverlay();
            TournamentManager.Instance?.HideTournamentMatchmakingUI();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            onCompleteAction?.Invoke();
        }
        private static void StartTournamentMatch() {
            if (_loadingTable || !PhotonNetwork.IsMasterClient || !TrucoMatchPlayGate.IsLiveTable()) return;
            _loadingTable = true;
            TournamentBracketOverlay.Hide();
            PhotonNetwork.LoadLevel("Gameplay");
        }

        public static void OnGameplayLoaded()
        {
            if (!IsTournamentActive || !TrucoMatchPlayGate.IsLiveTable()) return;
            _loadingTable = true;
            TournamentBracketOverlay.Hide();
            TournamentManager.Instance?.HideTournamentWaitingRoomOverlay();
            TournamentManager.Instance?.HideTournamentMatchmakingUI();
        }

        static async void EnsureNextMatchCreated()
        {
            if (_starting || !PhotonNetwork.IsMasterClient || ActiveMatch == null) return;
            var room = PhotonNetwork.CurrentRoom;
            if (room == null || room.PlayerCount != 2) return;
            _starting = true;
            int generation = _generation;
            try
            {
                var ids = room.Players.Values.OrderBy(p => p.ActorNumber)
                    .Select(p => p.CustomProperties["userId"] as string).ToList();
                if (ids.Any(string.IsNullOrEmpty) || ids.Distinct().Count() != 2) return;
                var created = await ApiController.CreateTournamentMatch(_currentTournamentRequest.TournamentId, ids);
                if (generation != _generation || PhotonNetwork.CurrentRoom != room) return;
                if (string.IsNullOrEmpty(created?._id))
                { AppManager.Instance?.DisplayNotification("No se pudo crear la próxima partida."); return; }
                room.SetCustomProperties(new Hashtable { ["backendMatchId"] = created._id });
            }
            finally { if (generation == _generation) _starting = false; }
        }

        #endregion

        #region Match Room Management
        public static void JoinSpecificTournamentMatch(TournamentMatch match, TournamentMatch upcomingMatch)
        {
            if (_currentTournamentRequest == null) return;

            Debug.Log($"[MultiplayerController] - Joining tournament match room: {match.roomName}");

            ActiveMatch = match;
            NextMatch = upcomingMatch;
            _loadingTable = false;
            TrucoMatchProgress.ResetForNewMatch();

            if (_currentTournamentRequest != null)
            {
                OneVsOneMatchSession.SetWithFlor(_currentTournamentRequest.WithFlor);
                OneVsOneMatchSession.SetTargetScore(_currentTournamentRequest.TargetScore);
            }

            _pendingMatchRoomName = match.roomName;
            _isInTournamentMatch = true;
            _hasEnteredTournament = true;

            PhotonNetwork.LeaveRoom();
        }

        public static void ValidateRoom()
        {
            if (_currentTournamentRequest == null || PhotonNetwork.CurrentRoom == null) return;
            if (_isInTournamentMatch)
            {
                if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
                    if (string.IsNullOrEmpty(ActiveMatch?.matchId))
                    { EnsureNextMatchCreated(); return; }
                    StartTournamentMatch();
                }
            }
            else
            {
                UpdatePlayerCount();
                if (PhotonNetwork.CurrentRoom.Name.Contains("TournamentMatchmaking") &&
                    PhotonNetwork.CurrentRoom.PlayerCount >= _currentTournamentRequest.MaxPlayerCount)
                {
                    StartTournamentNow();
                }
            }
        }

        #endregion

        #region Photon Callbacks
        public static async Task OnJoinedRoom()
        {

            if (_currentTournamentRequest == null) return;

            int generation = _generation;
            var joinedRoom = PhotonNetwork.CurrentRoom;
            if (_isInTournamentMatch && ActiveMatch != null)
            {
                if (joinedRoom.CustomProperties.TryGetValue("backendMatchId", out object matchId))
                    ActiveMatch.matchId = matchId as string;
                ApplyBracketFromCurrentRoom();
                PublishBracketToCurrentRoom();
                OneVsOneMatchSession.SetWithFlor(_currentTournamentRequest.WithFlor);
                OneVsOneMatchSession.SetTargetScore(_currentTournamentRequest.TargetScore);
            }

            if (PhotonNetwork.CurrentRoom.Name == _pendingMatchRoomName) _pendingMatchRoomName = null;
            Debug.Log($"[MultiplayerController] - Joined Room: {PhotonNetwork.CurrentRoom.Name} with player count: {PhotonNetwork.CurrentRoom.PlayerCount}, max player can be {PhotonNetwork.CurrentRoom.MaxPlayers}");
            ApplyOpeningTables();

            if (TournamentBracketState.IsHubRoom(PhotonNetwork.CurrentRoom.Name))
            {
                ApplyBracketFromCurrentRoom();
                if (_publishBracketOnNextHub)
                {
                    PublishBracketToCurrentRoom();
                    _publishBracketOnNextHub = false;
                }
                TournamentBracketOverlay.Show(string.IsNullOrEmpty(_pendingHubThenMatch));
                if (!string.IsNullOrEmpty(_pendingHubThenMatch))
                {
                    await Task.Delay(400);
                    if (generation != _generation || PhotonNetwork.CurrentRoom != joinedRoom) return;
                    _pendingMatchRoomName = _pendingHubThenMatch;
                    _pendingHubThenMatch = null;
                    _isInTournamentMatch = true;
                    if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
                    return;
                }
                return;
            }

            if (_isInTournamentMatch && PhotonNetwork.CurrentRoom.PlayerCount < 2)
                TournamentBracketOverlay.Show(false);
            await Task.Delay(1000);
            if (generation == _generation && PhotonNetwork.CurrentRoom == joinedRoom) ValidateRoom();
        }
        public static void OnLeftRoom()
        {
            if (_currentTournamentRequest == null) return;
            if (string.IsNullOrEmpty(_pendingMatchRoomName))
            { JoinTournamentLobby(_currentTournamentRequest); return; }
            if (!string.IsNullOrEmpty(_pendingMatchRoomName))
            {
                string roomToJoin = _pendingMatchRoomName;
                Hashtable playerProperties = new Hashtable
                {
                    ["userId"] = ApiController.GetSessionUser.Data._id,
                    ["username"] = ApiController.GetSessionUser.Data.username,
                };

                PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);
                JoinOrCreateTournamentRoom(roomToJoin);
                Debug.Log($"[MultiplayerController] - Joining tournament match room: {roomToJoin}");
            }
        }

        public static void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (_currentTournamentRequest == null) 
                return;
            
            Debug.Log($"[MultiplayerController] - Player joined match: {newPlayer.NickName}");
            if (PhotonNetwork.CurrentRoom != null && TournamentBracketState.IsHubRoom(PhotonNetwork.CurrentRoom.Name))
                return;
            ValidateRoom();
        }
        public static void OnPlayerLeftRoom(Player otherPlayer) {
            if (_currentTournamentRequest == null) return;
            Debug.Log($"[MultiplayerController] - Player left: {otherPlayer.NickName}");
            if (!_isInTournamentMatch) {
                UpdatePlayerCount(); // Reset tournament entry status if someone leaves before tournament starts
                if (!_hasEnteredTournament && PhotonNetwork.CurrentRoom.PlayerCount < _currentTournamentRequest.MaxPlayerCount) {
                    Debug.Log("[MultiplayerController] - Player left before tournament started, waiting for more players...");
                }
            }
        }

        public static void OnConnectedToMasterForTournament() {
            if (_currentTournamentRequest == null) return;

            if (!string.IsNullOrEmpty(_pendingMatchRoomName)) {
                JoinOrCreateTournamentRoom(_pendingMatchRoomName);
                Debug.Log($"[MultiplayerController] - Joining tournament match room: {_pendingMatchRoomName}");
                return;
            }

            if (_isInTournamentMatch) return;
            Debug.Log("[MultiplayerController] - Connected to Master Server, starting tournament matchmaking...");
            JoinTournamentLobby(_currentTournamentRequest);
        }

        static void JoinOrCreateTournamentRoom(string roomToJoin)
        {
            if (string.IsNullOrEmpty(roomToJoin) || _currentTournamentRequest == null) return;
            bool hub = TournamentBracketState.IsHubRoom(roomToJoin);
            Hashtable roomProperties = new Hashtable
            {
                ["tournamentId"] = _currentTournamentRequest.TournamentId,
                ["bracketLevel"] = _currentBracketLevel,
                ["withFlor"] = _currentTournamentRequest.WithFlor,
                ["targetScore"] = _currentTournamentRequest.TargetScore,
                ["isTournamentMatch"] = !hub,
                ["isTournamentHub"] = hub,
                ["matchRoomName"] = roomToJoin
            };
            if (BracketState != null)
                roomProperties["bracketJson"] = BracketState.ToJson();
            if (!hub && !string.IsNullOrEmpty(ActiveMatch?.matchId))
                roomProperties["backendMatchId"] = ActiveMatch.matchId;

            byte maxPlayers = hub ? (byte)16 : (_isInTournamentMatch ? (byte)2 : (byte)Mathf.Max(2, _currentPlayerCounts));
            var roomOptions = new RoomOptions
            {
                MaxPlayers = maxPlayers,
                PlayerTtl = 60000,
                EmptyRoomTtl = 120000,
                CustomRoomProperties = roomProperties,
                CustomRoomPropertiesForLobby = new string[] { "tournamentId", "bracketLevel", "isTournamentMatch", "isTournamentHub" }
            };
            PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, TypedLobby.Default);
        }

        #endregion

        #region Utilities
        private static Player FindPlayerByUserId(string userId)
        {
            foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
            {
                if (player.CustomProperties.TryGetValue("userId", out object playerUserId) && playerUserId.ToString() == userId)
                {
                    return player;
                }
            }
            return null;
        }

        public static int CalculateBracketLevels(int maxPlayers)
        {
            return (int)System.Math.Log(maxPlayers, 2);
        }
        private static string GetBracketTitle(int bracketLevel)
        {
            return bracketLevel switch
            {
                1 => "Finals",
                2 => "Semi-Finals",
                3 => "Quarter-Finals",
                4 => "Round of 16",
                5 => "Round of 32",
                6 => "Round of 64",
                _ => bracketLevel > 6 ? $"Round of {(int)Math.Pow(2, bracketLevel)}" : "Tournament Complete"
            };
        }

        private static void UpdatePlayerCount()
        {
            int currentCount = PhotonNetwork.CurrentRoom?.PlayerCount ?? 0;

            OnPlayerCountUpdated?.Invoke(currentCount);

        }

        #endregion
    }
}
