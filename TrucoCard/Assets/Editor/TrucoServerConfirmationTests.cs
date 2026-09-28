using NUnit.Framework;

public class TrucoServerConfirmationTests
{
    [TestCase("{\"ok\":true}", false)]
    [TestCase("{\"ok\":false,\"refunded\":true}", false)]
    [TestCase("{\"status\":\"closed\"}", false)]
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
    [TestCase("{\"error\":\"Unauthorized\"}")]
    [TestCase("not json")]
    public void InvalidMaintenanceResponseDoesNotReplaceKnownState(string json) =>
        Assert.IsNull(TrucoMaintenanceMode.TryParse(json));
}
