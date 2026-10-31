using System;
using System.Collections.Generic;
using MH.Multiplayer;

/// <summary>Checks transport data before a tournament can leave its lobby. No backend authority.</summary>
public static class TournamentStartupValidation
{
    public static bool IsValidBracketProjection(TournamentBracketState state, int capacity)
    {
        if (state == null || (capacity != 4 && capacity != 8) || state.maxPlayers != capacity
            || state.rounds == null || state.rounds.Count != (capacity == 8 ? 3 : 2)) return false;
        for (int round = 0, count = capacity / 2; round < state.rounds.Count; round++, count /= 2)
        {
            var matches = state.rounds[round];
            if (matches == null || matches.Count != count) return false;
            var players = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < matches.Count; index++)
            {
                var match = matches[index];
                if (match == null || match.round != round || match.index != index
                    || match.first == null || match.second == null) return false;
                foreach (var slot in new[] { match.first, match.second })
                {
                    if (string.IsNullOrWhiteSpace(slot.playerId)) { if (round == 0) return false; }
                    else if (!players.Add(slot.playerId)) return false;
                }
                if (match.IsFinished && (!match.IsReady
                    || match.winnerId != match.first.playerId && match.winnerId != match.second.playerId)) return false;
                if (round > 0)
                {
                    var lower = state.rounds[round - 1];
                    if ((match.first.playerId ?? "") != (lower[index * 2].winnerId ?? "")
                        || (match.second.playerId ?? "") != (lower[index * 2 + 1].winnerId ?? "")) return false;
                }
            }
        }
        return true;
    }

    public static bool CanMergeBracketProjection(TournamentBracketState current, TournamentBracketState incoming, int capacity)
    {
        if (!IsValidBracketProjection(incoming, capacity)) return false;
        if (current == null) return true;
        if (!IsValidBracketProjection(current, capacity)) return false;
        // Once known, opening positions are immutable within this client session.
        for (int index = 0; index < current.rounds[0].Count; index++)
            if (current.rounds[0][index].first.playerId != incoming.rounds[0][index].first.playerId
                || current.rounds[0][index].second.playerId != incoming.rounds[0][index].second.playerId) return false;
        return true;
    }

    public static bool TryReadBracketResult(object raw, int round, int index,
        out TournamentBracketState.BracketMatch result)
    {
        result = null;
        if (raw is not string json || string.IsNullOrWhiteSpace(json)) return false;
        try { result = UnityEngine.JsonUtility.FromJson<TournamentBracketState.BracketMatch>(json); }
        catch (ArgumentException) { return false; }
        if (result == null || result.round != round || result.index != index || string.IsNullOrWhiteSpace(result.winnerId))
        { result = null; return false; }
        return true;
    }

    public static bool IsValidTableAssignment(TournamentMatch table, string tournamentId, int capacity,
        string localPlayerId, TournamentBracketState bracket)
    {
        if (!IsValidBracketProjection(bracket, capacity) || table == null
            || string.IsNullOrWhiteSpace(tournamentId) || string.IsNullOrWhiteSpace(localPlayerId)
            || string.IsNullOrWhiteSpace(table.matchId) || table.round < 0 || table.round >= bracket.rounds.Count
            || table.matchIndex < 0 || table.matchIndex >= bracket.rounds[table.round].Count
            || table.roomName != TournamentBracketState.MatchRoomName(tournamentId, table.round, table.matchIndex)
            || table.playerIds == null || table.playerIds.Count != 2
            || string.IsNullOrWhiteSpace(table.playerIds[0]) || string.IsNullOrWhiteSpace(table.playerIds[1])
            || table.playerIds[0] == table.playerIds[1] || !table.playerIds.Contains(localPlayerId)) return false;
        var expected = bracket.rounds[table.round][table.matchIndex];
        return expected.IsReady && !expected.IsFinished && table.playerIds.Contains(expected.first.playerId)
            && table.playerIds.Contains(expected.second.playerId);
    }

    public static bool TryValidateOpeningTables(TournamentMatch[] tables, string tournamentId,
        int capacity, string localPlayerId, out TournamentMatch assignedTable)
    {
        assignedTable = null;
        if ((capacity != 4 && capacity != 8) || string.IsNullOrWhiteSpace(tournamentId)
            || string.IsNullOrWhiteSpace(localPlayerId) || tables == null || tables.Length != capacity / 2)
            return false;

        var matches = new HashSet<string>(StringComparer.Ordinal);
        var players = new HashSet<string>(StringComparer.Ordinal);
        var positions = new HashSet<int>();
        TournamentMatch candidate = null;
        foreach (var table in tables)
        {
            if (table == null || string.IsNullOrWhiteSpace(table.matchId) || !matches.Add(table.matchId)
                || table.round != 0 || table.matchIndex < 0 || table.matchIndex >= capacity / 2
                || !positions.Add(table.matchIndex)
                || table.roomName != TournamentBracketState.MatchRoomName(tournamentId, 0, table.matchIndex)
                || table.playerIds == null || table.playerIds.Count != 2)
                return false;
            foreach (var id in table.playerIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !players.Add(id)) return false;
                if (id == localPlayerId) candidate = table;
            }
        }
        if (candidate == null) return false;
        assignedTable = candidate;
        return true;
    }

    public static bool HasFailure(IDictionary<object, object> properties, out string message)
    {
        message = null;
        if (properties == null || !properties.TryGetValue("tournamentFailure", out object value)) return false;
        message = value as string;
        return !string.IsNullOrWhiteSpace(message);
    }

    public static bool HasMatchingTableIdentity(TournamentMatch match, string tournamentId,
        string roomName, IDictionary<object, object> properties)
    {
        return match != null && !string.IsNullOrWhiteSpace(match.matchId)
            && !string.IsNullOrWhiteSpace(tournamentId) && !string.IsNullOrWhiteSpace(match.roomName)
            && roomName == match.roomName && properties != null && !HasFailure(properties, out _)
            && properties.TryGetValue("isTournamentMatch", out object flag) && flag is bool active && active
            && properties.TryGetValue("backendMatchId", out object id) && id as string == match.matchId
            && properties.TryGetValue("tournamentId", out object tournament) && tournament as string == tournamentId;
    }
}
