using NUnit.Framework;
using UnityEngine;

/// <summary>1v1 prize math used on win panels and backend settlement expectations.</summary>
public class TrucoSettlementEditModeTests
{
    [TestCase(5, 9)]
    [TestCase(10, 18)]
    [TestCase(25, 45)]
    [TestCase(50, 90)]
    [TestCase(100, 180)]
    [TestCase(250, 450)]
    [TestCase(500, 900)]
    [TestCase(1000, 1800)]
    [TestCase(5000, 9000)]
    public void ComputeOneVsOnePrize_MatchesPotMinusTenPercent(int entry, int expectedPrize)
    {
        Assert.AreEqual(expectedPrize, Player1v1MatchExtensions.ComputeOneVsOnePrize(entry));
    }

    [Test]
    public void ComputeOneVsOnePrize_NetGainAfterEntryIsPrizeMinusEntry()
    {
        int entry = 10;
        int prize = Player1v1MatchExtensions.ComputeOneVsOnePrize(entry);
        Assert.AreEqual(8, prize - entry, "Winner nets entry back + profit after join deducted entry once.");
    }

    [Test]
    public void ComputeOneVsOnePrize_ZeroOrNegativeEntryReturnsZero()
    {
        Assert.AreEqual(0, Player1v1MatchExtensions.ComputeOneVsOnePrize(0));
        Assert.AreEqual(0, Player1v1MatchExtensions.ComputeOneVsOnePrize(-5));
    }
}
