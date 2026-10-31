using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoPhotonAuthenticationTests
{
    static JObject Fixture(string mode = "tournament", string tournament = "cup", string match = "m1") => new JObject {
        ["success"] = true, ["data"] = new JObject { ["schemaVersion"] = 1, ["ticket"] = new JObject {
            ["provider"] = "photon", ["userId"] = "p0", ["mode"] = mode, ["tournamentId"] = tournament,
            ["matchId"] = match, ["authToken"] = "test-only-not-a-live-ticket", ["ticketId"] = "ticket-1",
            ["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(2).ToString("O"), ["appVersion"] = "test-v1",
            ["allowedRooms"] = new JArray("TMatch_cup_R0_M0") } } };
    static TrucoPhotonAuthentication.Ticket Parse(JObject root) => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", "tournament", "cup", "m1", "TMatch_cup_R0_M0");

    [Test] public void RoomTicketRequiresAccountAndExactServerAssignment()
    {
        var root = Fixture(); Assert.AreEqual("p0", Parse(root).userId);
        Assert.Throws<InvalidOperationException>(() => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "impostor", "tournament", "cup", "m1", "TMatch_cup_R0_M0"));
        Assert.Throws<InvalidOperationException>(() => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", "tournament", "cup", "m2", "TMatch_cup_R0_M0"));
        Assert.Throws<InvalidOperationException>(() => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", "tournament", "cup", "m1", "TMatch_cup_R0_M1"));
        Assert.Throws<InvalidOperationException>(() => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", "spectator", "cup", "m1", "TMatch_cup_R0_M0"));
    }
    [TestCase("one_vs_one")][TestCase("tournament")][TestCase("spectator")]
    public void IdentityPreflightDoesNotGrantAnyRoom(string mode)
    {
        var root = Fixture(mode, null, null); root["data"]["ticket"]["allowedRooms"] = new JArray();
        Assert.AreEqual(mode, TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", mode).mode);
        Assert.Throws<InvalidOperationException>(() => TrucoPhotonAuthentication.ReadTicket(root.ToString(), "p0", mode, roomName: "paid-room"));
    }
    [TestCase("userId")][TestCase("authToken")][TestCase("appVersion")][TestCase("expiresAt")][TestCase("ticketId")]
    public void MalformedOrMissingSecurityFieldsCannotCoerceIntoTickets(string field)
    {
        var root = Fixture(); root["data"]["ticket"][field] = 123;
        Assert.Throws<InvalidOperationException>(() => Parse(root));
        ((JObject)root["data"]["ticket"]).Remove(field);
        Assert.Throws<InvalidOperationException>(() => Parse(root));
    }
    [Test] public void ExpiredUnversionedAndDuplicateRoomTicketsFailClosed()
    {
        var root = Fixture(); root["data"]["ticket"]["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        Assert.Throws<InvalidOperationException>(() => Parse(root));
        root = Fixture(); root["data"]["schemaVersion"] = 2; Assert.Throws<InvalidOperationException>(() => Parse(root));
        root = Fixture(); root["data"]["ticket"]["allowedRooms"] = new JArray("TMatch_cup_R0_M0", "TMatch_cup_R0_M0");
        Assert.Throws<InvalidOperationException>(() => Parse(root));
    }
}
