using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MH.Multiplayer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Versioned authoritative tournament contract. Photon carries transport hints, never settlement authority.</summary>
public static class TrucoTournamentService
{
    [Serializable] public sealed class Participant
    {
        public string playerId, name, registrationId, status;
        public int position;
        public bool entryPaid;
    }
    [Serializable] public sealed class Match
    {
        public string matchId, roomName, firstPlayerId, secondPlayerId, winnerId, status;
        public int round, index;
    }
    [Serializable] public sealed class Assignment
    {
        public string matchId, roomName;
        public int round, index;
        public List<string> playerIds;
    }
    [Serializable] public sealed class Payout
    {
        public string status, currency, payoutId, championId;
        public int? amount;
    }
    [Serializable] public sealed class Refund
    {
        public string playerId, status, refundId;
        public int amount;
    }
    [Serializable] public sealed class Wallet { public int available, held; public long revision; }
    [Serializable] public sealed class Snapshot
    {
        public string tournamentId, name, status, localStatus;
        public long revision;
        public int maxPlayers, targetScore, entryFee, advertisedPrize;
        public bool withFlor;
        public List<Participant> participants;
        public List<Match> matches;
        public Assignment localAssignment;
        public Payout payout;
        public List<Refund> refunds;
        public Wallet wallet;
    }
    [Serializable] public sealed class Mutation
    {
        public string idempotencyKey, matchId, winnerId, reason;
        public long expectedRevision;
        public int? localScore, opponentScore, handRound;
    }

    static readonly HashSet<string> States = new HashSet<string> { "registration", "starting", "playing", "completed", "cancelled", "reconciliation_required" };
    static readonly HashSet<string> LocalStates = new HashSet<string> { "not_registered", "registered", "waiting", "playing", "eliminated", "withdrawn", "champion" };
    static string LocalId => ApiController.GetSessionUser?.Data?._id;

    static string Route(string key, string tournamentId)
    {
        if (string.IsNullOrWhiteSpace(tournamentId)) throw new InvalidOperationException("Identificador de torneo inválido.");
        var route = ApiConfig.Custom(key, Uri.EscapeDataString(tournamentId));
        if (string.IsNullOrWhiteSpace(route)) throw new InvalidOperationException("El servicio de torneos todavía no está configurado.");
        return route;
    }
    public static async Task<Snapshot> Get(string tournamentId, Snapshot previous = null) =>
        ReadReceipt(await HttpApiClient.GetAsync(Route("TournamentSnapshot", tournamentId)), tournamentId, LocalId, previous);

    /// <summary>Display-only compatibility DTO; advertisedPrize never represents a paid receipt.</summary>
    public static Tournament ToLegacyTournament(Snapshot snapshot)
    {
        var users = snapshot.participants.ToDictionary(p => p.playerId, p => new User { _id = p.playerId, username = p.name, status = p.status });
        var final = snapshot.matches.FirstOrDefault(m => m.round == (snapshot.maxPlayers == 8 ? 2 : 1) && m.index == 0);
        return new Tournament { _id = snapshot.tournamentId, name = snapshot.name, status = snapshot.status,
            entryFee = snapshot.entryFee, maxPlayers = snapshot.maxPlayers, withFlor = snapshot.withFlor, targetScore = snapshot.targetScore,
            prizePool = snapshot.advertisedPrize, participants = snapshot.participants.Where(p => p.status != "withdrawn").Select(p => users[p.playerId]).ToList(),
            champion = snapshot.status == "completed" && final?.status == "finished" ? new List<User> { users[final.winnerId] } : new List<User>(),
            matches = snapshot.matches.Where(m => !string.IsNullOrEmpty(m.matchId)).Select(m => new match {
                _id = m.matchId, status = m.status,
                players = new[] { m.firstPlayerId, m.secondPlayerId }.Where(id => !string.IsNullOrEmpty(id)).Select(id => users[id]).ToList(),
                winner = m.status == "finished" ? users[m.winnerId] : null }).ToList() };
    }

