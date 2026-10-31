using NUnit.Framework;

public class TrucoServerConfirmationTests
{
    [TestCase("{\"success\":true,\"data\":{\"matchId\":\"m1\",\"status\":\"closed\",\"refundsApplied\":true}}", true)]
    [TestCase("{\"success\":true}", false)]
    [TestCase("{\"success\":true,\"data\":{\"matchId\":\"other\",\"status\":\"closed\",\"refundsApplied\":true}}", false)]
    [TestCase("{\"success\":true,\"data\":{\"matchId\":\"m1\",\"status\":\"closed\",\"refundsApplied\":false}}", false)]
    [TestCase("{\"success\":false,\"data\":{\"matchId\":\"m1\",\"status\":\"closed\",\"refundsApplied\":true}}", false)]
    public void AdminMatchClosureRequiresMatchingConfirmedCancellation(string json, bool expected) =>
        Assert.AreEqual(expected, TrucoServerAcknowledgement.MatchClosureConfirmed(json, "m1"));

    [TestCase("{\"ok\":true,\"match\":{\"_id\":\"m1\"}}", "m1")]
    [TestCase("{\"success\":true,\"data\":{\"match\":{\"_id\":\"m1\"}}}", "m1")]
    [TestCase("{\"ok\":true,\"data\":{\"_id\":\"m1\"}}", "m1")]
    [TestCase("{\"ok\":false,\"match\":{\"_id\":\"m1\"}}", null)]
    [TestCase("{\"ok\":true,\"data\":{\"success\":false,\"_id\":\"m1\"}}", null)]
    [TestCase("{\"ok\":true,\"match\":{}}", null)]
    [TestCase("{\"match\":{\"_id\":\"m1\"}}", null)]
    [TestCase("not json", null)]
    public void TournamentCreationRequiresConfirmedMatchId(string json, string expected) =>
        Assert.AreEqual(expected, TrucoServerAcknowledgement.TournamentMatch(json)?._id);

    [TestCase("{\"ok\":true}", false)]
    [TestCase("{\"ok\":false,\"refunded\":true}", false)]
    [TestCase("{\"status\":\"closed\"}", false)]
    [TestCase("{\"success\":true,\"data\":{\"refunded\":true}}", false)]
    [TestCase("{\"success\":true,\"data\":{\"status\":\"refunded\",\"matchId\":123}}", false)]
    [TestCase("{\"refunded\":true,\"matchId\":\"other\"}", false)]
    [TestCase("{\"ok\":true,\"data\":{\"refunded\":true,\"matchId\":\"m1\"}}", true)]
    [TestCase("{\"alreadyRefunded\":true,\"matchId\":\"m1\"}", true)]
    [TestCase("", false)]
    [TestCase("<html>proxy error</html>", false)]
    public void RefundRequiresSettlementReceipt(string json, bool expected) =>
        Assert.AreEqual(expected, TrucoServerAcknowledgement.RefundConfirmed(json, "m1"));

    [TestCase("{\"ok\":true}", false)]
    [TestCase("{\"ok\":false,\"paid\":true}", false)]
    [TestCase("{\"ok\":true,\"data\":{\"prizePaid\":true}}", true)]
    public void PrizeRequiresPaymentReceipt(string json, bool expected) =>
        Assert.AreEqual(expected, TrucoServerAcknowledgement.PrizePaid(json));

    [TestCase("{\"maintenance\":{\"active\":true}}")]
    [TestCase("{\"data\":{\"active\":true}}")]
    [TestCase("{\"data\":{\"maintenance\":{\"active\":true}}}")]
    [TestCase("{\"active\":true}")]
    public void WrappedMaintenanceCannotAccidentallyDisableBlocking(string json)
    {
        var state = TrucoMaintenanceMode.TryParse(json);
        Assert.IsNotNull(state);
        Assert.IsTrue(state.active);
        Assert.IsTrue(state.blockGame, "An omitted block policy must retain the safe default");
    }

    [TestCase("{}")]
    [TestCase("{\"ok\":false,\"active\":false}")]
    [TestCase("{\"success\":false,\"data\":{\"maintenance\":{\"active\":false}}}")]
    [TestCase("{\"error\":\"Unauthorized\"}")]
    [TestCase("not json")]
    public void InvalidMaintenanceResponseDoesNotReplaceKnownState(string json) =>
        Assert.IsNull(TrucoMaintenanceMode.TryParse(json));
}
