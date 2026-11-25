namespace MH.Multiplayer
{
    using Photon.Pun;
    using Photon.Realtime;
    using System;
    using System.Collections.Generic;
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
        static bool _connectingAssignedRoom, _joiningAssignedRoom, _retryAssignedRoom;
        static bool _rejoinAssignedRoom, _attemptingRejoin;
        static int _assignedRoomAttempts;
        static double _assignedRetryAt, _joinDeadline, _nextReadyPublication;
        const int MaxAssignedRoomAttempts = 3;
        const int JoinTimeoutSeconds = 15;
        const string GameplayScene = "Gameplay";
        public const string TournamentSeatReadyProperty = "trucoTournamentSeatReadyV2";
        public const string TournamentLaunchProperty = "trucoTournamentLaunchV2";
        public enum AssignedTableLaunchAction { Wait, Publish, Load }
        static string _presentedAssignment, _shownPayout;
        public static TournamentRequest CurrentTournamentRequest => _currentTournamentRequest;
        public static TrucoTournamentService.Snapshot CurrentSnapshot { get; private set; }
        public static bool IsTournamentActive => _currentTournamentRequest != null;
        public static bool HasSavedTournament => !string.IsNullOrEmpty(TrucoTournamentSessionStore.Remembered());
        public static bool LastLeaveConfirmed { get; private set; }
        public static bool CanRetryAssignedRoom => _retryAssignedRoom && _assignedRoomAttempts >= MaxAssignedRoomAttempts
            && !_connectingAssignedRoom && !_joiningAssignedRoom;
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

        public static async Task InitMultiplayerSession_Tournament(TournamentRequest request, Action<string> onErrorAction = null,
            Action<string> onProgress = null)
        {
            if (_joining || IsTournamentActive) return;
            if (request == null || string.IsNullOrWhiteSpace(request.TournamentId) || request.MaxPlayerCount != 4 && request.MaxPlayerCount != 8)
            { onErrorAction?.Invoke("El torneo debe tener un identificador válido y 4 u 8 jugadores."); return; }
            _joining = true;
            TrucoTournamentSessionStore.SuppressAutomaticResume = false;
            int generation = ++_generation;
            string playerError = "No se pudieron verificar los datos del torneo. No se envió la inscripción.";
            try
            {
                // Verify the versioned read contract before a registration can debit anything.
                onProgress?.Invoke("Consultando el estado del torneo…");
                var before = await TrucoTournamentService.Get(request.TournamentId);
                if (generation != _generation) return;
                bool alreadyRegistered = TrucoTournamentService.HasConfirmedEntry(before, LocalId);
                if (!alreadyRegistered && before.status != "registration")
                {
                    onErrorAction?.Invoke("El torneo ya cerró las inscripciones. Si te inscribiste, actualizá para consultar tu lugar.");
                    return;
                }
                OneVsOnePhotonFlow.Instance?.ResetPurpose();
                if (!alreadyRegistered)
                {
                    onProgress?.Invoke("Verificando la conexión multijugador…");
                    if (!await TrucoPhotonAuthentication.ConnectAndWaitUntilReadyAsync("tournament", request.TournamentId))
                    {
                        if (generation == _generation)
                            onErrorAction?.Invoke(TrucoPhotonAuthentication.LastError ?? "No se pudo verificar la conexión multijugador.");
                        return;
                    }
                }
                if (generation != _generation) return;
                onProgress?.Invoke("Confirmando tu inscripción…");
                _currentTournamentRequest = RequestFrom(before);
                CurrentSnapshot = before;
                TrucoTournamentSessionStore.Remember(before.tournamentId);
                ValidateMultiplayerCallbacks(); _multiplayerCallbacks.EnableTournamentMode();
                TrucoTournamentSessionPoller.Ensure();
                var joined = alreadyRegistered ? before : await TrucoTournamentService.Join(before);
                if (generation != _generation) return;
                ApplySnapshot(joined, force: true);
            }
            catch (HttpApiClient.RequestException exception) when (exception.IsDefiniteRejection)
            {
                if (generation != _generation) return;
                // A rejected join must not leave a phantom active registration or send Leave.
                if (IsTournamentActive)
                {
                    TrucoTournamentSessionStore.Forget();
                    LeaveTournamentMatchmaking();
                }
                if (exception.StatusCode == 401) { ApiController.ShowExpiredSession(); return; }
                onErrorAction?.Invoke(exception.Code == "WALLET_FROZEN"
                    ? "Tu billetera está bloqueada. Contactá a soporte para inscribirte en un torneo pago."
                    : "El servidor rechazó la inscripción. " + exception.Message);
            }
            catch (Exception exception)
            {
                if (generation != _generation) return;
                TrucoDebugLog.Warn(TrucoDebugLog.Category.Api, "Tournament entry stopped: " + exception.GetType().Name);
                if (IsTournamentActive) ShowPending("La inscripción requiere confirmación del servidor. Consultá el estado antes de volver a intentar.");
                else onErrorAction?.Invoke(playerError);
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
                if (!TrucoTournamentService.HasConfirmedEntry(snapshot, LocalId))
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
            catch (HttpApiClient.RequestException exception) when (exception.StatusCode == 401)
            { if (generation == _generation) ApiController.ShowExpiredSession(); }
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
            if (!changed)
            {
                // Retry an unconfirmed start on subsequent polls even when revision is unchanged.
                if (CanStartTournament(snapshot)) _ = StartTournamentNowAsync();
                // A transport callback can arrive before room properties or the peer's identity.
                // Polling must still recheck the assigned table when server state is unchanged.
                if (snapshot.PlayableAssignment != null) ValidateRoom();
                var finalMatch = snapshot.matches.FirstOrDefault(m => m.round == (snapshot.maxPlayers == 8 ? 2 : 1) && m.index == 0);
                if (snapshot.status == "playing" && finalMatch?.status == "finished" && finalMatch.winnerId == LocalId)
                    _ = FinalizeTournamentAsync();
                return;
            }
            BracketState = TrucoTournamentService.ToBracket(snapshot);
            OnPlayerCountUpdated?.Invoke(snapshot.participants.Count(p => p.entryPaid && p.status != "withdrawn"));
            OneVsOneMatchSession.SetWithFlor(snapshot.withFlor); OneVsOneMatchSession.SetTargetScore(snapshot.targetScore);
            PublishBracketToCurrentRoom();
            if (snapshot.localStatus == "not_registered")
            { ShowPending(HasPendingSettlement(snapshot) ? "El servidor mantiene un reembolso pendiente de confirmación." : "El servidor todavía no confirmó tu inscripción. No se confirmó un cobro ni una mesa."); return; }
            if (snapshot.status == "cancelled")
            {
                StopTableForServerState();
                var refund = TrucoTournamentService.RefundForPlayer(snapshot, LocalId);
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
                if (CanStartTournament(snapshot))
                {
                    TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.CreatingMatches);
                    _ = StartTournamentNowAsync();
                }
                else TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.Filling);
                return;
            }
            if (snapshot.status == "starting" || snapshot.status == "playing" && !TrucoTournamentService.HasCompleteOpeningRound(snapshot))
            {
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.CreatingMatches);
                return;
            }
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
            bool pendingMatch = snapshot.matches.Any(m => (m.status == "ready" || m.status == "playing")
                && (m.firstPlayerId == LocalId || m.secondPlayerId == LocalId));
            if (pendingMatch)
            {
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.AwaitingAssignment);
                return;
            }
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.WaitingNextRound);
            TournamentBracketOverlay.Show(snapshot.localStatus == "eliminated");
        }
        public static bool CanStartTournament(TrucoTournamentService.Snapshot snapshot) => TrucoTournamentService.CanStart(snapshot);

        static async Task StartTournamentNowAsync()
        {
            if (_starting || !CanStartTournament(CurrentSnapshot) || !TrucoTournamentService.HasConfirmedEntry(CurrentSnapshot, LocalId)) return;
            int generation = _generation; _starting = true;
            try
            {
                var snapshot = await TrucoTournamentService.Start(CurrentSnapshot);
                if (generation == _generation) ApplySnapshot(snapshot, force: true);
            }
            catch (HttpApiClient.RequestException exception) when (exception.StatusCode == 401)
            { if (generation == _generation) ApiController.ShowExpiredSession(); }
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
            var refund = TrucoTournamentService.RefundForPlayer(snapshot, LocalId);
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
            PhotonNetwork.AutomaticallySyncScene = false;
            _isInTournamentMatch = false; _loadingTable = false; _pendingMatchRoomName = null; ActiveMatch = null; _presentedAssignment = null;
            ResetAssignedTransport();
            if (SceneManager.GetActiveScene().name == GameplayScene) SceneManager.LoadScene("TempScene");
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
            if (ActiveMatch != null || _isInTournamentMatch) PhotonNetwork.AutomaticallySyncScene = false;
            _generation++; _joining = _starting = _refreshing = _advancing = _leaving = _loadingTable = _isInTournamentMatch = _finishing = false;
            ResetAssignedTransport();
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
                return;
            }
            // Never publish a transition away (or the previous hand scene) into the old room.
            PhotonNetwork.AutomaticallySyncScene = false;
            if (SceneManager.GetActiveScene().name == GameplayScene)
            {
                GameManager.Instance?.SetCanPlayCard(false);
                UIMANAGER.Instance?.DisableButtons();
                TurnManager.Instance?.StopAllTurnTimers();
                SceneManager.LoadScene("TempScene");
            }
            ActiveMatch = serverTable; NextMatch = null; _presentedAssignment = table.matchId;
            _isInTournamentMatch = true; _loadingTable = false; _pendingMatchRoomName = table.roomName;
            ResetAssignedTransport();
            TrucoMatchProgress.ResetForNewMatch();
            ValidateMultiplayerCallbacks(); _multiplayerCallbacks.EnableTournamentMode();
            SpectatorContext.Clear();
            PhotonNetwork.NickName = ApiController.GetSessionUser?.Data?.username ?? "Jugador";
            OneVsOnePhotonFlow.Instance?.ResetPurpose();
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable);
            BeginAssignedRoomAttempt();
        }

        static void ResetAssignedTransport()
        {
            _connectingAssignedRoom = _joiningAssignedRoom = _retryAssignedRoom = false;
            _rejoinAssignedRoom = _attemptingRejoin = false;
            _assignedRoomAttempts = 0;
            _assignedRetryAt = _joinDeadline = _nextReadyPublication = 0;
        }

        public static void RetryAssignedRoom()
        {
            if (!CanRetryAssignedRoom || !HasCurrentAssignment()) return;
            _assignedRoomAttempts = 0;
            BeginAssignedRoomAttempt();
        }

        static bool HasCurrentAssignment() => _isInTournamentMatch && ActiveMatch != null
            && CurrentSnapshot?.PlayableAssignment?.matchId == ActiveMatch.matchId;

        static void BeginAssignedRoomAttempt()
        {
            _retryAssignedRoom = false;
            _assignedRoomAttempts++;
            TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable);
            _ = ConnectAssignedTransportAsync(_generation, ActiveMatch);
        }

        static async Task ConnectAssignedTransportAsync(int generation, TournamentMatch table)
        {
            string tournamentId = _currentTournamentRequest.TournamentId;
            _connectingAssignedRoom = true;
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == table.roomName
                    && TrucoPhotonAuthentication.IsAuthorizedForRoom("tournament", tournamentId, table.matchId, table.roomName))
                { await OnJoinedRoom(); return; }
                bool ready = await TrucoPhotonAuthentication.ConnectAndWaitUntilReadyAsync("tournament", tournamentId, table.matchId, table.roomName);
                if (generation != _generation || ActiveMatch?.matchId != table.matchId) return;
                if (ready) JoinAssignedRoom();
                else AssignedRoomFailed("Photon authentication or Master connection");
            }
            catch (Exception exception)
            {
                if (generation == _generation && ActiveMatch?.matchId == table.matchId)
                    AssignedRoomFailed("transport setup: " + exception.GetType().Name);
            }
            finally
            {
                if (generation == _generation && ActiveMatch?.matchId == table.matchId) _connectingAssignedRoom = false;
            }
        }
        static void JoinAssignedRoom()
        {
            var assignment = TrucoTournamentService.AssignedTable(CurrentSnapshot);
            if (assignment == null || ActiveMatch?.matchId != assignment.matchId || string.IsNullOrEmpty(_pendingMatchRoomName)
                || _joiningAssignedRoom || PhotonNetwork.InRoom) return;
            if (PhotonNetwork.Server != ServerConnection.MasterServer
                || !TrucoPhotonAuthentication.IsAuthorizedForRoom("tournament", CurrentSnapshot.tournamentId, assignment.matchId, assignment.roomName))
            { AssignedRoomFailed("Photon Master or room scope unavailable"); return; }
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["userId"] = LocalId,
                ["username"] = ApiController.GetSessionUser?.Data?.username, ["isTournamentPlayer"] = true,
                [PhotonPlayerHelper.SpectatorKey] = false });
            var properties = new Hashtable { ["tournamentId"] = CurrentSnapshot.tournamentId, ["backendMatchId"] = assignment.matchId,
                ["isTournamentMatch"] = true, ["withFlor"] = CurrentSnapshot.withFlor, ["targetScore"] = CurrentSnapshot.targetScore,
                ["tournamentRevision"] = CurrentSnapshot.revision };
            _attemptingRejoin = _rejoinAssignedRoom;
            _joiningAssignedRoom = _attemptingRejoin
                ? PhotonNetwork.RejoinRoom(assignment.roomName)
                : PhotonNetwork.JoinOrCreateRoom(assignment.roomName, new RoomOptions { MaxPlayers = 2, IsVisible = false,
                    PublishUserId = true, PlayerTtl = 60000, EmptyRoomTtl = 120000, CustomRoomProperties = properties }, TypedLobby.Default);
            _joinDeadline = Time.realtimeSinceStartupAsDouble + JoinTimeoutSeconds;
            if (!_joiningAssignedRoom) AssignedRoomFailed("Photon rejected the local room request");
        }
        static void AssignedRoomFailed(string reason)
        {
            _joiningAssignedRoom = false;
            _retryAssignedRoom = true;
            _assignedRetryAt = Time.realtimeSinceStartupAsDouble + Math.Min(_assignedRoomAttempts * 2, 6);
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Photon, $"Tournament assigned room failed (attempt {_assignedRoomAttempts}): {reason}");
            if (_assignedRoomAttempts < MaxAssignedRoomAttempts)
                TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable, "Reconectando a tu mesa asignada…");
            else ShowPending("No se pudo conectar a tu mesa. Tu inscripción sigue guardada. Reintentá la conexión.");
        }
        public static void OnAssignedRoomFailed(string operation, short code)
        {
            if (!_joiningAssignedRoom) return;
            if (code == ErrorCode.JoinFailedFoundInactiveJoiner) _rejoinAssignedRoom = true;
            else if (_attemptingRejoin && (code == ErrorCode.JoinFailedWithRejoinerNotFound || code == ErrorCode.GameDoesNotExist))
                _rejoinAssignedRoom = false;
            AssignedRoomFailed($"{operation} code={code}");
        }
        public static void OnAssignedRoomDisconnected(DisconnectCause cause)
        {
            // After the first hand goes live its reconnection manager owns gameplay recovery.
            if (!HasCurrentAssignment() || _connectingAssignedRoom || _retryAssignedRoom
                || SceneManager.GetActiveScene().name == GameplayScene || _loadingTable) return;
            AssignedRoomFailed($"disconnected cause={cause}");
        }

        public static void TickAssignedTransport()
        {
            if (!HasCurrentAssignment() || _loadingTable || _connectingAssignedRoom) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_joiningAssignedRoom && !PhotonNetwork.InRoom && now >= _joinDeadline)
            {
                AssignedRoomFailed("Photon room request timed out");
                if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            }
            if (_joiningAssignedRoom && PhotonNetwork.InRoom)
            {
                _ = OnJoinedRoom();
                return;
            }
            if (_retryAssignedRoom && _assignedRoomAttempts < MaxAssignedRoomAttempts && now >= _assignedRetryAt)
                BeginAssignedRoomAttempt();
            else ValidateRoom();
        }

        static bool IsAuthorizedAssignedRoom()
        {
            return HasCurrentAssignment() && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                && TrucoPhotonAuthentication.IsAuthorizedForRoom("tournament", CurrentSnapshot.tournamentId, ActiveMatch.matchId, ActiveMatch.roomName)
                && TournamentStartupValidation.HasMatchingTableIdentity(ActiveMatch, CurrentSnapshot.tournamentId,
                    PhotonNetwork.CurrentRoom.Name, PhotonNetwork.CurrentRoom.CustomProperties);
        }

        public static bool HasLiveAssignedTable() => IsAuthorizedAssignedRoom()
            && PhotonPlayerHelper.HasMatchingAuthenticatedPair(PhotonNetwork.PlayerList.Where(player => !player.IsInactive),
                ActiveMatch.playerIds, LocalId);

        public static bool HasReadyAssignedPair(IEnumerable<Player> players, TournamentMatch table, string localUserId)
        {
            if (players == null || table == null || string.IsNullOrWhiteSpace(table.matchId)) return false;
            var active = players.Where(player => player != null && !player.IsInactive && !PhotonPlayerHelper.IsSpectatorPlayer(player)).ToArray();
            return PhotonPlayerHelper.HasMatchingAuthenticatedPair(active, table.playerIds, localUserId)
                && active.All(player => player.CustomProperties.TryGetValue(TournamentSeatReadyProperty, out object ready)
                    && ready as string == table.matchId);
        }

        public static AssignedTableLaunchAction GetAssignedTableLaunchAction(bool isMaster, bool hasExactReadyPair,
            string roomLaunchMatchId, string assignedMatchId)
        {
            if (!hasExactReadyPair || string.IsNullOrWhiteSpace(assignedMatchId)) return AssignedTableLaunchAction.Wait;
            if (roomLaunchMatchId == assignedMatchId) return AssignedTableLaunchAction.Load;
            return isMaster ? AssignedTableLaunchAction.Publish : AssignedTableLaunchAction.Wait;
        }

        public static void ValidateRoom()
        {
            if (_loadingTable || !IsAuthorizedAssignedRoom()) return;
            var room = PhotonNetwork.CurrentRoom;
            if (PhotonNetwork.LocalPlayer.UserId != LocalId) return;
            double now = Time.realtimeSinceStartupAsDouble;
            // Both peers announce their current assignment after entering this exact room.
            // The match-scoped room marker survives a dropped callback and is read by both peers.
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(TournamentSeatReadyProperty, out object ready)
                || ready as string != ActiveMatch.matchId)
            {
                if (now >= _nextReadyPublication)
                {
                    _nextReadyPublication = now + 1;
                    PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [TournamentSeatReadyProperty] = ActiveMatch.matchId,
                        [PhotonPlayerHelper.SpectatorKey] = false });
                }
                return;
            }
            room.CustomProperties.TryGetValue(TournamentLaunchProperty, out object launch);
            bool pairReady = HasReadyAssignedPair(room.Players.Values, ActiveMatch, LocalId);
            switch (GetAssignedTableLaunchAction(PhotonNetwork.IsMasterClient, pairReady, launch as string, ActiveMatch.matchId))
            {
                case AssignedTableLaunchAction.Publish when now >= _nextReadyPublication:
                    _nextReadyPublication = now + 1;
                    room.SetCustomProperties(new Hashtable { [TournamentLaunchProperty] = ActiveMatch.matchId });
                    break;
                case AssignedTableLaunchAction.Load:
                    _loadingTable = true;
                    TournamentBracketOverlay.Hide();
                    TrucoTournamentStatusScreen.SetPhase(TrucoTournamentPhase.LoadingTable, "Preparando las cartas para ambos jugadores…");
                    if (SceneManager.GetActiveScene().name != GameplayScene) PhotonNetwork.LoadLevel(GameplayScene);
                    break;
            }
        }

        public static void OnGameplayLoaded()
        {
            if (!IsAuthorizedAssignedRoom()) return;
            _loadingTable = true; TournamentBracketOverlay.Hide();
            PhotonNetwork.AutomaticallySyncScene = true;
            TournamentManager.Instance?.HideTournamentWaitingRoomOverlay(); TournamentManager.Instance?.HideTournamentMatchmakingUI();
        }
        public static Task OnJoinedRoom()
        {
            if (!HasCurrentAssignment()) return Task.CompletedTask;
            if (!IsAuthorizedAssignedRoom())
            {
                if (_joiningAssignedRoom && PhotonNetwork.InRoom)
                {
                    _rejoinAssignedRoom = false;
                    AssignedRoomFailed("Photon room identity or admission does not match the assignment");
                    PhotonNetwork.LeaveRoom(false);
                }
                return Task.CompletedTask;
            }
            _joiningAssignedRoom = _retryAssignedRoom = false;
            _rejoinAssignedRoom = true;
            _assignedRoomAttempts = 0;
            _nextReadyPublication = 0;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Photon,
                $"Tournament assigned room joined: match={ActiveMatch.matchId} room={ActiveMatch.roomName} region={PhotonNetwork.CloudRegion}.");
            ValidateRoom(); return Task.CompletedTask;
        }
        public static void OnPlayerEnteredRoom(Player player) => ValidateRoom();
        public static void OnPlayerLeftRoom(Player player)
        {
            if (!IsTournamentActive) return;
            ValidateRoom();
            _ = RefreshAuthoritativeStateAsync();
        }
        public static int CalculateBracketLevels(int count) => count == 8 ? 3 : count == 4 ? 2 : 0;
    }
}
