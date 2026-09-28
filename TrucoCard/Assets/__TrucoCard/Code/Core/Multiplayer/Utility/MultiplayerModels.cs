
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
        public int Prize;
        public bool WithFlor;
        public int TargetScore;
        public bool IsPrivate;

        public TournamentRequest(string tournamentId, int maxPlayerCount, int entryFee, string tournamentName = "",
            int prize = 0, bool withFlor = true, int targetScore = TrucoMatchRules.DefaultTargetScore,
            bool isPrivate = false)
        {
            this.TournamentId = tournamentId;
            this.MaxPlayerCount = maxPlayerCount;
            this.EntryFee = entryFee;
            this.TournamentName = tournamentName;
            this.Prize = prize;
            this.WithFlor = withFlor;
            this.TargetScore = TrucoMatchRules.NormalizeTargetScore(targetScore);
            this.IsPrivate = isPrivate;
        }
    }

    [Serializable]
    public class TournamentMatch
    {
        public string matchId;
        public string roomName;
        public List<string> playerIds;
        public List<string> playerNames;
        public int round;
        public int matchIndex;
    }

}