    public static Task<Snapshot> Join(Snapshot snapshot) => Mutate("TournamentJoin", "join", snapshot);
    public static Task<Snapshot> Start(Snapshot snapshot) => Mutate("TournamentStart", "start", snapshot);
    public static Task<Snapshot> Leave(Snapshot snapshot, string reason = "player_leave") =>
        Mutate("TournamentLeave", "leave", snapshot, reason: reason);
    public static Task<Snapshot> Reconcile(Snapshot snapshot, string reason) =>
        Mutate("TournamentReconcile", "reconcile", snapshot, reason: reason);
    public static Task<Snapshot> FinalizeMatch(Snapshot snapshot, string matchId, string winnerId,
        string reason = "score", int? localScore = null, int? opponentScore = null, int? handRound = null) =>
        Mutate("TournamentFinalizeMatch", "finalize-match", snapshot, matchId, winnerId, reason, localScore, opponentScore, handRound);
    public static Task<Snapshot> Finalize(Snapshot snapshot) => Mutate("TournamentFinalize", "finalize", snapshot);

    static async Task<Snapshot> Mutate(string route, string operation, Snapshot snapshot, string matchId = null,
        string winnerId = null, string reason = null, int? localScore = null, int? opponentScore = null, int? handRound = null)
    {
        if (snapshot == null || string.IsNullOrEmpty(LocalId)) throw new InvalidOperationException("No se pudo verificar la sesión del torneo.");
        var key = OperationKey(snapshot.tournamentId, operation, matchId);
        var body = new Mutation { idempotencyKey = key, expectedRevision = snapshot.revision, matchId = matchId,
            winnerId = winnerId, reason = reason, localScore = localScore, opponentScore = opponentScore, handRound = handRound };
        string storage = KeyName(snapshot.tournamentId, operation, matchId) + "_body";
        string jsonBody = PlayerPrefs.GetString(storage, "");
        if (jsonBody.Length == 0) { jsonBody = JsonConvert.SerializeObject(body); PlayerPrefs.SetString(storage, jsonBody); PlayerPrefs.Save(); }
        var original = JsonConvert.DeserializeObject<Mutation>(jsonBody);
        if (original == null || original.idempotencyKey != key || original.matchId != matchId || original.winnerId != winnerId || original.reason != reason)
            throw new InvalidOperationException("Existe una solicitud pendiente con otros datos. Consultá el estado antes de enviar otra.");
        var result = ReadReceipt(await HttpApiClient.PostAsync(Route(route, snapshot.tournamentId), jsonBody),
            snapshot.tournamentId, LocalId, snapshot, key);
        if (operation == "reconcile") ForgetOperation(snapshot.tournamentId, operation, matchId);
        if (operation == "leave" && (result.localStatus == "withdrawn" || result.status == "cancelled"))
            ForgetOperation(snapshot.tournamentId, "join", null);
        return result;
    }

