namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using Hashtable = ExitGames.Client.Photon.Hashtable;

    /// <summary>Backend snapshots own registration, pairings, results and settlement. Photon transports assigned tables only.</summary>
    public static class MultiplayerController
    {
        static TournamentRequest _currentTournamentRequest;
        static MultiplayerCallbacks _multiplayerCallbacks;
        static MultiplayerRPC _multiplayerRPC;
        static string _pendingMatchRoomName;
        static int _generation;
        static bool _joining, _starting, _refreshing, _advancing, _leaving, _loadingTable, _isInTournamentMatch, _finishing;
        static string _presentedAssignment, _shownPayout;
        public static TournamentRequest CurrentTournamentRequest => _currentTournamentRequest;
        public static TrucoTournamentService.Snapshot CurrentSnapshot { get; private set; }
        public static bool IsTournamentActive => _currentTournamentRequest != null;
        public static bool HasSavedTournament => !string.IsNullOrEmpty(TrucoTournamentSessionStore.Remembered());
        public static bool LastLeaveConfirmed { get; private set; }
        public static TournamentMatch ActiveMatch;
        public static TournamentMatch NextMatch;
        public static TournamentBracketState BracketState { get; private set; }
        public static (int roundNumber, string bracketTitle) CurrentBracketLevel
        {
            get
            {
                int remaining = Math.Max(0, CalculateBracketLevels(_currentTournamentRequest?.MaxPlayerCount ?? 4) - (ActiveMatch?.round ?? 0));
                return (remaining, remaining == 1 ? "Finals" : remaining == 2 ? "Semi-Finals" : remaining == 3 ? "Quarter-Finals" : "Tournament Complete");
            }
        }
        public static event Action<int> OnPlayerCountUpdated;
        public static event Action OnTournamentBrackedMatch;
        public static event Action<string> OnTournamentMatchFinalized;

        // Compatibility entry points intentionally cannot turn Photon JSON into authority.
        public static void ReplaceBracketState(TournamentBracketState state) => _ = RefreshAuthoritativeStateAsync();
        public static void ApplyBracketFromCurrentRoom() => _ = RefreshAuthoritativeStateAsync();
        public static void PublishBracketToCurrentRoom()
        {
            if (PhotonNetwork.InRoom && CurrentSnapshot != null)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { ["tournamentRevision"] = CurrentSnapshot.revision });
        }
        public static void OnRoomPropertiesUpdate(Hashtable properties)
        {
            if (!IsTournamentActive || properties == null) return;
            if (properties.ContainsKey("tournamentRevision") || properties.ContainsKey("bracketJson") || properties.ContainsKey("openingTables"))
                _ = RefreshAuthoritativeStateAsync();
            ValidateRoom();
        }

        public static async void InitMultiplayerSession_Tournament(TournamentRequest request, Action<string> onErrorAction = null)
        {
            if (_joining || IsTournamentActive) return;
            if (request == null || string.IsNullOrWhiteSpace(request.TournamentId) || request.MaxPlayerCount != 4 && request.MaxPlayerCount != 8)
            { onErrorAction?.Invoke("El torneo debe tener un identificador válido y 4 u 8 jugadores."); return; }
            _joining = true;
            TrucoTournamentSessionStore.SuppressAutomaticResume = false;
            int generation = ++_generation;
            try
            {
                // Verify the versioned read contract before a registration can debit anything.
                var before = await TrucoTournamentService.Get(request.TournamentId);
                if (generation != _generation) return;
                bool alreadyRegistered = before.participants.Any(p => p.playerId == LocalId && p.status != "withdrawn");
                OneVsOnePhotonFlow.Instance?.ResetPurpose();
                if (!alreadyRegistered && !await TrucoPhotonAuthentication.ConnectAndWaitUntilReadyAsync("tournament", request.TournamentId))
                    throw new InvalidOperationException(TrucoPhotonAuthentication.LastError);
                if (generation != _generation) return;
                _currentTournamentRequest = RequestFrom(before);
                CurrentSnapshot = before;
                TrucoTournamentSessionStore.Remember(before.tournamentId);
                ValidateMultiplayerCallbacks(); _multiplayerCallbacks.EnableTournamentMode();
                TrucoTournamentSessionPoller.Ensure();
                var joined = alreadyRegistered ? before : await TrucoTournamentService.Join(before);
                if (generation != _generation) return;
                ApplySnapshot(joined, force: true);
            }
            catch (Exception exception)
            {
                if (generation != _generation) return;
                if (IsTournamentActive) ShowPending("La inscripción requiere confirmación del servidor. Consultá el estado antes de volver a intentar.");
                onErrorAction?.Invoke(exception.Message);
            }
            finally { if (generation == _generation) _joining = false; }
        }
        static string LocalId => ApiController.GetSessionUser?.Data?._id;
        static TournamentRequest RequestFrom(TrucoTournamentService.Snapshot snapshot) => new TournamentRequest(
            snapshot.tournamentId, snapshot.maxPlayers, snapshot.entryFee, snapshot.name, snapshot.advertisedPrize, snapshot.withFlor, snapshot.targetScore);

        public static async Task<bool> OpenFromMenuAsync()
        {
            if (!IsTournamentActive) return await TryResumeTournamentAsync(userInitiated: true);
            await RefreshAuthoritativeStateAsync();
            if (CurrentSnapshot != null) ApplySnapshot(CurrentSnapshot, force: true);
            return IsTournamentActive;
        }

        public static async Task<bool> TryResumeTournamentAsync(bool userInitiated = false)
        {
            if (IsTournamentActive || _joining || string.IsNullOrEmpty(LocalId)) return false;
            if (!userInitiated && TrucoTournamentSessionStore.SuppressAutomaticResume) return false;
            string id = TrucoTournamentSessionStore.Remembered();
            if (string.IsNullOrEmpty(id)) return false;
            int generation = ++_generation; _joining = true;
            try
            {
                var snapshot = await TrucoTournamentService.Get(id);
                if (generation != _generation) return false;
                if (snapshot.localStatus == "not_registered" || snapshot.localStatus == "withdrawn")
                {
                    if (!HasPendingSettlement(snapshot)) TrucoTournamentSessionStore.Forget();
                    else
                    {
                        _currentTournamentRequest = RequestFrom(snapshot); CurrentSnapshot = null;
                        TrucoTournamentSessionPoller.Ensure(); ApplySnapshot(snapshot, force: true);
                        return true;
                    }
                    return false;
                }
                _currentTournamentRequest = RequestFrom(snapshot);
                ValidateMultiplayerCallbacks(); _multiplayerCallbacks.EnableTournamentMode();
                TrucoTournamentSessionPoller.Ensure(); ApplySnapshot(snapshot, force: true);
                return true;
            }
            catch (Exception exception) { Debug.LogWarning("Tournament resume is pending: " + exception.GetType().Name); return false; }
            finally { if (generation == _generation) _joining = false; }
        }
        public static async Task RefreshAuthoritativeStateAsync()
        {
            if (_refreshing || !IsTournamentActive || _joining || _leaving) return;
            int generation = _generation; var previous = CurrentSnapshot; _refreshing = true;
            try
            {
                var snapshot = await TrucoTournamentService.Get(_currentTournamentRequest.TournamentId, previous);
                if (generation == _generation) ApplySnapshot(snapshot);
            }
            catch (Exception exception) { Debug.LogWarning("Tournament state refresh pending: " + exception.GetType().Name); }
            finally { if (generation == _generation) _refreshing = false; }
        }
        static void ApplySnapshot(TrucoTournamentService.Snapshot snapshot, bool force = false)
        {
            if (_currentTournamentRequest == null || snapshot.tournamentId != _currentTournamentRequest.TournamentId) return;
            if (CurrentSnapshot != null && snapshot.revision < CurrentSnapshot.revision) return;
            TrucoTournamentService.Validate(snapshot, _currentTournamentRequest.TournamentId, LocalId, CurrentSnapshot);
            bool changed = CurrentSnapshot == null || snapshot.revision != CurrentSnapshot.revision || force;
            CurrentSnapshot = snapshot; _currentTournamentRequest = RequestFrom(snapshot);
            if (!changed) return;
            BracketState = TrucoTournamentService.ToBracket(snapshot);
            OnPlayerCountUpdated?.Invoke(snapshot.participants.Count(p => p.status != "withdrawn"));
            OneVsOneMatchSession.SetWithFlor(snapshot.withFlor); OneVsOneMatchSession.SetTargetScore(snapshot.targetScore);
            PublishBracketToCurrentRoom();
            if (snapshot.localStatus == "not_registered")
            { ShowPending(HasPendingSettlement(snapshot) ? "El servidor mantiene un reembolso pendiente de confirmación." : "El servidor todavía no confirmó tu inscripción. No se confirmó un cobro ni una mesa."); return; }
            if (snapshot.status == "cancelled")
            {
                StopTableForServerState();
                var refund = snapshot.refunds.FirstOrDefault(r => r.playerId == LocalId);
                string copy = refund?.status == "refunded" ? "Reembolso confirmado por el servidor: " + refund.amount + " Trucoins."
                    : refund?.status == "not_due" ? "El servidor confirmó que no corresponde un reembolso." : "El reembolso sigue pendiente de confirmación del servidor.";
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.Cancelled, copy);
                return;
            }
            if (snapshot.localStatus == "withdrawn")
            { StopTableForServerState(); ShowPending("La salida fue confirmada. Cualquier reembolso pendiente requiere su recibo del servidor."); return; }
            if (snapshot.status == "reconciliation_required") { ShowPending("El servidor está conciliando el resultado y cualquier reembolso."); return; }
            if (snapshot.status == "registration")
            {
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.Filling);
                if (snapshot.participants.Count == snapshot.maxPlayers && snapshot.participants.All(p => p.entryPaid && p.status == "registered"))
                    _ = StartTournamentNowAsync();
                return;
            }
            if (snapshot.status == "starting") { TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.CreatingMatches); return; }
            var final = snapshot.matches.FirstOrDefault(m => m.round == (snapshot.maxPlayers == 8 ? 2 : 1) && m.index == 0);
            if (snapshot.status == "completed")
            {
                StopTableForServerState();
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.Completed);
                if (final?.winnerId == LocalId)
                {
                    string receipt = snapshot.payout?.payoutId + ":" + snapshot.payout?.status;
                    if (_shownPayout != receipt)
                    {
                        _shownPayout = receipt;
                        bool paid = TrucoTournamentService.HasPaidPayout(snapshot);
                        TournamentChampionOverlay.ShowAuthoritativePayout(paid ? snapshot.payout.amount.Value : 0, snapshot.name,
                            paid, paid ? snapshot.payout.payoutId : null, snapshot.payout?.status);
                    }
                }
                else TournamentBracketOverlay.Show(true);
                return;
            }
            if (final?.status == "finished" && final.winnerId == LocalId) { _ = FinalizeTournamentAsync(); return; }
            var assigned = TrucoTournamentService.AssignedTable(snapshot);
            if (assigned != null)
            {
                JoinSpecificTournamentMatch(assigned, null);
                return;
            }
            StopTableForServerState();
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.WaitingNextRound);
            TournamentBracketOverlay.Show(snapshot.localStatus == "eliminated");
        }
        static async Task StartTournamentNowAsync()
        {
            if (_starting || CurrentSnapshot == null || CurrentSnapshot.status != "registration") return;
            int generation = _generation; _starting = true;
            try
            {
                var snapshot = await TrucoTournamentService.Start(CurrentSnapshot);
                if (generation == _generation) { OnTournamentBrackedMatch?.Invoke(); ApplySnapshot(snapshot, force: true); }
            }
            catch (Exception exception) { if (generation == _generation) ShowPending("No se confirmó el inicio. " + exception.Message); }
            finally { if (generation == _generation) _starting = false; }
        }
        public static async Task PlayNextBracket()
        {
            if (_advancing || ActiveMatch == null || CurrentSnapshot == null) return;
            _advancing = true; int generation = _generation;
            var table = ActiveMatch;
            try
            {
                var game = GameManager.Instance;
                var snapshot = await TrucoTournamentService.FinalizeMatch(CurrentSnapshot, table.matchId, LocalId, "score",
                    game?.myPlayerScoreHandler?.GetCurrentScore(), game?.otherPlayerScoreHandler?.GetCurrentScore(), DataHandler.Instance?.roundNumber);
                if (generation != _generation) return;
                var result = snapshot.matches.FirstOrDefault(m => m.matchId == table.matchId);
                if (result?.status != "finished")
                {
                    ApplySnapshot(snapshot);
                    if (snapshot.status != "cancelled") ShowPending("El servidor todavía no confirmó tu resultado. No se avanzó ni se acreditó un premio.");
                    return;
                }
                if (result.winnerId != LocalId)
                { ApplySnapshot(snapshot, force: true); AppManager.Instance?.DisplayNotification("El servidor confirmó el resultado. No clasificaste a la siguiente ronda."); return; }
                OnTournamentMatchFinalized?.Invoke(snapshot.tournamentId);
                ApplySnapshot(snapshot, force: true);
            }
            catch (Exception)
            {
                if (generation == _generation)
                    TrucoConfirmDialog.Show("RESULTADO PENDIENTE", "El servidor no confirmó el resultado. Reintentar usa la misma solicitud; no se acreditó un premio local.",
                        "CONSULTAR ESTADO", "REINTENTAR", () => { _ = PlayNextBracket(); }, () => { _ = RefreshAuthoritativeStateAsync(); });
            }
            finally { if (generation == _generation) _advancing = false; }
        }
        static async Task FinalizeTournamentAsync()
        {
            if (_finishing || CurrentSnapshot == null) return;
            var final = CurrentSnapshot.matches.FirstOrDefault(m => m.round == (CurrentSnapshot.maxPlayers == 8 ? 2 : 1) && m.index == 0);
            if (final?.status != "finished" || final.winnerId != LocalId) return;
            int generation = _generation; _finishing = true;
            try
            {
                var snapshot = await TrucoTournamentService.Finalize(CurrentSnapshot);
                if (generation == _generation) ApplySnapshot(snapshot, force: true);
            }
            catch (Exception) { if (generation == _generation) ShowPending("La final y el pago requieren confirmación del servidor."); }
            finally { if (generation == _generation) _finishing = false; }
        }
        public static async Task FinalizeCurrentMatch(bool tournamentChampion = false)
        {
            await PlayNextBracket();
            if (tournamentChampion && CurrentSnapshot != null) await FinalizeTournamentAsync();
        }
        public static async Task ReconcileTournamentAfterDisconnectAsync(string reason = "disconnect_timeout")
        {
            if (CurrentSnapshot == null) return;
            int generation = _generation;
            GameManager.Instance?.SetCanPlayCard(false); UIMANAGER.Instance?.DisableButtons();
            ShowPending("Confirmando la reconexión y el resultado con el servidor…");
            try
            {
                var snapshot = await TrucoTournamentService.Reconcile(CurrentSnapshot, reason);
                if (generation == _generation) ApplySnapshot(snapshot, force: true);
            }
            catch (Exception) { if (generation == _generation) ShowPending("No se pudo confirmar el resultado de la desconexión. No se asignó un ganador ni un reembolso local."); }
        }
        public static async Task<bool> LeaveTournamentByPlayerAsync(Action onCompleteAction = null)
        {
            if (_leaving) return false;
            if (CurrentSnapshot == null) { LeaveTournamentMatchmaking(onCompleteAction); return true; }
            int generation = _generation; _leaving = true; LastLeaveConfirmed = false;
            TrucoTournamentSessionStore.SuppressAutomaticResume = true;
            try
            {
                var snapshot = await TrucoTournamentService.Leave(CurrentSnapshot);
                if (generation != _generation) return false;
                CurrentSnapshot = snapshot;
                LastLeaveConfirmed = snapshot.localStatus == "not_registered" || snapshot.localStatus == "withdrawn" || snapshot.status == "completed" || snapshot.status == "cancelled";
                bool pendingSettlement = HasPendingSettlement(snapshot);
                if (LastLeaveConfirmed && !HasPendingSettlement(snapshot)) TrucoTournamentSessionStore.Forget();
                LeaveTournamentMatchmaking(onCompleteAction);
                if (!LastLeaveConfirmed) AppManager.Instance?.DisplayNotification("Saliste de la vista. La inscripción y cualquier reembolso siguen pendientes de conciliación.");
                else if (pendingSettlement) AppManager.Instance?.DisplayNotification("Salida confirmada. El pago o reembolso todavía está pendiente de confirmación del servidor.");
                return true;
            }
            catch (Exception)
            {
                if (generation != _generation) return false;
                LeaveTournamentMatchmaking(onCompleteAction);
                AppManager.Instance?.DisplayNotification("Saliste de la vista. El servidor no confirmó la salida ni un reembolso; el torneo se conserva para consultar su estado.");
                return true;
            }
            finally { if (generation == _generation) _leaving = false; }
        }
        static bool HasPendingSettlement(TrucoTournamentService.Snapshot snapshot)
        {
            var refund = snapshot.refunds.FirstOrDefault(r => r.playerId == LocalId);
            bool cancelledPaid = snapshot.status == "cancelled" && snapshot.entryFee > 0
                && snapshot.participants.Any(p => p.playerId == LocalId && p.entryPaid);
            return refund?.status == "pending" || cancelledPaid && (refund?.status != "refunded"
                || refund.amount != snapshot.entryFee || string.IsNullOrWhiteSpace(refund.refundId))
                || snapshot.localStatus == "champion" && snapshot.payout?.status != "paid" && snapshot.payout?.status != "not_due";
        }
        static void ShowPending(string message) => TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.Reconciling, message);
        static void StopTableForServerState()
        {
            if (ActiveMatch == null && !_isInTournamentMatch) return;
            GameManager.Instance?.SetCanPlayCard(false); UIMANAGER.Instance?.DisableButtons(); TurnManager.Instance?.StopAllTurnTimers();
            _isInTournamentMatch = false; _loadingTable = false; _pendingMatchRoomName = null; ActiveMatch = null; _presentedAssignment = null;
            if (SceneManager.GetActiveScene().name == "Gameplay") SceneManager.LoadScene("TempScene");
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
        }
        public static void SpectateAfterLoss()
        {
            StopTableForServerState();
            TournamentBracketOverlay.Show(true);
            _ = RefreshAuthoritativeStateAsync();
        }
        /// <summary>Local cleanup only. User cancellation must use LeaveTournamentByPlayerAsync.</summary>
        public static void LeaveTournamentMatchmaking(Action onCompleteAction = null)
        {
            _generation++; _joining = _starting = _refreshing = _advancing = _leaving = _loadingTable = _isInTournamentMatch = _finishing = false;
            _currentTournamentRequest = null; CurrentSnapshot = null; ActiveMatch = NextMatch = null; BracketState = null;
            _pendingMatchRoomName = _presentedAssignment = _shownPayout = null;
            _multiplayerCallbacks?.DisableTournamentMode();
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.None); TournamentBracketOverlay.Hide(); TournamentChampionOverlay.Hide();
            TournamentManager.Instance?.HideTournamentWaitingRoomOverlay(); TournamentManager.Instance?.HideTournamentMatchmakingUI();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false);
            onCompleteAction?.Invoke();
        }
        /// <summary>Account-boundary cleanup retains a pending account's durable resume/reconciliation key.</summary>
        public static void ClearLocalSession()
        {
            if (IsTournamentActive) StopTableForServerState();
            LeaveTournamentMatchmaking();
        }
        public static void ValidateMultiplayerCallbacks()
        {
            if (_multiplayerCallbacks != null) return;
            var go = new GameObject("[MultiplayerCallbacks]"); _multiplayerCallbacks = go.AddComponent<MultiplayerCallbacks>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        public static void ValidateMultiplayerRPC()
        {
            if (_multiplayerRPC != null) return;
            var go = PhotonNetwork.Instantiate("MultiplayerRPC", Vector3.zero, Quaternion.identity);
            _multiplayerRPC = go.GetComponent<MultiplayerRPC>(); UnityEngine.Object.DontDestroyOnLoad(go);
        }
        public static void JoinSpecificTournamentMatch(TournamentMatch table, TournamentMatch upcomingMatch)
        {
            var serverTable = TrucoTournamentService.AssignedTable(CurrentSnapshot);
            if (_currentTournamentRequest == null || serverTable == null || table == null || table.matchId != serverTable.matchId
                || table.roomName != serverTable.roomName || table.round != serverTable.round || table.matchIndex != serverTable.matchIndex
                || !TournamentStartupValidation.IsValidTableAssignment(table, _currentTournamentRequest.TournamentId,
                    _currentTournamentRequest.MaxPlayerCount, LocalId, BracketState)) return;
            if (_presentedAssignment == table.matchId && ActiveMatch?.matchId == table.matchId)
            {
                ValidateRoom();
                bool idleTransport = PhotonNetwork.NetworkClientState == ClientState.Disconnected
                    || PhotonNetwork.NetworkClientState == ClientState.PeerCreated
                    || PhotonNetwork.IsConnectedAndReady && PhotonNetwork.Server == ServerConnection.MasterServer;
                if (!PhotonNetwork.InRoom && !_loadingTable && !TrucoPhotonAuthentication.IsAuthenticating
                    && idleTransport && _pendingMatchRoomName == serverTable.roomName)
                    _ = ConnectAssignedTransportAsync(_generation, serverTable);
                return;
            }
            if (SceneManager.GetActiveScene().name == "Gameplay") SceneManager.LoadScene("TempScene");
            ActiveMatch = serverTable; NextMatch = null; _presentedAssignment = table.matchId;
            _isInTournamentMatch = true; _loadingTable = false; _pendingMatchRoomName = table.roomName;
            TrucoMatchProgress.ResetForNewMatch();
            ValidateMultiplayerCallbacks(); _multiplayerCallbacks.EnableTournamentMode();
            PhotonNetwork.AutomaticallySyncScene = true; PhotonNetwork.NickName = ApiController.GetSessionUser?.Data?.username ?? "Jugador";
            OneVsOnePhotonFlow.Instance?.ResetPurpose();
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable);
            _ = ConnectAssignedTransportAsync(_generation, serverTable);
        }
        static async Task ConnectAssignedTransportAsync(int generation, TournamentMatch table)
        {
            bool started = await TrucoPhotonAuthentication.ConnectUsingSettingsAsync("tournament", _currentTournamentRequest.TournamentId,
                table.matchId, table.roomName);
            if (generation != _generation || ActiveMatch?.matchId != table.matchId) return;
            if (!started) { ShowPending(TrucoPhotonAuthentication.LastError); return; }
            if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == table.roomName)
            { _pendingMatchRoomName = null; ValidateRoom(); }
            else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom) JoinAssignedRoom();
        }
        static void JoinAssignedRoom()
        {
            var assignment = TrucoTournamentService.AssignedTable(CurrentSnapshot);
            if (assignment == null || ActiveMatch?.matchId != assignment.matchId || string.IsNullOrEmpty(_pendingMatchRoomName)
                || !TrucoPhotonAuthentication.IsAuthorizedForRoom("tournament", CurrentSnapshot.tournamentId, assignment.matchId, assignment.roomName)
                || PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["userId"] = LocalId,
                ["username"] = ApiController.GetSessionUser?.Data?.username, ["isTournamentPlayer"] = true });
            var properties = new Hashtable { ["tournamentId"] = CurrentSnapshot.tournamentId, ["backendMatchId"] = assignment.matchId,
                ["isTournamentMatch"] = true, ["withFlor"] = CurrentSnapshot.withFlor, ["targetScore"] = CurrentSnapshot.targetScore,
                ["tournamentRevision"] = CurrentSnapshot.revision };
            PhotonNetwork.JoinOrCreateRoom(assignment.roomName, new RoomOptions { MaxPlayers = 2, PublishUserId = true, PlayerTtl = 60000,
                EmptyRoomTtl = 120000, CustomRoomProperties = properties,
                CustomRoomPropertiesForLobby = new[] { "tournamentId", "backendMatchId", "isTournamentMatch" } }, TypedLobby.Default);
        }
        public static void ValidateRoom()
        {
            if (!_isInTournamentMatch || _loadingTable || ActiveMatch == null || CurrentSnapshot?.localAssignment == null
                || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.Name != ActiveMatch.roomName) return;
            var assignment = TrucoTournamentService.AssignedTable(CurrentSnapshot);
            var players = PhotonNetwork.CurrentRoom.Players.Values.Where(p => !p.IsInactive && !PhotonPlayerHelper.IsSpectatorPlayer(p)).ToList();
            if (assignment == null || assignment.matchId != ActiveMatch.matchId || players.Count != 2
                || players.Select(p => p.UserId).Distinct().Count() != 2
                || players.Any(p => string.IsNullOrWhiteSpace(p.UserId) || !assignment.playerIds.Contains(p.UserId))) return;
            if (!TrucoMatchPlayGate.IsLiveTable() || !PhotonNetwork.IsMasterClient) return;
            _loadingTable = true; TournamentBracketOverlay.Hide();
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable); PhotonNetwork.LoadLevel("Gameplay");
        }
        public static void OnGameplayLoaded()
        {
            if (!IsTournamentActive || !TrucoMatchPlayGate.IsLiveTable()) return;
            _loadingTable = true; TournamentBracketOverlay.Hide();
            TournamentManager.Instance?.HideTournamentWaitingRoomOverlay(); TournamentManager.Instance?.HideTournamentMatchmakingUI();
        }
        public static Task OnJoinedRoom()
        {
            if (!IsTournamentActive) return Task.CompletedTask;
            if (PhotonNetwork.CurrentRoom?.Name == ActiveMatch?.roomName) _pendingMatchRoomName = null;
            ValidateRoom(); return Task.CompletedTask;
        }
        public static void OnLeftRoom() { if (IsTournamentActive && !string.IsNullOrEmpty(_pendingMatchRoomName)) JoinAssignedRoom(); }
        public static void OnPlayerEnteredRoom(Player player) => ValidateRoom();
        public static void OnPlayerLeftRoom(Player player) { if (IsTournamentActive && !_isInTournamentMatch) _ = RefreshAuthoritativeStateAsync(); }
        public static void OnConnectedToMasterForTournament() { if (!string.IsNullOrEmpty(_pendingMatchRoomName)) JoinAssignedRoom(); }
        public static int CalculateBracketLevels(int count) => count == 8 ? 3 : count == 4 ? 2 : 0;
    }
}
