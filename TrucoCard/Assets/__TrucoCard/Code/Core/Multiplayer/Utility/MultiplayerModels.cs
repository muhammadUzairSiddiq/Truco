
namespace MH.Multiplayer
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class TournamentRequest
    {
        public string TournamentId;
        public int MaxPlayerCount;
        public int EntryFee;
        public string TournamentName;

        public TournamentRequest(string tournamentId, int maxPlayerCount, int entryFee, string tournamentName = "")
        {
            this.TournamentId = tournamentId;
            this.MaxPlayerCount = maxPlayerCount;
            this.EntryFee = entryFee;
            this.TournamentName = tournamentName;
        }
    }

    [Serializable]
    public class TournamentMatch
    {
        public string matchId;
        public string roomName;
        public List<string> playerIds;
        public List<string> playerNames;
    }

}