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
        private static int _currentBracketLevel = 0;
        private static int _currentPlayerCounts = 0;
        private static bool _hasEnteredTournament = false;
        private static bool _isInTournamentMatch = false;
        
        public static TournamentRequest CurrentTournamentRequest => _currentTournamentRequest;
        public static (int roundNumber, string bracketTitle) CurrentBracketLevel => (_currentBracketLevel, GetBracketTitle(_currentBracketLevel));
        public static bool IsTournamentActive => _currentTournamentRequest != null;
        public static TournamentMatch ActiveMatch;
        public static TournamentMatch NextMatch;

        #endregion

        #region Events
        public static event Action<int> OnPlayerCountUpdated;
        public static event Action OnTournamentBrackedMatch;
        public static event Action<string> OnTournamentMatchFinalized;
        #endregion

        #region Initialization and Setup
        public static void InitMultiplayerSession_Tournament(TournamentRequest tournamentRequest, Action<string> onErrorAction = null) {
            ValidateMultiplayerCallbacks();

            Action JoinTournamentMatchMaking = () => {
                _multiplayerCallbacks.EnableTournamentMode();
                _currentTournamentRequest = tournamentRequest;
                _currentBracketLevel = CalculateBracketLevels(tournamentRequest.MaxPlayerCount);
                _currentPlayerCounts = tournamentRequest.MaxPlayerCount;
                _hasEnteredTournament = false;
                _isInTournamentMatch = false;
                PhotonNetwork.NickName = ApiController.GetSessionUser.Data.username;
                JoinTournamentLobby(tournamentRequest);
            };

            if (!PhotonNetwork.IsConnectedAndReady) {
                Debug.Log("[MultiplayerController] - Connecting to Photon...");
                PhotonNetwork.AutomaticallySyncScene = true;
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
        private async static void StartTournamentNow() 
        {
            try
            {
                OnTournamentBrackedMatch?.Invoke();

                await Task.Delay(1000 * (PhotonNetwork.LocalPlayer.ActorNumber + 1));

                bool enteredTournament = await ApiController.EnterTournament(_currentTournamentRequest.TournamentId, null, (err) =>
                {
                    AppManager.Instance.DisplayNotification(err, () =>
                    {
                        LeaveTournamentMatchmaking(() =>
                        {
                            TournamentManager.Instance.HideTournamentWaitingRoomOverlay();
                        });
                    });
                });

                await Task.Delay(5000);

                if (!PhotonNetwork.IsMasterClient || _hasEnteredTournament || PhotonNetwork.CurrentRoom.PlayerCount < _currentTournamentRequest.MaxPlayerCount) return;

                _tournamentPlayers.Clear();

                foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
                {
                    if (player.CustomProperties.TryGetValue("userId", out object userId) &&
                        player.CustomProperties.TryGetValue("username", out object username))
                    {
                        _tournamentPlayers.Add(new User
                        {
                            _id = userId.ToString(),
                            username = username.ToString()
                        });
                    }
                }


                // === NEXT ROUND ===
                TournamentMatch nextMatch = new TournamentMatch();

                if (_currentBracketLevel > 1)
                {
                    string nextTitle = GetBracketTitle(_currentBracketLevel - 1);

                    string combinedNames = string.Join("_", _tournamentPlayers.Select(p => p.username));
                    string nextRoom = $"{nextTitle}_Lobby_{combinedNames}_{_currentTournamentRequest.TournamentId}";

                    nextMatch = new TournamentMatch
                    {
                        matchId = $"{nextTitle}_Lobby_{_currentTournamentRequest.TournamentId}",
                        roomName = nextRoom,
                        playerIds = _tournamentPlayers.Select(p => p._id).ToList(),
                        playerNames = _tournamentPlayers.Select(p => p.username).ToList()
                    };
                }

                // === CURRENT ROUND ===
                string currentTitle = GetBracketTitle(_currentBracketLevel);

                for (int i = 0; i < _tournamentPlayers.Count; i += 2)
                {
                    var matchPlayers = _tournamentPlayers.Skip(i).Take(2).ToList();

                    if (matchPlayers.Count < 2) continue;

                    string p1 = matchPlayers[0]._id;
                    string p2 = matchPlayers[1]._id;

                    string targetPlayer = p1;
                    string opponentPlayer = p2;

                    if(targetPlayer == ApiController.GetSessionUser.Data._id && PhotonNetwork.IsMasterClient)
                    {

                    }
                    else if(opponentPlayer == ApiController.GetSessionUser.Data._id && PhotonNetwork.IsMasterClient)
                    {
                        targetPlayer = p2;
                        opponentPlayer = p1;
                    }

                    _multiplayerRPC.photonView.RPC("CreateTournamentMatch", FindPlayerByUserId(targetPlayer), opponentPlayer, JsonUtility.ToJson(nextMatch));

                    //    MatchDto createdMatchViaApi = await ApiController.CreateTournamentMatch(_currentTournamentRequest.TournamentId, new List<string> { p1, p2 });

                    //    if (createdMatchViaApi == null)
                    //    {

                    //        int totalLetters = p1.Length + p2.Length;
                    //        string uniqueCode = System.Guid.NewGuid().ToString("N").Substring(0, 6);
                    //        string roomName = $"{currentTitle}_{uniqueCode}_{p1}Vs{p2}_{totalLetters}";
                    //        string matchId = $"{currentTitle}_{p1}Vs{p2}_{_currentTournamentRequest.TournamentId}";

                    //        currentMatches.Add(new TournamentMatch
                    //        {
                    //            matchId = matchId,
                    //            roomName = roomName,
                    //            playerIds = new List<string> { matchPlayers[0]._id, matchPlayers[1]._id },
                    //            playerNames = new List<string> { p1, p2 }
                    //        });
                    //    }
                    //    else
                    //    {
                    //        currentMatches.Add(new TournamentMatch
                    //        {
                    //            matchId = createdMatchViaApi._id,
                    //            roomName = createdMatchViaApi._id,
                    //            playerIds = new List<string> { matchPlayers[0]._id, matchPlayers[1]._id },
                    //            playerNames = new List<string> { p1, p2 }
                    //        });
                    //    }
                    //}

                    //allBrackets.Add(new TournamentBracket {
                    //    bracketTitle = currentTitle,
                    //    matches = currentMatches
                    //});



                    //// === SEND FULL BRACKET TO ALL ===
                    //CurrentTournamentFullBracket = new TournamentFullBracket {
                    //    tournamentId = _currentTournamentRequest.TournamentId,
                    //    brackets = allBrackets
                }

                //string fullJson = JsonUtility.ToJson(CurrentTournamentFullBracket);
                //_multiplayerRPC.photonView.RPC("ReceiveTournamentFullBracket", RpcTarget.Others, fullJson);


                //LeanTween.delayedCall(3, () =>
                //{
                //    _multiplayerRPC.photonView.RPC("ReceiveTournamentFullBracket", RpcTarget.MasterClient, fullJson);
                //});

            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerController] - Error starting tournament: {ex.Message}");
            }
        }
        public static async Task PlayNextBracket()
        {
            try
            {
                if (_currentTournamentRequest == null)
                {
                    Debug.LogWarning("[MultiplayerController] - Cannot play next bracket: tournament not active.");
                    return;
                }

                _currentBracketLevel = Math.Max(0, _currentBracketLevel - 1);
                _currentPlayerCounts = Math.Max(1, _currentPlayerCounts / 2);
                _pendingMatchRoomName = string.Empty;

                TournamentManager.Instance.DisplayTournamentWaitingRoomOverlay();

                await Task.Delay(2000);

                await FinalizeCurrentMatch();

                if (NextMatch != null)
                {

                    if (_currentBracketLevel > 1)
                    {
                        _isInTournamentMatch = false;
                    }

                    ActiveMatch = NextMatch;
                    _pendingMatchRoomName = NextMatch.roomName;
                    NextMatch = null;

                    Debug.Log($"[MultiplayerController] - Proceeding to next bracket: {_pendingMatchRoomName}");

                    if (PhotonNetwork.InRoom)
                        PhotonNetwork.LeaveRoom();
                }
                else
                {
                    Debug.Log("[MultiplayerController] - No brackets or matches found in CurrentTournamentFullBracket.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiplayerController] - Error in PlayNextBracket: {ex.Message}");
            }
        }
        public static async Task FinalizeCurrentMatch(bool tournamentChampion = false)
        {
            string userID = ApiController.GetSessionUser.Data._id;

            if (!string.IsNullOrEmpty(ActiveMatch.matchId))
            {
                bool finalizeActiveMatch = await ApiController.FinalizeTournamentMatch(CurrentTournamentRequest.TournamentId, ActiveMatch.matchId, userID);
            }

            if (!tournamentChampion) return;

            if (!string.IsNullOrEmpty(CurrentTournamentRequest.TournamentId))
            {
                bool finalizeActiveMatch = await ApiController.FinalizeTournament(CurrentTournamentRequest.TournamentId, userID);
            }
        }

        public static void LeaveTournamentMatchmaking(Action onCompleteAction = null)
        {

            _currentTournamentRequest = null;
            _tournamentPlayers.Clear();
            _pendingMatchRoomName = null;
            _currentBracketLevel = 0;
            _hasEnteredTournament = false;
            _isInTournamentMatch = false;
            _multiplayerCallbacks?.DisableTournamentMode();
            ActiveMatch = null;
            NextMatch = null;

            if (!PhotonNetwork.InRoom)
            {
                onCompleteAction?.Invoke();
            }
            else if (PhotonNetwork.LeaveRoom())
            {
                PhotonNetwork.Disconnect();
                Debug.Log("[MultiplayerController] - Left tournament room.");
                onCompleteAction?.Invoke();
            }
        }
        private static void StartTournamentMatch() {
            if (!PhotonNetwork.IsMasterClient) return;
            PhotonNetwork.LoadLevel("Gameplay");
        }

        #endregion

        #region Match Room Management
        public static void JoinSpecificTournamentMatch(TournamentMatch match, TournamentMatch upcomingMatch)
        {
            if (_currentTournamentRequest == null) return;

            Debug.Log($"[MultiplayerController] - Joining tournament match room: {match.roomName}");

            ActiveMatch = match;
            NextMatch = upcomingMatch;

            _pendingMatchRoomName = match.roomName;
            _isInTournamentMatch = true;
            _hasEnteredTournament = true;

            PhotonNetwork.LeaveRoom();
        }

        private static void ValidateRoom()
        {
            if (_isInTournamentMatch)
            {
                if (PhotonNetwork.CurrentRoom.PlayerCount >= 2)
                {
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

            DataHandler.Instance.points = 0;
            DataHandler.Instance.roundNumber = 0;

            if (PhotonNetwork.CurrentRoom.Name == _pendingMatchRoomName) _pendingMatchRoomName = null;
            Debug.Log($"[MultiplayerController] - Joined Room: {PhotonNetwork.CurrentRoom.Name} with player count: {PhotonNetwork.CurrentRoom.PlayerCount}, max player can be {PhotonNetwork.CurrentRoom.MaxPlayers}");
            ValidateMultiplayerRPC(); // Initialize RPC system FIRST
            await Task.Delay(1000);
            ValidateRoom(); // Then validate the room and potentially start tournament
        }
        public static void OnLeftRoom()
        {
            if (_currentTournamentRequest == null) return;
            if (!string.IsNullOrEmpty(_pendingMatchRoomName))
            {
                string roomToJoin = _pendingMatchRoomName;
                Hashtable roomProperties = new Hashtable
                {
                    ["tournamentId"] = _currentTournamentRequest.TournamentId,
                    ["bracketLevel"] = _currentBracketLevel,
                    ["isTournamentMatch"] = true,
                    ["matchRoomName"] = roomToJoin
                };

                Hashtable playerProperties = new Hashtable
                {
                    ["userId"] = ApiController.GetSessionUser.Data._id,
                    ["username"] = ApiController.GetSessionUser.Data.username,
                };

                PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

                RoomOptions roomOptions = new RoomOptions
                {
                    MaxPlayers = _isInTournamentMatch ? 2 : _currentPlayerCounts, // Tournament matches are 1v1
                    CustomRoomProperties = roomProperties,
                    CustomRoomPropertiesForLobby = new string[] { "tournamentId", "bracketLevel", "isTournamentMatch" }
                };

                PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, TypedLobby.Default);
                Debug.Log($"[MultiplayerController] - Joining tournament match room: {roomToJoin}");
            }
        }

        public static void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (_currentTournamentRequest == null) 
                return;
            
            Debug.Log($"[MultiplayerController] - Player joined match: {newPlayer.NickName}");
            
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
                string roomToJoin = _pendingMatchRoomName;
                Hashtable roomProperties = new Hashtable {
                    ["tournamentId"] = _currentTournamentRequest.TournamentId,
                    ["bracketLevel"] = _currentBracketLevel,
                    ["isTournamentMatch"] = true,
                    ["matchRoomName"] = roomToJoin
                };

                RoomOptions roomOptions = new RoomOptions {
                    MaxPlayers = _isInTournamentMatch ?  2 : _currentPlayerCounts, // Tournament matches are 1v1
                    CustomRoomProperties = roomProperties,
                    CustomRoomPropertiesForLobby = new string[] { "tournamentId", "bracketLevel", "isTournamentMatch" }
                };

                PhotonNetwork.JoinOrCreateRoom(roomToJoin, roomOptions, TypedLobby.Default);
                Debug.Log($"[MultiplayerController] - Joining tournament match room: {roomToJoin}");
                return;
            }

            Debug.Log("[MultiplayerController] - Connected to Master Server, starting tournament matchmaking...");
            JoinTournamentLobby(_currentTournamentRequest);
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