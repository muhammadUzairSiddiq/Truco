using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TrucoBracketPresentationTests
{
    [TestCase(4)]
    [TestCase(8)]
    public void ConnectedTreeContainsEveryMatchWithoutChangingResults(int count)
    {
        var state = new TournamentBracketState(count);
        var players = new List<User>();
        for (int i = 0; i < count; i++) players.Add(new User { _id = "p" + i, username = "Jugador " + i });
        state.Seed(players);
        var host = new GameObject("Preview", typeof(RectTransform));
        try
        {
            TournamentBracketGraphic.Build(host.GetComponent<RectTransform>(), state, "p0");
            int cards = 0;
            foreach (Transform child in host.transform)
                if (child.name.StartsWith("Match_")) cards++;
            Assert.AreEqual(count - 1, cards);
            foreach (var round in state.rounds)
                foreach (var match in round) Assert.IsFalse(match.IsFinished);
            Assert.IsNotNull(host.transform.Find("FinalTrophy"));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(0, 0)]
    [TestCase(0, 1)]
    [TestCase(1, 0)]
    public void ParentIsCenteredBetweenItsChildren(int round, int parent)
    {
        float a = TournamentBracketGraphic.Center(round, parent * 2, 290).y;
        float b = TournamentBracketGraphic.Center(round, parent * 2 + 1, 290).y;
        Assert.AreEqual((a + b) / 2, TournamentBracketGraphic.Center(round + 1, parent, 290).y);
    }
}
