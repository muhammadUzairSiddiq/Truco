using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-side bracket projection.  It never awards coins or changes game results: it only maps an
/// already-decided winner into the correct next visible slot.  This keeps tournament presentation
/// separate from the established Truco rules and makes 4- and 8-player progression deterministic.
/// </summary>
[Serializable]
public sealed class TournamentBracketState
{
    [Serializable]
    public sealed class Slot
    {
        public string playerId;
        public string playerName;
        public bool eliminated;
    }

    [Serializable]
    public sealed class BracketMatch
    {
        public int round;
        public int index;
        public Slot first = new Slot();
        public Slot second = new Slot();
        public string winnerId;
        public string winnerName;
        public bool IsReady => !string.IsNullOrEmpty(first.playerId) && !string.IsNullOrEmpty(second.playerId);
        public bool IsFinished => !string.IsNullOrEmpty(winnerId);
    }

    public int maxPlayers;
    public readonly List<List<BracketMatch>> rounds = new List<List<BracketMatch>>();

    public TournamentBracketState(int playerCount)
    {
        maxPlayers = playerCount == 8 ? 8 : 4;
        int matchCount = maxPlayers / 2;
        while (matchCount > 0)
        {
            var round = new List<BracketMatch>();
            for (int i = 0; i < matchCount; i++)
                round.Add(new BracketMatch { round = rounds.Count, index = i });
            rounds.Add(round);
            matchCount /= 2;
        }
    }

    public void Seed(IList<User> players)
    {
        if (players == null) return;
        var opening = rounds[0];
        for (int i = 0; i < opening.Count * 2 && i < players.Count; i++)
        {
            var user = players[i];
            if (user == null) continue;
            var slot = i % 2 == 0 ? opening[i / 2].first : opening[i / 2].second;
            slot.playerId = !string.IsNullOrEmpty(user._id) ? user._id : user.id;
            slot.playerName = user.username;
        }
    }

    /// <summary>Records one validated match result and advances exactly one winner into its parent match.</summary>
    public bool RecordWinner(int round, int matchIndex, string winnerId, string winnerName)
    {
        if (round < 0 || round >= rounds.Count || matchIndex < 0 || matchIndex >= rounds[round].Count)
            return false;
        var current = rounds[round][matchIndex];
        if (!current.IsReady || current.IsFinished || string.IsNullOrEmpty(winnerId)) return false;
        if (current.first.playerId != winnerId && current.second.playerId != winnerId) return false;

        current.winnerId = winnerId;
        current.winnerName = winnerName;
        if (current.first.playerId != winnerId) current.first.eliminated = true;
        if (current.second.playerId != winnerId) current.second.eliminated = true;
        if (round + 1 >= rounds.Count) return true;

        var parent = rounds[round + 1][matchIndex / 2];
        var target = matchIndex % 2 == 0 ? parent.first : parent.second;
        target.playerId = winnerId;
        target.playerName = winnerName;
        target.eliminated = false;
        return true;
    }

    public string RoundTitle(int round)
    {
        if (maxPlayers == 8)
            return round == 0 ? "CUARTOS DE FINAL" : round == 1 ? "SEMIFINALES" : "FINAL";
        return round == 0 ? "SEMIFINALES" : "FINAL";
    }

    // Merge independent completed branches in round order. A stale snapshot must never erase a result.
    public void MergeResults(TournamentBracketState other)
    {
        if (other == null || other.maxPlayers != maxPlayers) return;
        for (int r = 0; r < rounds.Count; r++)
            for (int i = 0; i < rounds[r].Count; i++)
            {
                var incoming = other.rounds[r][i];
                if (incoming.IsFinished)
                    RecordWinner(r, i, incoming.winnerId, incoming.winnerName);
            }
    }

    public static string MatchRoomName(string tournamentId, int round, int matchIndex) =>
        "TMatch_" + tournamentId + "_R" + round + "_M" + matchIndex;

    public static string HubRoomName(string tournamentId) => "THub_" + tournamentId;

    public static bool IsHubRoom(string roomName) =>
        !string.IsNullOrEmpty(roomName) && roomName.StartsWith("THub_");

    public string ToJson()
    {
        var wire = new Wire { maxPlayers = maxPlayers, matches = Flatten() };
        return JsonUtility.ToJson(wire);
    }

    public static TournamentBracketState FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        Wire wire;
        try { wire = JsonUtility.FromJson<Wire>(json); }
        catch { return null; }
        if (wire == null) return null;
        var state = new TournamentBracketState(wire.maxPlayers);
        if (wire.matches == null) return state;
        for (int i = 0; i < wire.matches.Length; i++)
        {
            var src = wire.matches[i];
            if (src == null || src.round < 0 || src.round >= state.rounds.Count) continue;
            if (src.index < 0 || src.index >= state.rounds[src.round].Count) continue;
            state.rounds[src.round][src.index] = src;
        }
        return state;
    }

    BracketMatch[] Flatten()
    {
        var list = new List<BracketMatch>();
        for (int r = 0; r < rounds.Count; r++)
            list.AddRange(rounds[r]);
        return list.ToArray();
    }

    [Serializable]
    class Wire
    {
        public int maxPlayers;
        public BracketMatch[] matches;
    }
}
