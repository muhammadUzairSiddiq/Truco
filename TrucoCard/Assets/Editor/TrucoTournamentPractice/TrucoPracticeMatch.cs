using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Local practice engine sharing deterministic rules with the live game. No Photon/API/economy calls.</summary>
public sealed class TrucoPracticeMatch
{
    public enum Canto { None, Truco, Envido }
    readonly Random _random;
    public readonly int Target;
    public readonly bool WithFlor;
    public readonly int[] Scores = new int[2];
    public readonly List<DeckCards>[] Hands = { new List<DeckCards>(), new List<DeckCards>() };
    public readonly bool[][] Used = { new bool[3], new bool[3] };
    public readonly DeckCards[] Table = new DeckCards[2];
    readonly List<int> _tricks = new List<int>();
    bool _clearTable;
    int _acceptedTruco, _lastRaiser = -1, _caller, _pendingLevel;
    bool _envidoResolved, _florResolved;
    public int HandNumber { get; private set; }
    public int Mano => (HandNumber - 1) % 2;
    public int Turn { get; private set; }
    public Canto Pending { get; private set; }
    public int Responder => Pending == Canto.None ? -1 : 1 - _caller;
    public bool HandOver { get; private set; }
    public int Winner { get; private set; } = -1;
    public string Message { get; private set; }
    public int Actions { get; private set; }
    public bool Finished => Winner >= 0;
    public int Stake => TrucoRulePoints.MazoAwardForAcceptedTrucoLevel(_acceptedTruco);
    public TrucoPracticeMatch(int seed, int target, bool flor)
    { _random = new Random(seed); Target = TrucoMatchRules.NormalizeTargetScore(target); WithFlor = flor; NewHand(); }
    public void NewHand()
    {
        if (Finished || (HandNumber > 0 && !HandOver)) return;
        var deck = new List<DeckCards>();
        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
            foreach (int rank in new[] { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 }) deck.Add(new DeckCards { suit = suit, rank = rank });
        for (int i = deck.Count - 1; i > 0; i--) { int j = _random.Next(i + 1); var card = deck[i]; deck[i] = deck[j]; deck[j] = card; }
        for (int actor = 0; actor < 2; actor++)
        { Hands[actor].Clear(); for (int i = 0; i < 3; i++) { Hands[actor].Add(deck[actor * 3 + i]); Used[actor][i] = false; } Table[actor] = null; }
        HandNumber++; Turn = Mano; _tricks.Clear(); Pending = Canto.None; _acceptedTruco = 0; _lastRaiser = -1;
        HandOver = false; _envidoResolved = false; _florResolved = false; _clearTable = false;
        Message = "Nueva mano · " + (Mano == 0 ? "salís vos" : "sale el rival");
        // The same Flor-value helper used by production decides practice Flor; Sin Flor never enters this branch.
        if (WithFlor && (HasFlor(0) || HasFlor(1)))
        {
            int winner = !HasFlor(0) ? 1 : !HasFlor(1) ? 0 : FlorValue(0) == FlorValue(1) ? Mano : FlorValue(0) > FlorValue(1) ? 0 : 1;
            _florResolved = _envidoResolved = true; Award(winner, 3);
            Message = "Flor · " + (winner == 0 ? "vos" : "rival") + " +3";
        }
    }
    public bool HasFlor(int actor) => WithFlor && Hands[actor].Select(c => c.suit).Distinct().Count() == 1;
    int FlorValue(int actor) => TrucoFlorScore.Compute(Hands[actor].Select(c => c.rank));
    public int EnvidoValue(int actor)
    {
        int best = Hands[actor].Max(c => TrucoCardRules.EnvidoValue(c.rank));
        for (int i = 0; i < 3; i++) for (int j = i + 1; j < 3; j++)
            if (Hands[actor][i].suit == Hands[actor][j].suit) best = Math.Max(best, TrucoCardRules.EnvidoPairTotal(Hands[actor][i].rank, Hands[actor][j].rank));
        return best;
    }
    public bool CanAct(int actor) => !Finished && !HandOver && Pending == Canto.None && Turn == actor;
    public bool CanTruco(int actor) => CanAct(actor) && _acceptedTruco < 3 && _lastRaiser != actor;
    public bool CanEnvido(int actor) => CanAct(actor) && !_envidoResolved && !Used.Any(cards => cards.Any(v => v));
    public bool CallTruco(int actor)
    {
        if (!CanTruco(actor)) return false;
        Pending = Canto.Truco; _caller = actor; _pendingLevel = _acceptedTruco + 1; Actions++;
        Message = _pendingLevel == 1 ? "¡Truco!" : _pendingLevel == 2 ? "¡Retruco!" : "¡Vale cuatro!"; return true;
    }
    public bool CallEnvido(int actor)
    {
        if (!CanEnvido(actor)) return false;
        Pending = Canto.Envido; _caller = actor; Actions++; Message = "¡Envido!"; return true;
    }
    public bool Reply(int actor, bool accept)
    {
        if (Finished || Pending == Canto.None || actor != Responder) return false;
        Actions++; var canto = Pending; Pending = Canto.None;
        if (canto == Canto.Truco)
        {
            if (accept) { _acceptedTruco = _pendingLevel; _lastRaiser = _caller; Message = "Quiero · la mano vale " + Stake; }
            else { Award(_caller, Stake); HandOver = true; Message = "No quiero · mano terminada"; }
        }
        else
        {
            _envidoResolved = true;
            int winner = accept ? TrucoMatchRules.ResolveEnvidoWinnerActor(EnvidoValue(0), EnvidoValue(1), 0, 1, Mano) : _caller;
            Award(winner, accept ? 2 : 1);
            Message = accept ? "Envido · " + EnvidoValue(0) + " a " + EnvidoValue(1) + " · " + (winner == 0 ? "vos" : "rival") : "Envido · no quiero";
        }
        return true;
    }
    public bool Fold(int actor)
    {
        if (!CanAct(actor)) return false;
        int extra = TrucoRulePoints.MazoUnplayedEnvidoPoint(_acceptedTruco, Used.Any(c => c.Any(v => v)), _lastRaiser >= 0, _envidoResolved, _florResolved);
        Award(1 - actor, Stake + extra); HandOver = true; Actions++; Message = "Al mazo · mano terminada"; return true;
    }
    public bool Play(int actor, int index)
    {
        if (!CanAct(actor) || index < 0 || index >= 3 || Used[actor][index]) return false;
        if (_clearTable) { Table[0] = Table[1] = null; _clearTable = false; }
        Table[actor] = Hands[actor][index]; Used[actor][index] = true; Actions++;
        if (Table[1 - actor] == null) { Turn = 1 - actor; Message = "Carta jugada · responde el rival"; return true; }
        int a = TrucoCardRules.TrucoRank(Table[0].rank, Table[0].suit), b = TrucoCardRules.TrucoRank(Table[1].rank, Table[1].suit);
        int trick = a == b ? 0 : a > b ? 1 : 2; _tricks.Add(trick);
        int? winner = TrucoHandWinner.Evaluate(_tricks, Mano + 1);
        if (winner.HasValue) { Award(winner.Value - 1, Stake); HandOver = true; Message = (winner.Value == 1 ? "Ganaste" : "Ganó el rival") + " la mano · +" + Stake; }
        else { if (trick != 0) Turn = trick - 1; else Turn = 1 - actor; _clearTable = true; Message = trick == 0 ? "Parda" : "Gana la baza " + (trick == 1 ? "tu carta" : "el rival"); }
        return true;
    }
    void Award(int actor, int points)
    { Scores[actor] += points; if (TrucoMatchRules.HasReachedTarget(Scores[actor], Target)) Winner = actor; }
    public bool StepAI()
    {
        if (Finished) return false;
        if (HandOver) { NewHand(); return true; }
        if (Pending != Canto.None)
        {
            int actor = Responder;
            bool accept = Pending == Canto.Envido ? EnvidoValue(actor) >= 25 : Hands[actor].Count(c => TrucoCardRules.TrucoRank(c.rank, c.suit) >= 9) >= 1;
            return Reply(actor, accept || _random.Next(5) == 0);
        }
        int current = Turn;
        if (CanEnvido(current) && EnvidoValue(current) >= 27 && _random.Next(2) == 0) return CallEnvido(current);
        if (CanTruco(current) && _random.Next(7) == 0) return CallTruco(current);
        var indices = Enumerable.Range(0, 3).Where(i => !Used[current][i]).OrderBy(i => TrucoCardRules.TrucoRank(Hands[current][i].rank, Hands[current][i].suit)).ToList();
        int chosen = indices[0]; var rival = Table[1 - current];
        if (!_clearTable && rival != null)
        {
            int rank = TrucoCardRules.TrucoRank(rival.rank, rival.suit);
            var winning = indices.Where(i => TrucoCardRules.TrucoRank(Hands[current][i].rank, Hands[current][i].suit) > rank).ToList();
            if (winning.Count > 0) chosen = winning[0];
        }
        return Play(current, chosen);
    }
    public void CompleteWithBots()
    {
        int guard = 0;
        while (!Finished && guard++ < 5000) if (!StepAI()) throw new InvalidOperationException("Practice AI stalled.");
        if (!Finished) throw new InvalidOperationException("Practice match exceeded action bound.");
    }
}
