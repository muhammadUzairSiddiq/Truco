using MH.Multiplayer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public static class ApiController
{
    #region Setters/Private Variables


    #endregion

    #region Getters/Public Variables

    public static SessionTournament GetActiveTournaments { private set; get; }
    public static SessionUser GetSessionUser { private set; get; }

    #endregion

    #region Initialization

    public static async void Init(LoginRequest loginRequest, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        Debug.Log("[ApiController] - Initializing Api");

        try
        {
            bool loggedIn = await LoginAsync(loginRequest, null, onErrorAction);

            if (loggedIn)
            {
              await GetCurrentUserProfile();

                onCompleteAction?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Initialization Failed: " + ex.Message);

            onErrorAction?.Invoke(ex.Message);
        }
    }

    #endregion

    #region Auth & User Profile

    public static async Task<bool> LoginAsync(LoginRequest loginRequestData, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        try
        {
            string json = JsonUtility.ToJson(loginRequestData);

            string resp = await HttpApiClient.PostAsync(ApiConfig.Login, json);

            // Try to extract token if server sends it in JSON body
            try
            {
                var loginResp = JsonUtility.FromJson<LoginResponse>(resp);
                if (loginResp != null)
                {
                    string token = !string.IsNullOrEmpty(loginResp.accessToken) ? loginResp.accessToken : loginResp.token;
                    if (!string.IsNullOrEmpty(token))
                    {
                        HttpApiClient.SetAuthToken(token);
                        Debug.Log("[ApiController] - Token saved from Login Response.");
                    }
                }
            }
            catch (Exception) { /* Not a standard login response package or token in cookie */ }

            onCompleteAction?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            onErrorAction?.Invoke(ex.Message);
            Debug.Log("[ApiController] - Login failed: " + ex.Message);

            return false;
        }
    }

    [Serializable]
    public class LoginResponse
    {
        public bool success;
        public string accessToken; // Adjusted to common field name
        public string token;       // Fallback
        public User user;
    }
    public static async Task<bool> RegisterAsync(RegisterRequest registerData, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        try
        {
            string json = JsonUtility.ToJson(registerData);

            string resp = await HttpApiClient.PostAsync(ApiConfig.Register, json);

            RegisterResponse registerResponse = JsonUtility.FromJson<RegisterResponse>(resp);

            Debug.Log($"[ApiController] - User registered successfully: {registerResponse.username}");

            onCompleteAction?.Invoke();

            return true;
        }
        catch (Exception ex)
        {
            onErrorAction?.Invoke("Registration failed: " + ex.Message);

            Debug.Log("[ApiController] - Registration failed: " + ex.Message);
            return false;
        }
    }
    
    /// <summary>Local logout: clear HTTP session, destroy DDOL session holders. Call before loading the login scene.</summary>
    public static void ClearClientSessionState()
    {
        if (GetSessionUser != null)
        {
            GetSessionUser.Logout();
            Object.Destroy(GetSessionUser.gameObject);
            GetSessionUser = null;
        }
        if (GetActiveTournaments != null)
        {
            Object.Destroy(GetActiveTournaments.gameObject);
            GetActiveTournaments = null;
        }
        HttpApiClient.ClearAuthSession();
    }

    public static async Task<bool> GetCurrentUserProfile()
    {
        try
        {
            string profileResponse = await HttpApiClient.GetAsync(ApiConfig.GetCurrentUserProfile);
            UserProfileResponse userProfile = JsonUtility.FromJson<UserProfileResponse>(profileResponse);
            if (userProfile != null && !string.IsNullOrEmpty(userProfile.user._id))
            {
                if (GetSessionUser == null)
                {
                    GameObject sessionObj = new GameObject("[SessionUser]");
                    Object.DontDestroyOnLoad(sessionObj);
                    GetSessionUser = sessionObj.AddComponent<SessionUser>();
                }

                //userProfile._id += $"{Random.Range(1111, 9999)}";
                //userProfile.username += $"{Random.Range(1111, 9999)}";

                GetSessionUser.UpdateUserData(userProfile.user);
                TrucoWalletHudRefresh.Apply();

                Debug.Log("[ApiController] - User Profile fetched: " + userProfile.user.username);
                return true;
            }
            else
            {
                Debug.Log("[ApiController] - User Profile response invalid." + profileResponse);
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Fetching User Profile failed: " + ex.Message);
            return false;
        }
    }
    public static async Task<bool> CheckUserAdmin()
    {
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.CheckAdmin);
            Debug.Log("[ApiController] - Admin Check Response: " + response);

            var result = JsonUtility.FromJson<AdminCheckResponse>(response);
            return result.ok && result.role == "admin";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[ApiController] - Admin Check Failed: " + ex.Message);
            return false;
        }
    }

    public static async Task<bool> ForgotPassword(string email, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        try
        {
            var body = new EmailBodyRequest 
            { 
                email = email 
            };

            string json = JsonUtility.ToJson(body);
            string resp = await HttpApiClient.PostAsync(ApiConfig.ForgotPassword, json);

            onCompleteAction?.Invoke();

            Debug.Log("[ApiController] - Password recovery email sent.");
            return true;
        }
        catch (Exception ex)
        {
            onErrorAction?.Invoke("Password recovery failed: " + ex.Message);
            Debug.Log("[ApiController] - Password recovery failed: " + ex.Message);
            return false;
        }
    }
    public static async Task<bool> SendVerificationOTP(string email, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        try
        {
            var body = new EmailBodyRequest
            {
                email = email
            };

            string json = JsonUtility.ToJson(body);
            string resp = await HttpApiClient.PostAsync(ApiConfig.ResendVerification, json);

            onCompleteAction?.Invoke();

            Debug.Log("[ApiController] - Verification OTP Sent.");
            return true;
        }
        catch (Exception ex)
        {
            onErrorAction?.Invoke("Error Sending OTP: " + ex.Message);
            Debug.Log("[ApiController] - Error Sending OTP: " + ex.Message);
            return false;
        }

    }

    public static async Task<bool> VerifyOTP(string email, string otpCode, Action onCompleteAction = null, Action<string> onErrorAction = null)
    {
        try
        {

            var body = new OTPRequest
            {
                email = email,
                otp = otpCode
            };

            string json = JsonUtility.ToJson(body);
            string resp = await HttpApiClient.PostAsync(ApiConfig.VerifyEmail, json);

            onCompleteAction?.Invoke();

            Debug.Log("[ApiController] - Email Verified.");
            return true;
        }
        catch (Exception ex)
        {
            onErrorAction?.Invoke("Error Verifying OTP: " + ex.Message);
            Debug.Log("[ApiController] - OTP Verification Failed: " + ex.Message);
            return false;
        }
    }

    #endregion

    #region Tournaments

    public static async Task<bool> RequestLatestTournaments()
    {
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.ListTournaments);
            Debug.Log(response);

            TournamentsListResponse tournamentsListResponse = JsonUtility.FromJson<TournamentsListResponse>(response);

            if (tournamentsListResponse != null && tournamentsListResponse.tournaments.Count > 0)
            {
                if (GetActiveTournaments == null)
                {
                    GameObject sessionObj = new GameObject("[SessionTournaments]");
                    Object.DontDestroyOnLoad(sessionObj);

                    GetActiveTournaments = sessionObj.AddComponent<SessionTournament>();
                }

                GetActiveTournaments.UpdateTournaments(tournamentsListResponse.tournaments);
                Debug.Log("[ApiController] - Tournament Exists: " + tournamentsListResponse.tournaments.Count);

                return true;
            }

            Debug.Log("[ApiController] - No Active Tournament Found!");

            return true;

        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Initialization Failed: " + ex.Message);

            return false;
        }
    }
    
    public static async Task<bool> EnterTournament(string tournamentId, Action onCompleteAction = null, Action<string> onErrorAction= null)
    {
        // Implement retry logic for transient write conflicts
        const int maxRetries = 3;
        int attempt = 0;

        var playerIdBody = JsonUtility.ToJson(new { playerId = GetSessionUser?.Data?._id });

        while (attempt < maxRetries)
        {
            attempt++;
            try
            {
                string response = await HttpApiClient.PostAsync(ApiConfig.EnterTournament(tournamentId), playerIdBody);

                // If the API returns a response string with error details, check it first
                if (!string.IsNullOrEmpty(response))
                {
                    // If the response explicitly mentions already entered, treat as success
                    if (response.IndexOf("Already", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        response.IndexOf("already entered", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Debug.Log("[ApiController] - Already entered tournament (response).");
                        onCompleteAction?.Invoke();
                        return true;
                    }
                }

                // Parse and validate the response
                var result = JsonUtility.FromJson<EnterTournamentResponse>(response);
                if (result != null && result.ok)
                {
                    onCompleteAction?.Invoke();

                    Debug.Log($"[ApiController] - Successfully entered tournament. Coins: {result.coins}");
                    return true;
                }

                // If server returned a non-ok result but gave a message about write conflict, retry
                if (response != null && response.IndexOf("write conflict", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogWarning($"[ApiController] - Write conflict detected in response, attempt {attempt}/{maxRetries}. Retrying...");
                    // exponential backoff
                    await Task.Delay(200 * attempt);
                    continue;
                }

                onErrorAction?.Invoke("No se pudo entrar al torneo (respuesta inválida del servidor).");

                Debug.Log("[ApiController] - Tournament entry failed: Invalid response");
                return false;
            }
            catch (Exception ex)
            {
                // If exception message suggests a write conflict, retry a few times
                if (!string.IsNullOrEmpty(ex.Message) && ex.Message.IndexOf("write conflict", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.LogWarning($"[ApiController] - Write conflict during EnterTournament (attempt {attempt}/{maxRetries}): {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(200 * attempt); // small backoff
                        continue; // retry
                    }
                    else
                    {
                        onErrorAction?.Invoke("No se pudo entrar al torneo: " + ex.Message);
                        Debug.Log("[ApiController] - Enter Tournament Failed after retries: " + ex.Message);
                        return false;
                    }
                }

                // If exception indicates already entered, treat as success
                if (!string.IsNullOrEmpty(ex.Message) && ex.Message.IndexOf("Already", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("[ApiController] - Already entered tournament.");
                    onCompleteAction?.Invoke();
                    return true;
                }

                onErrorAction?.Invoke("No se pudo entrar al torneo: " + ex.Message);

                Debug.Log("[ApiController] - Enter Tournament Failed: " + ex.Message);
                return false;
            }
        }

        // If we exhausted retries
        onErrorAction?.Invoke("No se pudo entrar al torneo: demasiados intentos. Probá de nuevo.");
        Debug.Log("[ApiController] - Enter Tournament Failed: Maximum retry attempts reached.");
        return false;
    }
    public static async Task<bool> ValidatePrivateTournament(string tournamentId, string password)
    {
        try
        {
            ValidatePrivateTournamentRequest validateData = new ValidatePrivateTournamentRequest
            {
                password = password
            };
            string json = JsonUtility.ToJson(validateData);
            string response = await HttpApiClient.PostAsync(ApiConfig.ValidatePrivateTournament(tournamentId), json);
            ValidatePrivateTournamentResponse result = JsonUtility.FromJson<ValidatePrivateTournamentResponse>(response);

            Debug.Log($"[ApiController] - Private tournament validated: {response}");
            return result.valid;
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Validate private tournament failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<match> CreateTournamentMatch(string tournamentId, List<string> playerIds)
    {
        try
        {
            TournamentRequest tournament = MultiplayerController.CurrentTournamentRequest;
            int entryFee = tournament.EntryFee; 

            CreateTournamentMatchRequest requestPayload = new CreateTournamentMatchRequest
            {
                participants = playerIds,
                entryFee = entryFee,
                gameType = "truco"
            };

            // Convert to JSON
            string json = JsonUtility.ToJson(requestPayload);

            // Log detailed information for debugging
            Debug.Log("[ApiController] - Tournament match request:");
            Debug.Log($"[ApiController] - Tournament ID: {tournamentId}");
            Debug.Log($"[ApiController] - Player IDs: {string.Join(", ", playerIds)}");
            Debug.Log($"[ApiController] - Entry Fee: {entryFee}");
            Debug.Log($"[ApiController] - Full payload: {json}");

            // Make the API call
            string response = await HttpApiClient.PostAsync(ApiConfig.CreateTournamentMatch(tournamentId), json);
            CreateTournamentMatchResponse result = JsonUtility.FromJson<CreateTournamentMatchResponse>(response);

            if (result != null && result.ok && result.match != null)
            {
                Debug.Log($"[ApiController] - Tournament match created: {result.match._id}");
                return result.match;
            }

            Debug.Log($"[ApiController] - Create tournament match failed: {response}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Create tournament match failed: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> FinalizeTournamentMatch(string tournamentId, string matchId, string winnerId)
    {
        try
        {
            var finalizeData = new
            {
                matchId = matchId,
                winnerId = winnerId
            };

            string json = JsonUtility.ToJson(finalizeData);
            string response = await HttpApiClient.PostAsync(ApiConfig.FinalizeMatch(tournamentId), json);
            Debug.Log($"[ApiController] - Match finalized: {response}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Finalize match failed: {ex.Message}");
            return false;
        }
    }
    public static async Task<bool> FinalizeTournament(string tournamentId, string championIdStr)
    {

        try
        {
            FinalizeChampionRequest body = new FinalizeChampionRequest
            {
                championId = championIdStr
            };

            string json = JsonUtility.ToJson(body);

            string response = await HttpApiClient.PostAsync(ApiConfig.FinalizeTournament(tournamentId), json);
            Debug.Log("[ApiController] - Finalize Tournament Response: " + response);
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Finalize Tournament Failed: " + ex.Message);
            return false;
        }

    }

    public static async Task<Tournament> GetTournamentDetails(string tournamentId)
    {
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.GetTournament(tournamentId));
            Tournament tournament = JsonUtility.FromJson<Tournament>(response);
            Debug.Log($"[ApiController] - Tournament details fetched: {tournament.name}");
            return tournament;
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Get tournament details failed: {ex.Message}");
            return null;
        }
    }
    public static async Task<List<User>> GetTournamentPlayers(string tournamentId)
    {
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.GetTournamentPlayers(tournamentId));
            User[] players = JsonHelper.FromJson<User>(response);
            Debug.Log($"[ApiController] - Tournament players fetched: {players.Length}");
            return players.ToList();
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Get tournament players failed: {ex.Message}");
            return new List<User>();
        }
    }
    public static async Task<bool> IsPlayerInTournamentAsync(string tournamentId, string playerId)
    {
        try
        {
            List<User> players = await GetTournamentPlayers(tournamentId);
            return players.Any(p => p._id == playerId);
        }
        catch (Exception ex)
        {
            Debug.Log($"[ApiController] - Player validation failed: {ex.Message}");
            return false;
        }
    }


    #endregion

    #region Admin

    public static async Task<List<Tournament>> GetAllTournamentsAdmin()
    {
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.AdminListTournaments);

            Tournament[] allTournaments = JsonHelper.FromJson<Tournament>(response);

            return allTournaments.ToList();

        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Initialization Failed: " + ex.Message);

            return null;
        }

    }

    public static async Task<bool> CreateTournamentByAdmin(CreateTournamentRequestAdmin tournamentRequestAdmin, Action onSuccessAction = null, Action<string> onErrorAction = null)
    {
        try
        {
            string json = JsonUtility.ToJson(tournamentRequestAdmin);
            string response = await HttpApiClient.PostAsync(ApiConfig.ListTournaments, json);
            Debug.Log("[ApiController] - Create Tournament Response: " + response);

            onSuccessAction?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - Create Tournament Failed: " + ex.Message);
            onErrorAction?.Invoke(ex.Message);
            return false;
        }
    }

    #endregion

    #region OneVsOne (player rooms, backend authority)

    /// <summary>Parses POST join responses: flat <c>match</c>, <c>data.match</c>, or <c>data</c> as match; falls back to list row only after those shapes are tried.</summary>
    public static Player1v1Match TryParsePlayerJoinMatchJson(string response, Player1v1Match listRowHint, out string apiErrorOrMessage)
    {
        apiErrorOrMessage = null;
        if (string.IsNullOrEmpty(response) || !response.TrimStart().StartsWith("{"))
            return null;

        bool flatOk = false, wrappedOk = false, flatDataOk = false;

        try
        {
            var flat = JsonUtility.FromJson<PlayerJoinMatchResponse>(response);
            if (flat?.match != null && !string.IsNullOrEmpty(flat.match._id))
                return flat.match;
            flatOk = flat != null && (flat.ok || flat.success);
            if (flat != null)
            {
                if (!string.IsNullOrEmpty(flat.error)) apiErrorOrMessage = flat.error;
                else if (!string.IsNullOrEmpty(flat.message)) apiErrorOrMessage = flat.message;
            }
        }
        catch (Exception e) { Debug.LogWarning("[ApiController] join parse (flat): " + e.Message); }

        try
        {
            var wrapped = JsonUtility.FromJson<PlayerJoinMatchWrappedRoot>(response);
            if (wrapped?.data?.match != null && !string.IsNullOrEmpty(wrapped.data.match._id))
                return wrapped.data.match;
            wrappedOk = wrapped != null && (wrapped.ok || wrapped.success);
            if (wrapped != null)
            {
                if (!string.IsNullOrEmpty(wrapped.error)) apiErrorOrMessage = wrapped.error;
                else if (!string.IsNullOrEmpty(wrapped.message)) apiErrorOrMessage = wrapped.message;
            }
        }
        catch (Exception e) { Debug.LogWarning("[ApiController] join parse (wrapped): " + e.Message); }

        try
        {
            var flatData = JsonUtility.FromJson<PlayerJoinMatchFlattenedDataRoot>(response);
            if (flatData?.data != null && !string.IsNullOrEmpty(flatData.data._id))
                return flatData.data;
            flatDataOk = flatData != null && (flatData.ok || flatData.success);
            if (flatData != null)
            {
                if (!string.IsNullOrEmpty(flatData.error)) apiErrorOrMessage = flatData.error;
                else if (!string.IsNullOrEmpty(flatData.message)) apiErrorOrMessage = flatData.message;
            }
        }
        catch (Exception e) { Debug.LogWarning("[ApiController] join parse (flat data): " + e.Message); }

        if (listRowHint != null && (flatOk || wrappedOk || flatDataOk))
            return listRowHint;

        return null;
    }

    public static List<Player1v1Match> TryParsePlayerMatchListJson(string response)
    {
        var list = new List<Player1v1Match>();
        if (string.IsNullOrEmpty(response) || !response.TrimStart().StartsWith("{"))
        {
            if (response != null && response.TrimStart().StartsWith("["))
            {
                try
                {
                    var arr = JsonHelper.FromJson<Player1v1Match>(response);
                    if (arr != null) list.AddRange(arr);
                }
                catch (Exception e) { Debug.LogWarning("[ApiController] array parse: " + e.Message); }
            }
            return list;
        }
        try
        {
            var top = JsonUtility.FromJson<MatchesListEnvelope>(response);
            if (top?.matches != null && top.matches.Length > 0)
            {
                list.AddRange(top.matches);
                return list;
            }
        }
        catch (Exception) { }
        try
        {
            var dataRoot = JsonUtility.FromJson<MatchesListDataRoot>(response);
            if (dataRoot?.data != null && dataRoot.data.matches != null)
            {
                list.AddRange(dataRoot.data.matches);
                return list;
            }
            if (dataRoot?.matches != null)
            {
                list.AddRange(dataRoot.matches);
                return list;
            }
        }
        catch (Exception) { }
        return list;
    }

    public static async Task<List<Player1v1Match>> FetchPlayer1v1MatchList()
    {
        var result = new List<Player1v1Match>();
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.ListMatches);
            Debug.Log("[ApiController] - GET /matches: " + response);
            var parsed = TryParsePlayerMatchListJson(response);
            if (parsed != null) result = parsed;
        }
        catch (Exception ex)
        {
            Debug.Log("[ApiController] - FetchPlayer1v1MatchList: " + ex.Message);
        }
        return result;
    }

    public static async Task<Player1v1Match> PlayerCreate1v1Match(PlayerCreateMatchRequest body, Action<string> onError = null)
    {
        try
        {
            string json = JsonUtility.ToJson(body);
            string response = await HttpApiClient.PostAsync(ApiConfig.PlayerCreateMatch, json);
            Debug.Log("[ApiController] - player-create: " + response);
            if (string.IsNullOrEmpty(response) || !response.Contains("{"))
            {
                onError?.Invoke(response ?? "Error al crear la sala (backend).");
                return null;
            }
            var parsed = JsonUtility.FromJson<PlayerCreateMatchResponse>(response);
            if (parsed?.match != null && !string.IsNullOrEmpty(parsed.match._id))
            {
                await GetCurrentUserProfile();
                return parsed.match;
            }
            onError?.Invoke("No se pudo crear la sala. Revisá el backend y el formato de respuesta.");
            return null;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            Debug.Log("[ApiController] - PlayerCreate1v1Match: " + ex);
            return null;
        }
    }

    public static async Task<bool> RegisterPhotonRoomName(string matchId, string photonRoomName, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(photonRoomName)) return false;
        try
        {
            var body = new RegisterPhotonRoomRequest { photonRoomName = photonRoomName, roomName = photonRoomName };
            string json = JsonUtility.ToJson(body);
            string response = await HttpApiClient.PostAsync(ApiConfig.MatchRegisterPhotonRoom(matchId), json);
            Debug.Log("[ApiController] - photon-room: " + response);
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            return false;
        }
    }

    public static async Task<Player1v1Match> PlayerJoin1v1Match(string matchId, string password, Player1v1Match listRowHint, Action<string> onError = null)
    {
        try
        {
            string body = string.IsNullOrEmpty(password) ? "{}" : JsonUtility.ToJson(new PlayerMatchJoinRequest { password = password });
            string response = await HttpApiClient.PostAsync(ApiConfig.PlayerJoinMatch(matchId), body);
            Debug.Log("[ApiController] - join: " + response);
            if (string.IsNullOrEmpty(response) || !response.Contains("{"))
            {
                onError?.Invoke(response ?? "Error al unirse.");
                return null;
            }
            var match = TryParsePlayerJoinMatchJson(response, listRowHint, out string apiMsg);
            if (match != null)
            {
                await GetCurrentUserProfile();
                return match;
            }
            if (!string.IsNullOrEmpty(apiMsg))
                onError?.Invoke(apiMsg);
            else
                onError?.Invoke("No se pudo unir a la sala.");
            return null;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            return null;
        }
    }

    /// <summary>Fresh match row from backend before join (avoids stale list / "Match is full").</summary>
    public static async Task<Player1v1Match> GetMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return null;
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.GetMatch(matchId));
            Debug.Log("[ApiController] - GET match: " + response);
            if (string.IsNullOrEmpty(response) || !response.Contains("{")) return null;
            var direct = JsonUtility.FromJson<Player1v1Match>(response);
            if (direct != null && !string.IsNullOrEmpty(direct._id)) return direct;
            var wrapped = JsonUtility.FromJson<PlayerJoinMatchFlattenedDataRoot>(response);
            if (wrapped?.data != null && !string.IsNullOrEmpty(wrapped.data._id)) return wrapped.data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ApiController] - GetMatch1v1: " + ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Master client: POST /result (rake + prize), POST /leave, GET /auth/me.
    /// Only one client should submit the result to avoid double settlement.
    /// </summary>
    public static async System.Threading.Tasks.Task Finalize1v1MatchAsMaster(string matchId, string winnerUserId, System.Action onSettled = null)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        bool resultOk = false;
        if (!string.IsNullOrEmpty(winnerUserId))
            resultOk = await SubmitMatchResult1v1(matchId, winnerUserId, msg =>
                Debug.LogWarning("[ApiController] - match result failed: " + msg));
        await TryNotifyPlayerLeftMatch1v1(matchId);
        TrucoActiveHostMatchStore.Clear();
        await GetCurrentUserProfile();
        onSettled?.Invoke();
        if (!string.IsNullOrEmpty(winnerUserId) && !resultOk)
        {
            AppManager.Instance?.DisplayNotification(
                TrucoLocalization.IsEnglish
                    ? "Match finished, but the server did not confirm the prize. Check POST /matches/{id}/result."
                    : "Partida terminada, pero el servidor no confirmó el premio. Revisá POST /matches/{id}/result.");
        }
    }

    /// <summary>Non-master client after a match: leave row + refresh wallet (no /result).</summary>
    public static async System.Threading.Tasks.Task Finalize1v1MatchAsGuest(string matchId, System.Action onSettled = null)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        await TryNotifyPlayerLeftMatch1v1(matchId);
        TrucoActiveHostMatchStore.Clear();
        await GetCurrentUserProfile();
        onSettled?.Invoke();
    }

    /// <summary>Pre-game cancel: full entry refund via POST /leave.</summary>
    public static async System.Threading.Tasks.Task CancelPreGameMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        await TryNotifyPlayerLeftMatch1v1(matchId);
        if (TrucoActiveHostMatchStore.IsRememberedHost(matchId))
            TrucoActiveHostMatchStore.Clear();
        await GetCurrentUserProfile();
    }

    /// <summary>Backward-compatible alias — prefer <see cref="Finalize1v1MatchAsMaster"/>.</summary>
    public static System.Threading.Tasks.Task Finalize1v1MatchClient(string matchId, string winnerUserId)
        => Finalize1v1MatchAsMaster(matchId, winnerUserId);

    public static async System.Threading.Tasks.Task TryStartMatchGame1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.MatchStartGame(matchId), "{}");
            Debug.Log("[ApiController] - start-game: " + matchId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ApiController] - TryStartMatchGame1v1: " + ex.Message);
        }
    }

    public static async Task<List<Player1v1Match>> FetchLiveActiveMatchesForDashboard()
    {
        var merged = new List<Player1v1Match>();
        try
        {
            string live = await HttpApiClient.GetAsync(ApiConfig.DashboardRealtimeActiveMatches);
            Debug.Log("[ApiController] - dashboard active-matches: " + (live != null && live.Length > 500 ? live.Substring(0, 500) + "…" : live));
            var fromDash = TryParsePlayerMatchListJson(live);
            if (fromDash != null) merged.AddRange(fromDash);
        }
        catch (Exception ex) { Debug.LogWarning("[ApiController] - dashboard: " + ex.Message); }
        if (merged.Count == 0)
        {
            try
            {
                string m = await HttpApiClient.GetAsync(ApiConfig.ListMatches);
                var list = TryParsePlayerMatchListJson(m);
                if (list != null) merged = list;
            }
            catch (Exception ex) { Debug.LogWarning("[ApiController] - GET /matches: " + ex.Message); }
        }
        if (merged.Count == 0)
        {
            try
            {
                string admin = await HttpApiClient.GetAsync(ApiConfig.AdminListMatches);
                var list = TryParsePlayerMatchListJson(admin);
                if (list != null) merged = list;
            }
            catch (Exception ex) { Debug.LogWarning("[ApiController] - admin/matches: " + ex.Message); }
        }
        return merged;
    }

    public static async Task<string> FetchFraudAlertsSummaryRaw()
    {
        try
        {
            return await HttpApiClient.GetAsync(ApiConfig.DashboardRealtimeFraudAlerts);
        }
        catch (Exception ex)
        {
            return "Error: " + ex.Message;
        }
    }

    /// <summary>
    /// Reports winner; backend should mark match finished, settle coins, and remove both players from the joinable lobby
    /// (e.g. empty <c>players</c> or status <c>completed</c>) so new joins are not blocked by "Match is full".
    /// </summary>
    public static async Task<bool> SubmitMatchResult1v1(string matchId, string winnerUserId, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(winnerUserId)) return false;
        try
        {
            var body = new MatchResultSubmitRequest { winnerId = winnerUserId, status = "completed" };
            string json = JsonUtility.ToJson(body);
            await HttpApiClient.PostAsync(ApiConfig.MatchSubmitResult(matchId), json);
            Debug.Log("[ApiController] - match result reported: " + matchId);
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            Debug.LogWarning("[ApiController] - SubmitMatchResult1v1: " + ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Notifies server this user left the match (early quit, scene unload, etc.). Requires <c>POST …/matches/:id/leave</c> on API.
    /// Fails quietly if route is missing so older servers keep working.
    /// </summary>
    public static async System.Threading.Tasks.Task TryNotifyPlayerLeftMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.MatchPlayerLeave(matchId), "{}");
            Debug.Log("[ApiController] - match leave notified: " + matchId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ApiController] - TryNotifyPlayerLeftMatch1v1 (add POST /matches/:id/leave on server if needed): " + ex.Message);
        }
    }

    /// <summary>
    /// Asks the backend to END/close the match so the room disappears from the joinable list.
    /// Route is read from the central SO (key=MatchEnd). Until the backend dev provides it, this
    /// fails quietly (the client also reports the result via <see cref="SubmitMatchResult1v1"/>).
    /// </summary>
    public static async System.Threading.Tasks.Task TryEndMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.MatchEnd(matchId), "{}");
            Debug.Log("[ApiController] - match end requested: " + matchId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ApiController] - TryEndMatch1v1 (set MatchEnd path in TrucoApiEndpoints when backend is ready): " + ex.Message);
        }
    }

    public static async Task<bool> AdminForceCloseMatch1v1(string matchId, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.AdminForceCloseMatch(matchId), "{}");
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            return false;
        }
    }

    #endregion
}