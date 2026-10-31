using System.Collections.Generic;

public sealed class TrucoPracticeTournament
{
    public readonly TournamentBracketState Bracket;
    public readonly int Target;
    public readonly bool WithFlor;
    public readonly int Seed;
    public int CompletedMatches { get; private set; }
    public int Actions { get; private set; }
    public const string LocalId = "editor-local";
    public TrucoPracticeTournament(int count, int target, bool flor, int seed)
    {
        Bracket = new TournamentBracketState(count); Target = TrucoMatchRules.NormalizeTargetScore(target); WithFlor = flor; Seed = seed;
        var players = new List<User>();
        string[] names = { "Vos", "Mateo · AI", "Lucía · AI", "Diego · AI", "Sofía · AI", "Nico · AI", "Valen · AI", "Tomás · AI" };
        for (int i = 0; i < count; i++) players.Add(new User { _id = i == 0 ? LocalId : "editor-ai-" + i, username = names[i] });
        Bracket.Seed(players);
    }
    public bool Record(int round, int index, string winnerId, string winnerName, int actions)
    {
        if (!Bracket.RecordWinner(round, index, winnerId, winnerName)) return false;
        CompletedMatches++; Actions += actions; return true;
    }
    public void CompleteRoundWithBots(int round)
    {
        foreach (var table in Bracket.rounds[round])
        {
            if (!table.IsReady || table.IsFinished) continue;
            var game = new TrucoPracticeMatch(Seed + round * 101 + table.index * 17, Target, WithFlor);
            game.CompleteWithBots();
            var winner = game.Winner == 0 ? table.first : table.second;
            Record(round, table.index, winner.playerId, winner.playerName, game.Actions);
        }
    }
    public void CompleteWithBots()
    { for (int round = 0; round < Bracket.rounds.Count; round++) CompleteRoundWithBots(round); }
}
