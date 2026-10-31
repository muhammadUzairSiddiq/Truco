using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using Photon.Realtime;

/// <summary>Private, short-lived backend tickets authenticate immutable Photon user IDs. Never falls back to anonymous play.</summary>
public static class TrucoPhotonAuthentication
{
    [Serializable] public sealed class Ticket
    {
        public string provider, userId, mode, tournamentId, matchId, authToken, expiresAt, appVersion, ticketId;
        public List<string> allowedRooms;
    }
    [Serializable] sealed class Request { public string mode, tournamentId, matchId, roomName; }
    static Task<bool> _connection;
    static string _pendingScope, _ticketScope;
    static Ticket _ticket;
    static int _generation, _readyWaiters;
    public static bool IsAuthenticating => _readyWaiters > 0 || _connection != null && !_connection.IsCompleted;
    public static string LastError { get; private set; }
    static string LocalId => ApiController.GetSessionUser?.Data?._id;
    static string Mode(string value) => value == "1v1" ? "one_vs_one" : value;

    public static Task<bool> ConnectUsingSettingsAsync(string mode, string tournamentId = null, string matchId = null, string roomName = null)
    {
        mode = Mode(mode);
        string scope = LocalId + ":" + mode + ":" + tournamentId + ":" + matchId + ":" + roomName;
        if (_connection != null && !_connection.IsCompleted)
            return _pendingScope == scope ? _connection : AfterCurrent(_connection, LocalId, mode, tournamentId, matchId, roomName);
        _pendingScope = scope;
        _connection = ConnectCore(mode, tournamentId, matchId, roomName, scope);
        return _connection;
    }
    static async Task<bool> AfterCurrent(Task<bool> previous, string expectedUser, string mode, string tournamentId, string matchId, string roomName)
    {
        int generation = _generation, sessionGeneration = HttpApiClient.SessionGeneration;
        await previous;
        return generation == _generation && sessionGeneration == HttpApiClient.SessionGeneration
            && expectedUser == LocalId && await ConnectUsingSettingsAsync(mode, tournamentId, matchId, roomName);
    }
    public static async Task<bool> ConnectAndWaitUntilReadyAsync(string mode, string tournamentId = null,
        string matchId = null, string roomName = null, int timeoutSeconds = 20)
    {
        string user = LocalId; int generation = _generation, sessionGeneration = HttpApiClient.SessionGeneration;
        _readyWaiters++;
        try
        {
            if (!await ConnectUsingSettingsAsync(mode, tournamentId, matchId, roomName)) return false;
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, Math.Min(60, timeoutSeconds)));
            while (DateTime.UtcNow < deadline && generation == _generation && sessionGeneration == HttpApiClient.SessionGeneration && LocalId == user)
            {
                if (IsVerifiedReady(user)) return true;
                if (!PhotonNetwork.IsConnected && PhotonNetwork.NetworkClientState == ClientState.Disconnected) break;
                await Task.Delay(50);
            }
            LastError = "No se pudo confirmar una conexión multijugador autenticada. No se autorizó una nueva entrada.";
            return false;
        }
        finally { _readyWaiters--; }
    }
    public static bool IsAuthenticatedForCurrentAccount => IsVerifiedConnection(LocalId);
    public static bool IsAuthorizedForRoom(string mode, string tournamentId, string matchId, string roomName) =>
        IsVerifiedConnection(LocalId) && !string.IsNullOrWhiteSpace(roomName) && _ticket.mode == Mode(mode)
        && _ticket.tournamentId == tournamentId && _ticket.matchId == matchId && _ticket.allowedRooms.Contains(roomName);
    static bool IsVerifiedReady(string expectedUser) => IsVerifiedConnection(expectedUser) && PhotonNetwork.Server == ServerConnection.MasterServer;
    static bool IsVerifiedConnection(string expectedUser) => !string.IsNullOrWhiteSpace(expectedUser)
        && !PhotonNetwork.OfflineMode && PhotonNetwork.IsConnectedAndReady && PhotonNetwork.AuthValues?.AuthType == CustomAuthenticationType.Custom
        && PhotonNetwork.LocalPlayer?.UserId == expectedUser && _ticket?.userId == expectedUser;

    static async Task<bool> ConnectCore(string mode, string tournamentId, string matchId, string roomName, string scope)
    {
        int generation = _generation, sessionGeneration = HttpApiClient.SessionGeneration; string user = LocalId; LastError = null;
        try
        {
            if (string.IsNullOrWhiteSpace(user) || mode != "one_vs_one" && mode != "tournament" && mode != "spectator")
                throw new InvalidOperationException();
            if (_ticketScope == scope && IsVerifiedConnection(user) && ValidUntil(_ticket.expiresAt) > DateTimeOffset.UtcNow.AddSeconds(5)) return true;
            var route = ApiConfig.Custom("PhotonSession");
            if (string.IsNullOrWhiteSpace(route)) throw new InvalidOperationException();
            var payload = new Request { mode = mode, tournamentId = tournamentId, matchId = matchId, roomName = roomName };
            var ticket = ReadTicket(await HttpApiClient.PostAsync(route, JsonConvert.SerializeObject(payload)), user, mode, tournamentId, matchId, roomName);
            if (generation != _generation || sessionGeneration != HttpApiClient.SessionGeneration || LocalId != user) return false;
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
                var deadline = DateTime.UtcNow.AddSeconds(8);
                while (PhotonNetwork.IsConnected && DateTime.UtcNow < deadline) await Task.Delay(50);
                if (PhotonNetwork.IsConnected || generation != _generation || sessionGeneration != HttpApiClient.SessionGeneration || LocalId != user) return false;
            }
            var auth = new AuthenticationValues(user) { AuthType = CustomAuthenticationType.Custom };
            auth.SetAuthPostData(new Dictionary<string, object> { ["token"] = ticket.authToken, ["mode"] = ticket.mode,
                ["tournamentId"] = tournamentId, ["matchId"] = matchId });
            PhotonNetwork.AuthValues = auth;
            _ticket = ticket; _ticketScope = scope;
            TrucoPhotonRegionSettings.ApplyToPhoton();
            // PUN overwrites GameVersion from AppSettings inside ConnectUsingSettings.
            var settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());
            settings.AppVersion = ticket.appVersion;
            bool connected = PhotonNetwork.ConnectUsingSettings(settings);
            if (!connected) LastError = "No se pudo iniciar la conexión multijugador autenticada.";
            return connected;
        }
        catch (Exception)
        {
            LastError = "El servidor no confirmó una sesión multijugador segura. Volvé a intentarlo más tarde.";
            return false;
        }
    }
    static DateTimeOffset ValidUntil(string value)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
            throw new InvalidOperationException();
        return result;
    }
    public static Ticket ReadTicket(string json, string expectedUser, string mode, string tournamentId = null,
        string matchId = null, string roomName = null)
    {
        try
        {
            var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            var data = root?["data"] as JObject;
            if (root == null) throw new InvalidOperationException();
            if (root["success"]?.Type != JTokenType.Boolean || !root["success"].Value<bool>()
                || data?["schemaVersion"]?.Type != JTokenType.Integer || data["schemaVersion"].Value<int>() != 1
                || data["ticket"] is not JObject value) throw new InvalidOperationException();
            foreach (string field in new[] { "provider", "userId", "mode", "authToken", "expiresAt", "appVersion", "ticketId" })
                if (value[field]?.Type != JTokenType.String) throw new InvalidOperationException();
            foreach (string field in new[] { "tournamentId", "matchId" })
                if (value[field] == null || value[field].Type != JTokenType.Null && value[field].Type != JTokenType.String) throw new InvalidOperationException();
            if (value["allowedRooms"] is not JArray allowedRooms || allowedRooms.Count > 128) throw new InvalidOperationException();
            foreach (var room in allowedRooms) if (room.Type != JTokenType.String) throw new InvalidOperationException();
            var ticket = value.ToObject<Ticket>();
            mode = Mode(mode);
            if (mode != "one_vs_one" && mode != "tournament" && mode != "spectator"
                || string.IsNullOrWhiteSpace(expectedUser) || ticket?.provider != "photon" || ticket.userId != expectedUser || ticket.mode != mode
                || ticket.tournamentId != tournamentId || ticket.matchId != matchId || ticket.allowedRooms == null
                || string.IsNullOrWhiteSpace(ticket.authToken) || ticket.authToken.Length > 8192 || string.IsNullOrWhiteSpace(ticket.ticketId)
                || string.IsNullOrWhiteSpace(ticket.appVersion) || ticket.appVersion.Length > 64
                || ValidUntil(ticket.expiresAt) <= DateTimeOffset.UtcNow.AddSeconds(5)
                || ValidUntil(ticket.expiresAt) > DateTimeOffset.UtcNow.AddDays(1)
                || !string.IsNullOrEmpty(roomName) && !ticket.allowedRooms.Contains(roomName)) throw new InvalidOperationException();
            var rooms = new HashSet<string>(StringComparer.Ordinal);
            foreach (string allowed in ticket.allowedRooms)
                if (string.IsNullOrWhiteSpace(allowed) || !rooms.Add(allowed)) throw new InvalidOperationException();
            return ticket;
        }
        catch (Exception exception) when (exception is JsonException || exception is ArgumentException || exception is InvalidOperationException
            || exception is FormatException || exception is OverflowException)
        { throw new InvalidOperationException("El servidor no confirmó un permiso multijugador válido para esta cuenta y mesa."); }
    }
    public static void Clear()
    {
        _generation++; _ticket = null; _ticketScope = _pendingScope = null; _connection = null;
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        PhotonNetwork.AuthValues = null;
    }
    public static void ClearSession() => Clear();
}
