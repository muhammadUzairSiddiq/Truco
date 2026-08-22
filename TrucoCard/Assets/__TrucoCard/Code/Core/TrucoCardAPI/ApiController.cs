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

    public static async Task<bool> EnsureSessionUserLoadedAsync()
    {
        if (!string.IsNullOrEmpty(GetSessionUser?.Data?._id)) return true;
        return await GetCurrentUserProfile();
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
        {
            if (string.IsNullOrEmpty(listRowHint.photonRoomName) && string.IsNullOrEmpty(listRowHint.photonRoom))
                listRowHint.photonRoomName = listRowHint.ResolvePhotonRoomNameOrDefault(listRowHint._id);
            return listRowHint;
        }

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
            var parsed = TryParsePlayerMatchListJson(response);
            int raw = parsed?.Count ?? 0;
            if (parsed != null)
            {
                for (int i = 0; i < parsed.Count; i++)
                {
                    var m = parsed[i];
                    if (m != null && m.IsLobbyLikeStatus())
                        result.Add(m);
                }
            }
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api,
                "GET /matches raw=" + raw + " lobby-like=" + result.Count +
                " (backend also returns cancelled/completed rows — filtered client-side)");
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
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "player-create: " + response);
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
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "photon-room: " + response);
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
            TrucoDebugLog.Always(TrucoDebugLog.Category.Api, "POST /join response: " + response);
            if (string.IsNullOrEmpty(response) || !response.Contains("{"))
            {
                onError?.Invoke(response ?? "Error al unirse.");
                return null;
            }
            var match = TryParsePlayerJoinMatchJson(response, listRowHint, out string apiMsg);
            if (match != null)
            {
                if (string.IsNullOrEmpty(match._id) && listRowHint != null)
                    match._id = listRowHint._id;
                await GetCurrentUserProfile();
                return match;
            }

            // Backend sometimes returns success + message without a match object.
            if (IsJoinSuccessMessage(apiMsg) || IsJoinSuccessPayload(response))
            {
                TrucoDebugLog.Always(TrucoDebugLog.Category.Api,
                    "POST /join success without match object — using list row / GET hydrate");
                var hydrated = await GetMatch1v1(matchId);
                if (hydrated != null)
                {
                    await GetCurrentUserProfile();
                    return hydrated;
                }
                if (listRowHint != null)
                {
                    await GetCurrentUserProfile();
                    return listRowHint;
                }
            }

            if (!string.IsNullOrEmpty(apiMsg))
                onError?.Invoke(apiMsg);
            else
                onError?.Invoke("No se pudo unir a la sala.");
            return null;
        }
        catch (Exception ex)
        {
            TrucoDebugLog.Error(TrucoDebugLog.Category.Api, "POST /join exception: " + ex.Message);
            if (OneVsOneLobbyFlowRules.IsMatchFullApiError(ex.Message))
            {
                var verify = await GetMatch1v1(matchId);
                if (verify != null && verify.IsAlreadyRegisteredGuest())
                {
                    await GetCurrentUserProfile();
                    return verify;
                }
            }
            onError?.Invoke(ex.Message);
            return null;
        }
    }

    static bool IsJoinSuccessMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        return message.IndexOf("joined", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("unido", StringComparison.OrdinalIgnoreCase) >= 0
               || message.IndexOf("Successfully joined", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsJoinSuccessPayload(string response)
    {
        if (string.IsNullOrEmpty(response)) return false;
        return response.IndexOf("\"success\":true", StringComparison.OrdinalIgnoreCase) >= 0
               || response.IndexOf("\"ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static Player1v1Match TryParseSingleMatchJson(string response)
    {
        if (string.IsNullOrEmpty(response) || !response.Contains("{")) return null;
        try
        {
            var wrapped = JsonUtility.FromJson<PlayerCreateMatchResponse>(response);
            if (wrapped?.match != null && !string.IsNullOrEmpty(wrapped.match._id))
                return wrapped.match;
        }
        catch (Exception) { }
        try
        {
            var join = JsonUtility.FromJson<PlayerJoinMatchResponse>(response);
            if (join?.match != null && !string.IsNullOrEmpty(join.match._id))
                return join.match;
        }
        catch (Exception) { }
        try
        {
            var direct = JsonUtility.FromJson<Player1v1Match>(response);
            if (direct != null && !string.IsNullOrEmpty(direct._id)) return direct;
        }
        catch (Exception) { }
        try
        {
            var flat = JsonUtility.FromJson<PlayerJoinMatchFlattenedDataRoot>(response);
            if (flat?.data != null && !string.IsNullOrEmpty(flat.data._id)) return flat.data;
        }
        catch (Exception) { }
        return null;
    }

    public static async Task<Player1v1Match> GetMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return null;
        try
        {
            string response = await HttpApiClient.GetAsync(ApiConfig.GetMatch(matchId));
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "GET match id=" + matchId);
            return TryParseSingleMatchJson(response);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ApiController] - GetMatch1v1: " + ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Settle 1v1: POST /result (prize) with retries, then leave+end, then refresh wallet.
    /// Both clients may call this with the same winnerId — server should treat duplicate as OK.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> Finalize1v1MatchAsMaster(string matchId, string winnerUserId, System.Action onSettled = null)
        => await Finalize1v1MatchSettlement(matchId, winnerUserId, submitResult: true, onSettled);

    /// <summary>Guest also submits /result when it knows the winner (master alone was unreliable after mano master rotate).</summary>
    public static async System.Threading.Tasks.Task<bool> Finalize1v1MatchAsGuest(string matchId, string winnerUserId = null, System.Action onSettled = null)
        => await Finalize1v1MatchSettlement(matchId, winnerUserId, submitResult: !string.IsNullOrEmpty(winnerUserId), onSettled);

    /// <returns>True when POST /result succeeded or was already settled; false when skipped or failed.</returns>
    public static async System.Threading.Tasks.Task<bool> Finalize1v1MatchSettlement(
        string matchId,
        string winnerUserId,
        bool submitResult,
        System.Action onSettled = null,
        string loserUserId = null)
    {
        if (string.IsNullOrEmpty(matchId)) return false;

        int balBefore = GetSessionUser?.Data?.wallet?.balance ?? -1;
        bool secretOk = HttpApiClient.HasGameSecretConfigured();
        TrucoRulesScenarioLog.Backend("SETTLE start",
            "match=" + matchId
            + " winner=" + (winnerUserId ?? "null")
            + " submitResult=" + submitResult
            + " secretConfigured=" + secretOk
            + " balBefore=" + balBefore);

        bool resultOk = !submitResult;
        string lastErr = null;
        if (submitResult)
        {
            if (string.IsNullOrEmpty(winnerUserId))
            {
                lastErr = "winnerUserId empty — cannot POST /result";
                TrucoRulesScenarioLog.BackendFail("POST /result skipped", lastErr);
            }
            else if (!secretOk)
            {
                lastErr = "x-game-secret missing on client (TrucoClientSettings.gameSecret)";
                TrucoRulesScenarioLog.BackendFail("POST /result blocked", lastErr);
            }
            else
            {
                const int maxAttempts = 5;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    resultOk = await SubmitMatchResult1v1(matchId, winnerUserId, loserUserId, msg =>
                    {
                        lastErr = msg;
                        Debug.LogWarning("[ApiController] - match result failed (attempt " + attempt + "): " + msg);
                    });
                    if (resultOk) break;
                    if (IsAlreadySettledError(lastErr))
                    {
                        resultOk = true;
                        TrucoRulesScenarioLog.Backend("POST /result already settled → treat OK",
                            "match=" + matchId + " msg=" + lastErr);
                        break;
                    }
                    if (attempt < maxAttempts)
                        await System.Threading.Tasks.Task.Delay(400 * attempt);
                }
            }
        }

        // Winner closes the row (/end only). Loser only refreshes wallet — never POST /leave post-game.
        if (submitResult)
            await CloseCompletedMatchRowAsync(matchId);
        TrucoActiveHostMatchStore.Clear();
        await GetCurrentUserProfile();
        int balAfter = GetSessionUser?.Data?.wallet?.balance ?? -1;
        TrucoRulesScenarioLog.Backend("SETTLE done",
            "match=" + matchId
            + " resultOk=" + resultOk
            + " balBefore=" + balBefore
            + " balAfter=" + balAfter
            + " delta=" + (balBefore >= 0 && balAfter >= 0 ? (balAfter - balBefore).ToString() : "?"));
        onSettled?.Invoke();

        // Never show prize/support popups here — win/lose panels + balance refresh own the UX.
        // Failed /result is logged only (server may already have settled via walkover).
        if (submitResult && !resultOk)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                "SETTLE /result failed. winner=" + (winnerUserId ?? "?")
                + " local=" + (GetSessionUser?.Data?._id ?? "?")
                + " err=" + (lastErr ?? "?"));
        }

        return resultOk;
    }

    static bool IsAlreadySettledError(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        string m = msg.ToLowerInvariant();
        return m.Contains("already") || m.Contains("completed") || m.Contains("finished")
               || m.Contains("settled") || m.Contains("duplicate")
               || m.Contains("walkover") || m.Contains("closed") || m.Contains("ended")
               || m.Contains("ya ") || m.Contains("acredit") || m.Contains("finaliz")
               || m.Contains("cerrad") || m.Contains("duplicad") || m.Contains("procesad");
    }

    /// <summary>Pre-game cancel: POST /leave (refund) then /end so the lobby row disappears.</summary>
    public static async System.Threading.Tasks.Task<bool> CancelPreGameMatch1v1(string matchId, int expectedRefund = 0)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        int fee = expectedRefund > 0 ? expectedRefund : TrucoActiveHostMatchStore.GetRememberedEntryFee();
        int balBefore = GetSessionUser?.Data?.wallet?.balance ?? -1;
        TrucoDebugLog.Log(TrucoDebugLog.Category.Api,
            "CancelPreGameMatch1v1 match=" + matchId + " balBefore=" + balBefore + " expectRefund=" + fee);
        bool leaveOk = await CloseLobbyMatchForRefundAsync(matchId);

        for (int i = 0; i < 3; i++)
        {
            await GetCurrentUserProfile();
            int balAfter = GetSessionUser?.Data?.wallet?.balance ?? -1;
            bool balanceRefunded = WalletGainedAtLeast(balBefore, balAfter, fee);
            TrucoRulesScenarioLog.Backend("CancelPreGame done",
                "match=" + matchId + " leaveOk=" + leaveOk
                + " balBefore=" + balBefore + " balAfter=" + balAfter
                + " expectRefund=" + fee
                + " delta=" + (balBefore >= 0 && balAfter >= 0 ? (balAfter - balBefore).ToString() : "?"));
            // Never drop the remembered match id until the wallet actually came back
            // (or there was no stake to refund). Photon webhooks can close the row
            // without refunding; clearing early made login-reclaim impossible.
            bool confirmed = balanceRefunded;
            if (!confirmed && leaveOk && (balBefore < 0 || balAfter < 0))
                confirmed = true;
            if (confirmed)
            {
                if (TrucoActiveHostMatchStore.IsRememberedHost(matchId))
                    TrucoActiveHostMatchStore.Clear();
                OneVsOneMatchSession.ClearSavedRoomPersistence();
                TrucoWalletHudRefresh.Apply();
                return true;
            }
            if (i < 2)
                await System.Threading.Tasks.Task.Delay(400 * (i + 1));
        }

        TrucoWalletHudRefresh.Apply();
        return false;
    }

    /// <summary>
    /// Both players failed reconnect (or local reconnect failed with no stayer walkover yet).
    /// Closes the match row via leave+end and logs — backend should refund locked entry when no winner was posted.
    /// </summary>
    public static async System.Threading.Tasks.Task CancelMutualDisconnect1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        TrucoRulesScenarioLog.Backend("CancelMutualDisconnect1v1", "match=" + matchId);
        TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
            "MUTUAL_DISCONNECT cancel+refund attempt match=" + matchId);
        try
        {
            await CloseLobbyMatchForRefundAsync(matchId);
            await GetCurrentUserProfile();
        }
        catch (Exception ex)
        {
            TrucoRulesScenarioLog.BackendFail("CancelMutualDisconnect1v1", ex.Message);
            Debug.LogWarning("[ApiController] CancelMutualDisconnect1v1: " + ex.Message);
        }
    }

    /// <summary>
    /// Opponent abandoned: POST /walkover with claimerId, then close the match row.
    /// Requires x-game-secret (same as /result).
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> ClaimWalkover1v1(string matchId, string claimerUserId, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(claimerUserId)) return false;
        try
        {
            string matchToken = await RequestMatchToken1v1(matchId, onError);
            var body = new MatchWalkoverRequest { claimerId = claimerUserId };
            string json = JsonUtility.ToJson(body);
            TrucoRulesScenarioLog.Backend("POST /walkover attempt",
                "match=" + matchId + " claimer=" + claimerUserId + " hasToken=" + !string.IsNullOrEmpty(matchToken));
            await HttpApiClient.PostAsync(ApiConfig.MatchWalkover(matchId), json, requireGameSecret: true, matchToken: matchToken);
            TrucoRulesScenarioLog.Backend("POST /walkover OK", "match=" + matchId + " claimer=" + claimerUserId);
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "walkover claimed match=" + matchId + " claimer=" + claimerUserId);
            await CloseCompletedMatchRowAsync(matchId);
            await GetCurrentUserProfile();
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            TrucoRulesScenarioLog.BackendFail("POST /walkover", ex.Message);
            Debug.LogWarning("[ApiController] - ClaimWalkover1v1: " + ex.Message);
            return false;
        }
    }

    /// <summary>Pre-game: /leave refunds entry, then /end closes the row. Returns true when /leave succeeded.</summary>
    public static async System.Threading.Tasks.Task<bool> CloseLobbyMatchForRefundAsync(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        bool left = false;
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts && !left; attempt++)
        {
            left = await TryNotifyPlayerLeftMatch1v1(matchId);
            if (!left && attempt < maxAttempts)
                await System.Threading.Tasks.Task.Delay(350 * attempt);
        }
        if (!left)
        {
            TrucoRulesScenarioLog.BackendFail("CloseLobbyMatchForRefund",
                "POST /leave failed after retries match=" + matchId);
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api,
                "CloseLobbyMatchForRefund: POST /leave failed after retries match=" + matchId);
            return false;
        }

        await System.Threading.Tasks.Task.Delay(200);
        await TryEndMatch1v1(matchId);
        return true;
    }

    /// <summary>After /result or /walkover: close row only — do NOT POST /leave (re-deducts winner).</summary>
    public static async System.Threading.Tasks.Task CloseCompletedMatchRowAsync(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        await TryEndMatch1v1(matchId);
    }

    /// <summary>Leave + end so GET /matches drops the row (backend + Photon webhooks also clean zombies).</summary>
    public static async System.Threading.Tasks.Task CloseMatchRowAfterGameAsync(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return;
        await CloseLobbyMatchForRefundAsync(matchId);
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
    /// Anti-cheat: short-lived token from POST /match-token — required for /result and /walkover.
    /// </summary>
    public static async Task<string> RequestMatchToken1v1(string matchId, Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId)) return null;
        try
        {
            string response = await HttpApiClient.PostAsync(ApiConfig.MatchRequestToken(matchId), "{}");
            string token = ParseMatchTokenFromJson(response);
            TrucoRulesScenarioLog.Backend("POST /match-token OK",
                "match=" + matchId + " hasToken=" + !string.IsNullOrEmpty(token));
            if (string.IsNullOrEmpty(token))
            {
                string msg = "match-token response missing token field";
                onError?.Invoke(msg);
                TrucoRulesScenarioLog.BackendFail("POST /match-token", msg + " raw=" + TruncateForLog(response, 120));
            }
            return token;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            TrucoRulesScenarioLog.BackendFail("POST /match-token", "match=" + matchId + " err=" + ex.Message);
            return null;
        }
    }

    static string ParseMatchTokenFromJson(string response)
    {
        if (string.IsNullOrEmpty(response)) return null;
        try
        {
            var flat = JsonUtility.FromJson<MatchTokenResponse>(response);
            if (!string.IsNullOrEmpty(flat?.matchToken)) return flat.matchToken.Trim();
            if (!string.IsNullOrEmpty(flat?.token)) return flat.token.Trim();
            if (!string.IsNullOrEmpty(flat?.data?.matchToken)) return flat.data.matchToken.Trim();
            if (!string.IsNullOrEmpty(flat?.data?.token)) return flat.data.token.Trim();
        }
        catch { }

        string lower = response.ToLowerInvariant();
        foreach (string key in new[] { "matchtoken", "match_token", "token" })
        {
            int idx = lower.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (idx < 0) continue;
            int colon = response.IndexOf(':', idx);
            if (colon < 0) continue;
            int q1 = response.IndexOf('"', colon + 1);
            if (q1 < 0) continue;
            int q2 = response.IndexOf('"', q1 + 1);
            if (q2 <= q1) continue;
            string val = response.Substring(q1 + 1, q2 - q1 - 1).Trim();
            if (!string.IsNullOrEmpty(val)) return val;
        }
        return null;
    }

    /// <summary>
    /// Reports winner; backend should mark match finished, settle coins, and remove both players from the joinable lobby
    /// (e.g. empty <c>players</c> or status <c>completed</c>) so new joins are not blocked by "Match is full".
    /// </summary>
    public static async Task<bool> SubmitMatchResult1v1(
        string matchId,
        string winnerUserId,
        string loserUserId = null,
        Action<string> onError = null)
    {
        if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(winnerUserId)) return false;
        string url = ApiConfig.MatchSubmitResult(matchId);
        try
        {
            string matchToken = await RequestMatchToken1v1(matchId, onError);
            var body = new MatchResultSubmitRequest
            {
                winnerId = winnerUserId,
                loserId = loserUserId,
                status = "completed",
                matchToken = matchToken
            };
            string json = JsonUtility.ToJson(body);
            TrucoRulesScenarioLog.Backend("POST /result attempt",
                "url=" + url + " body=" + json
                + " secret=" + HttpApiClient.HasGameSecretConfigured()
                + " hasToken=" + !string.IsNullOrEmpty(matchToken));
            string response = await HttpApiClient.PostAsync(url, json, requireGameSecret: true, matchToken: matchToken);
            TrucoRulesScenarioLog.Backend("POST /result OK",
                "match=" + matchId + " winner=" + winnerUserId
                + " response=" + TruncateForLog(response, 200));
            Debug.Log("[ApiController] - match result reported: " + matchId);
            return true;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
            TrucoRulesScenarioLog.BackendFail("POST /result",
                "match=" + matchId + " winner=" + winnerUserId + " err=" + ex.Message);
            Debug.LogWarning("[ApiController] - SubmitMatchResult1v1: " + ex.Message);
            return false;
        }
    }

    static string TruncateForLog(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    /// <summary>
    /// Notifies server this user left the match (early quit, scene unload, etc.). Requires <c>POST …/matches/:id/leave</c> on API.
    /// Fails quietly if route is missing so older servers keep working.
    /// </summary>
    public static async System.Threading.Tasks.Task<bool> TryNotifyPlayerLeftMatch1v1(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.MatchPlayerLeave(matchId), "{}");
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "match leave notified: " + matchId);
            return true;
        }
        catch (Exception ex)
        {
            if (IsBenignLeaveMatchError(ex.Message))
            {
                TrucoDebugLog.Log(TrucoDebugLog.Category.Api,
                    "match leave benign (treat OK): " + matchId + " — " + ex.Message);
                return true;
            }
            TrucoRulesScenarioLog.BackendFail("POST /leave", "match=" + matchId + " err=" + ex.Message);
            Debug.LogWarning("[ApiController] - TryNotifyPlayerLeftMatch1v1: " + ex.Message);
            return false;
        }
    }

    static bool IsBenignLeaveMatchError(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return false;
        string m = msg.ToLowerInvariant();
        return m.Contains("already left") || m.Contains("already removed")
               || m.Contains("not a participant") || m.Contains("not in match")
               || m.Contains("not in this match") || m.Contains("no longer")
               || m.Contains("not found") || m.Contains("cancelled") || m.Contains("canceled");
    }

    static bool WalletGainedAtLeast(int balBefore, int balAfter, int minGain)
    {
        if (balBefore < 0 || balAfter < 0) return false;
        if (minGain <= 0) return balAfter > balBefore;
        return balAfter >= balBefore + minGain;
    }

    public struct LobbyPurgeResult
    {
        public int attempted;
        public int succeeded;
        public int failed;
        public int mineStillVisible;
    }

    static bool NeedsMatchHydration(Player1v1Match m) =>
        m != null && (m.players == null || m.players.Length == 0)
                  && string.IsNullOrEmpty(m.createdBy) && string.IsNullOrEmpty(m.hostId);

    static async System.Threading.Tasks.Task<Player1v1Match> ResolveMatchForPurgeAsync(Player1v1Match m)
    {
        if (m == null || !NeedsMatchHydration(m)) return m;
        var full = await GetMatch1v1(m._id);
        return full ?? m;
    }

    static async System.Threading.Tasks.Task<bool> TryEndMatch1v1Bool(string matchId)
    {
        if (string.IsNullOrEmpty(matchId)) return false;
        try
        {
            await HttpApiClient.PostAsync(ApiConfig.MatchEnd(matchId), "{}");
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "match end requested: " + matchId);
            return true;
        }
        catch (Exception ex)
        {
            TrucoDebugLog.Warn(TrucoDebugLog.Category.Api, "TryEndMatch1v1: " + ex.Message);
            return false;
        }
    }

    static async System.Threading.Tasks.Task<bool> TryCloseLobbyMatchForUserAsync(Player1v1Match m, string userId)
    {
        if (m == null || string.IsNullOrEmpty(m._id) || string.IsNullOrEmpty(userId)) return false;
        int stake = m.GetEntryStake();
        int balBefore = GetSessionUser?.Data?.wallet?.balance ?? -1;
        bool leaveOk = await CloseLobbyMatchForRefundAsync(m._id);
        await GetCurrentUserProfile();
        int balAfter = GetSessionUser?.Data?.wallet?.balance ?? -1;
        if (leaveOk || WalletGainedAtLeast(balBefore, balAfter, stake))
            return true;

        // Never POST /end without a successful /leave — that closes the row with NO refund.
        TrucoRulesScenarioLog.BackendFail("Purge lobby close",
            "match=" + m._id + " leaveOk=false stake=" + stake
            + " balBefore=" + balBefore + " balAfter=" + balAfter);
        if (m.IsCurrentUserHostOfRoom() || OneVsOneLobbyFlowRules.MatchBelongsToUser(m, userId))
            return await AdminForceCloseMatch1v1(m._id);
        return false;
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

    public static async System.Threading.Tasks.Task<int> LeaveAllMyActiveLobbyMatchesAsync(
        string exceptMatchId = null)
    {
        var result = await PurgeAllMyLobbyMatchesAsync(exceptMatchId);
        return result.succeeded;
    }

    /// <summary>POST /leave (and host fallbacks) on every lobby row tied to the logged-in user.</summary>
    public static async System.Threading.Tasks.Task<LobbyPurgeResult> PurgeAllMyLobbyMatchesAsync(
        string exceptMatchId = null)
    {
        var result = new LobbyPurgeResult();
        if (!await EnsureSessionUserLoadedAsync()) return result;
        string uid = GetSessionUser?.Data?._id;
        if (string.IsNullOrEmpty(uid)) return result;

        // Always try the persisted unused-room id first — Photon may have already
        // closed the lobby row so it no longer appears in GET /matches.
        string remembered = TrucoActiveHostMatchStore.GetRememberedMatchId();
        if (!string.IsNullOrEmpty(remembered) && remembered != exceptMatchId)
        {
            result.attempted++;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Purge remembered unused room=" + remembered);
            if (await CancelPreGameMatch1v1(remembered, TrucoActiveHostMatchStore.GetRememberedEntryFee()))
                result.succeeded++;
            else
                result.failed++;
        }

        var list = await FetchPlayer1v1MatchList();
        if (list == null || list.Count == 0) return result;

        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrEmpty(m._id)) continue;
            if (!string.IsNullOrEmpty(exceptMatchId) && m._id == exceptMatchId) continue;
            if (!m.IsLobbyLikeStatus()) continue;

            if (OneVsOneLobbyFlowRules.MatchBelongsToUser(m, uid))
            {
                result.attempted++;
                TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Purge my lobby match=" + m._id);
                if (await TryCloseLobbyMatchForUserAsync(m, uid))
                    result.succeeded++;
                else
                    result.failed++;
                await System.Threading.Tasks.Task.Delay(100);
                continue;
            }

            var resolved = await ResolveMatchForPurgeAsync(m);
            if (!OneVsOneLobbyFlowRules.MatchBelongsToUser(resolved, uid)) continue;

            result.attempted++;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Purge lobby match (hydrated)=" + m._id);
            if (await TryCloseLobbyMatchForUserAsync(resolved, uid))
                result.succeeded++;
            else
                result.failed++;
            await System.Threading.Tasks.Task.Delay(100);
        }

        if (result.attempted > 0)
            await GetCurrentUserProfile();

        var after = await FetchPlayer1v1MatchList();
        if (after != null)
        {
            for (int i = 0; i < after.Count; i++)
            {
                var m = after[i];
                if (m == null || !m.IsLobbyLikeStatus()) continue;
                var resolved = await ResolveMatchForPurgeAsync(m);
                if (OneVsOneLobbyFlowRules.MatchBelongsToUser(resolved, uid))
                    result.mineStillVisible++;
            }
        }

        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby,
            "Bulk purge finished attempted=" + result.attempted + " ok=" + result.succeeded +
            " failed=" + result.failed + " mineStillVisible=" + result.mineStillVisible);
        return result;
    }

    /// <summary>Admin/dev: force-close every lobby-like match visible on the dashboard.</summary>
    public static async System.Threading.Tasks.Task<int> AdminForceCloseAllLobbyMatchesAsync(
        System.Action<string> onError = null)
    {
        var list = await FetchLiveActiveMatchesForDashboard();
        if (list == null || list.Count == 0) return 0;
        int closed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || string.IsNullOrEmpty(m._id)) continue;
            if (!m.IsLobbyLikeStatus()) continue;
            TrucoDebugLog.Log(TrucoDebugLog.Category.Api, "Admin force-close match=" + m._id);
            if (await AdminForceCloseMatch1v1(m._id, onError))
                closed++;
            await System.Threading.Tasks.Task.Delay(80);
        }
        TrucoDebugLog.Log(TrucoDebugLog.Category.Lobby, "Admin force-close finished: " + closed + " matches");
        return closed;
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