    static string KeyName(string tournamentId, string operation, string subject)
    {
        using (var sha = SHA256.Create())
            return "truco_tournament_operation_v1_" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                LocalId + ":" + tournamentId + ":" + operation + ":" + subject))).Replace("-", "");
    }
    static string OperationKey(string tournamentId, string operation, string subject)
    {
        string name = KeyName(tournamentId, operation, subject), key = PlayerPrefs.GetString(name, "");
        if (key.Length == 0) { key = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(name, key); PlayerPrefs.Save(); }
        return key;
    }
    static void ForgetOperation(string tournamentId, string operation, string subject)
    {
        string key = KeyName(tournamentId, operation, subject);
        PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + "_body"); PlayerPrefs.Save();
    }

    public static Snapshot ReadReceipt(string json, string expectedTournament, string localPlayerId,
        Snapshot previous = null, string idempotencyKey = null)
    {
        try
        {
            var root = JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
            if (root == null) throw new InvalidOperationException();
            var data = root["data"] as JObject;
            var raw = data?["snapshot"] as JObject;
            if (root["success"]?.Type != JTokenType.Boolean || !root["success"].Value<bool>()
                || root["ok"]?.Type == JTokenType.Boolean && !root["ok"].Value<bool>()
                || data?["schemaVersion"]?.Type != JTokenType.Integer || data["schemaVersion"].Value<int>() != 1
                || raw == null || raw["revision"]?.Type != JTokenType.Integer
                || raw["maxPlayers"]?.Type != JTokenType.Integer || raw["targetScore"]?.Type != JTokenType.Integer
                || raw["entryFee"]?.Type != JTokenType.Integer || raw["advertisedPrize"]?.Type != JTokenType.Integer
                || raw["withFlor"]?.Type != JTokenType.Boolean || raw["participants"] is not JArray
                || raw["matches"] is not JArray || raw["refunds"] is not JArray
                || raw["wallet"] is not JObject || raw["wallet"]["available"]?.Type != JTokenType.Integer
                || raw["wallet"]["held"]?.Type != JTokenType.Integer || raw["wallet"]["revision"]?.Type != JTokenType.Integer
                || idempotencyKey != null && (string)data["idempotencyKey"] != idempotencyKey)
                throw new InvalidOperationException();
            if (((JArray)raw["participants"]).Any(p => p["position"]?.Type != JTokenType.Integer || p["entryPaid"]?.Type != JTokenType.Boolean)
                || ((JArray)raw["matches"]).Any(m => m["round"]?.Type != JTokenType.Integer || m["index"]?.Type != JTokenType.Integer)
                || ((JArray)raw["refunds"]).Any(r => r["amount"]?.Type != JTokenType.Integer)
                || raw["payout"] is JObject payout && (string)payout["status"] == "paid" && payout["amount"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException();
            var snapshot = raw.ToObject<Snapshot>();
            Validate(snapshot, expectedTournament, localPlayerId, previous);
            return snapshot;
        }
        catch (Exception exception) when (exception is JsonException || exception is InvalidOperationException
            || exception is ArgumentException || exception is OverflowException || exception is FormatException)
        {
            throw new InvalidOperationException("El servidor no confirmó un estado de torneo válido. No se avanzó ni se modificó el saldo.");
        }
    }

    public static void Validate(Snapshot value, string tournamentId, string localPlayerId, Snapshot previous = null)
    {
        if (value == null || value.tournamentId != tournamentId || value.revision < 0 || !States.Contains(value.status ?? "")
            || !LocalStates.Contains(value.localStatus ?? "") || (value.maxPlayers != 4 && value.maxPlayers != 8)
            || (value.targetScore != 15 && value.targetScore != 30) || value.entryFee < 0 || value.advertisedPrize < 0
            || value.wallet == null || value.wallet.available < 0 || value.wallet.held < 0 || value.wallet.revision < 0
            || value.participants == null || value.matches == null || value.refunds == null || value.participants.Count > value.maxPlayers)
            throw new InvalidOperationException("Incomplete tournament snapshot");
        var ids = new HashSet<string>(); var positions = new HashSet<int>();
        foreach (var player in value.participants)
            if (player == null || string.IsNullOrWhiteSpace(player.playerId) || string.IsNullOrWhiteSpace(player.registrationId)
                || !ids.Add(player.playerId) || player.position < 0 || player.position >= value.maxPlayers || !positions.Add(player.position)
                || player.status != "registered" && player.status != "eliminated" && player.status != "withdrawn")
                throw new InvalidOperationException("Invalid tournament participants");
        bool started = value.status == "playing" || value.status == "completed" || value.status == "starting";
        if (started && (value.participants.Count != value.maxPlayers || value.participants.Any(p => !p.entryPaid)))
            throw new InvalidOperationException("Unconfirmed tournament entries");
        if (value.localStatus != "not_registered" && value.localStatus != "withdrawn"
            && !value.participants.Any(p => p.playerId == localPlayerId && p.status != "withdrawn"))
            throw new InvalidOperationException("Foreign authenticated tournament state");
        int rounds = value.maxPlayers == 8 ? 3 : 2;
        var keys = new HashSet<string>(); var matchIds = new HashSet<string>();
        foreach (var match in value.matches)
        {
            if (match == null || match.round < 0 || match.round >= rounds || match.index < 0
                || match.index >= value.maxPlayers / (1 << (match.round + 1)) || !keys.Add(match.round + ":" + match.index)
                || match.status != "waiting" && match.status != "ready" && match.status != "playing" && match.status != "finished" && match.status != "cancelled")
                throw new InvalidOperationException("Invalid tournament matches");
            bool playable = match.status == "ready" || match.status == "playing" || match.status == "finished";
            if (playable && (string.IsNullOrWhiteSpace(match.matchId) || !matchIds.Add(match.matchId)
                || match.roomName != TournamentBracketState.MatchRoomName(tournamentId, match.round, match.index)
                || !ids.Contains(match.firstPlayerId ?? "") || !ids.Contains(match.secondPlayerId ?? "") || match.firstPlayerId == match.secondPlayerId))
                throw new InvalidOperationException("Unconfirmed tournament table");
            if (match.status == "finished" && match.winnerId != match.firstPlayerId && match.winnerId != match.secondPlayerId)
                throw new InvalidOperationException("Invalid tournament winner");
            if (match.status != "finished" && !string.IsNullOrEmpty(match.winnerId)) throw new InvalidOperationException("Unconfirmed tournament winner");
        }
        if (started && value.matches.Count != value.maxPlayers - 1) throw new InvalidOperationException("Incomplete tournament bracket");
        if (started && !TournamentStartupValidation.IsValidBracketProjection(ToBracket(value), value.maxPlayers))
            throw new InvalidOperationException("Inconsistent tournament bracket");
        if (value.localAssignment != null)
        {
            var assignment = value.localAssignment;
            var table = value.matches.FirstOrDefault(m => m.round == assignment.round && m.index == assignment.index);
            if (table == null || table.status != "ready" && table.status != "playing" || table.matchId != assignment.matchId
                || table.roomName != assignment.roomName || assignment.playerIds == null || assignment.playerIds.Count != 2
                || !assignment.playerIds.Contains(localPlayerId) || !assignment.playerIds.Contains(table.firstPlayerId)
                || !assignment.playerIds.Contains(table.secondPlayerId)) throw new InvalidOperationException("Foreign tournament assignment");
        }
        var refundedPlayers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var refund in value.refunds)
            if (refund == null || refund.amount < 0 || string.IsNullOrWhiteSpace(refund.playerId)
                || !refundedPlayers.Add(refund.playerId)
                || refund.status != "pending" && refund.status != "refunded" && refund.status != "not_due"
                || refund.status == "not_due" && refund.amount != 0
                || refund.status == "refunded" && string.IsNullOrWhiteSpace(refund.refundId)) throw new InvalidOperationException("Invalid refund receipt");
        if (value.status == "cancelled" && value.entryFee > 0)
            foreach (var participant in value.participants.Where(p => p.entryPaid))
            {
                var refund = value.refunds.SingleOrDefault(r => r.playerId == participant.playerId);
                if (refund == null || refund.status == "not_due" || refund.amount != value.entryFee)
                    throw new InvalidOperationException("Cancelled paid entries require a full refund without commission");
            }
        if (value.payout != null && value.payout.status != "pending" && value.payout.status != "paid" && value.payout.status != "not_due")
            throw new InvalidOperationException("Invalid payout state");
        if (value.payout?.status == "paid" && !HasPaidPayout(value)) throw new InvalidOperationException("Unconfirmed payout");
        if (value.localStatus == "champion" && !value.matches.Any(m => m.round == rounds - 1 && m.index == 0
            && m.status == "finished" && m.winnerId == localPlayerId)) throw new InvalidOperationException("Unconfirmed champion");
        if (previous != null)
        {
            if (previous.tournamentId != value.tournamentId || value.revision < previous.revision || value.wallet.revision < previous.wallet.revision)
                throw new InvalidOperationException("Stale tournament revision");
            if (value.revision == previous.revision && Canonical(value) != Canonical(previous))
                throw new InvalidOperationException("Conflicting tournament revision");
            if ((previous.status == "completed" || previous.status == "cancelled") && value.status != previous.status)
                throw new InvalidOperationException("Terminal tournament state changed");
            if (previous.payout?.status == "paid" && (value.payout?.status != "paid" || value.payout.payoutId != previous.payout.payoutId
                || value.payout.amount != previous.payout.amount || value.payout.championId != previous.payout.championId))
                throw new InvalidOperationException("Confirmed payout changed");
            foreach (var refund in previous.refunds.Where(r => r.status == "refunded"))
                if (!value.refunds.Any(r => r.playerId == refund.playerId && r.refundId == refund.refundId
                    && r.amount == refund.amount && r.status == "refunded")) throw new InvalidOperationException("Confirmed refund changed");
            if (previous.matches.Count > 0)
            {
                if (previous.maxPlayers != value.maxPlayers || previous.withFlor != value.withFlor || previous.targetScore != value.targetScore
                    || previous.participants.Any(p => !value.participants.Any(n => n.playerId == p.playerId && n.position == p.position)))
                    throw new InvalidOperationException("Tournament assignments changed");
                foreach (var old in previous.matches.Where(m => !string.IsNullOrEmpty(m.matchId)))
                    if (!value.matches.Any(m => m.round == old.round && m.index == old.index && m.matchId == old.matchId
                        && m.roomName == old.roomName && m.firstPlayerId == old.firstPlayerId && m.secondPlayerId == old.secondPlayerId
                        && (old.status != "finished" || m.status == "finished" && m.winnerId == old.winnerId)))
                        throw new InvalidOperationException("Tournament result or table identity changed");
            }
        }
        if (value.matches.Count > 0) ToBracket(value);
    }

    static string Canonical(Snapshot value)
    {
        var json = JObject.FromObject(value);
        json["participants"] = JArray.FromObject(value.participants.OrderBy(p => p.position));
        json["matches"] = JArray.FromObject(value.matches.OrderBy(m => m.round).ThenBy(m => m.index));
        json["refunds"] = JArray.FromObject(value.refunds.OrderBy(r => r.playerId, StringComparer.Ordinal));
        if (value.localAssignment != null) json["localAssignment"]["playerIds"] = JArray.FromObject(value.localAssignment.playerIds.OrderBy(id => id, StringComparer.Ordinal));
        return json.ToString(Formatting.None);
    }

    public static bool HasPaidPayout(Snapshot value)
    {
        var payout = value?.payout;
        var final = value?.matches?.FirstOrDefault(m => m.round == (value.maxPlayers == 8 ? 2 : 1) && m.index == 0);
        return value?.status == "completed" && (value.maxPlayers == 4 || value.maxPlayers == 8)
            && payout?.status == "paid" && payout.amount.HasValue && payout.amount.Value >= 0
            && payout.currency == "TRUCOINS" && !string.IsNullOrWhiteSpace(payout.payoutId)
            && final?.status == "finished" && !string.IsNullOrWhiteSpace(final.winnerId) && payout.championId == final.winnerId;
    }

    public static TournamentBracketState ToBracket(Snapshot snapshot)
    {
        var state = new TournamentBracketState(snapshot.maxPlayers);
        var byPosition = new User[snapshot.maxPlayers];
        foreach (var p in snapshot.participants) byPosition[p.position] = new User { _id = p.playerId, username = p.name };
        state.Seed(byPosition);
        foreach (var match in snapshot.matches.OrderBy(m => m.round).ThenBy(m => m.index))
        {
            var projected = state.rounds[match.round][match.index];
            if ((projected.first.playerId ?? "") != (match.firstPlayerId ?? "")
                || (projected.second.playerId ?? "") != (match.secondPlayerId ?? "")) throw new InvalidOperationException("Bracket ancestry mismatch");
            if (match.status == "finished")
            {
                var name = snapshot.participants.FirstOrDefault(p => p.playerId == match.winnerId)?.name;
                if (!state.RecordWinner(match.round, match.index, match.winnerId, name)) throw new InvalidOperationException("Invalid bracket result");
            }
        }
        return state;
    }
    public static TournamentMatch AssignedTable(Snapshot snapshot)
    {
        var assignment = snapshot?.localAssignment;
        if (assignment == null) return null;
        return new TournamentMatch { matchId = assignment.matchId, roomName = assignment.roomName, round = assignment.round,
            matchIndex = assignment.index, playerIds = new List<string>(assignment.playerIds),
            playerNames = assignment.playerIds.Select(id => snapshot.participants.First(p => p.playerId == id).name).ToList() };
    }
}
