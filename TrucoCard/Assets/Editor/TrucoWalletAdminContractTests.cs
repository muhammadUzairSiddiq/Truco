using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoWalletAdminContractTests
{
    static TrucoWalletAdminService.Record Record(string type = "sell", string status = "requested", int revision = 3) =>
        new TrucoWalletAdminService.Record {
            request = new TrucoWalletService.Request { id = "request1", type = type, amount = 50, status = status, createdAt = "2026-10-31T10:00:00Z" },
            owner = new TrucoWalletAdminService.Owner { id = "owner1", username = "Jugador" }, revision = revision,
            allowedActions = new[] { "approve", "reject", "pay" }
        };
    static TrucoWalletAdminService.Command Command() => new TrucoWalletAdminService.Command {
        expectedRevision = 3, idempotencyKey = "stable-operation-key", reason = "Comprobante revisado", paymentReference = "BANK-123"
    };
    static string Receipt(string type, string action, string status, string key = "stable-operation-key", bool applied = true)
    {
        var record = Record(type, status, 4);
        return JsonConvert.SerializeObject(new { success = true, data = new TrucoWalletAdminService.Mutation {
            record = record, operation = new TrucoWalletAdminService.Operation {
                id = "operation1", requestId = "request1", action = action, idempotencyKey = key, revision = 4, applied = applied
            }
        } });
    }
    [TestCase("buy", "approve", "credited")]
    [TestCase("sell", "approve", "approved")]
    [TestCase("sell", "pay", "paid")]
    [TestCase("sell", "pay", "finalized")]
    [TestCase("sell", "reject", "rejected")]
    public void MatchingAppliedReceiptConfirmsOnlyTheServerReturnedState(string type, string action, string status)
    {
        var previous = Record(type); var command = Command(); var json = Receipt(type, action, status);
        var first = TrucoWalletAdminService.ParseMutation(json, previous, action, command);
        var retry = TrucoWalletAdminService.ParseMutation(json, previous, action, command);
        Assert.AreEqual(status, first.record.request.status);
        Assert.AreEqual(first.operation.id, retry.operation.id, "An idempotent retry must refer to the same server operation.");
    }
    [Test]
    public void AcceptanceAloneOrMismatchedOperationCannotClaimPayment()
    {
        var previous = Record(); var command = Command();
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseMutation("{\"success\":true,\"data\":{}}", previous, "pay", command));
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseMutation(Receipt("sell", "pay", "paid", "another-key"), previous, "pay", command));
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseMutation(Receipt("sell", "pay", "paid", applied: false), previous, "pay", command));
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseMutation(Receipt("buy", "approve", "approved"), Record("buy"), "approve", command));
    }
    [Test]
    public void MutationRequiresServerAllowedActionCurrentRevisionAndPayoutReference()
    {
        var record = Record(); var command = Command();
        Assert.IsNull(TrucoWalletAdminService.ValidateCommand(record, "pay", command));
        command.paymentReference = "";
        Assert.IsNotNull(TrucoWalletAdminService.ValidateCommand(record, "pay", command));
        command = Command(); command.expectedRevision = 2;
        Assert.IsNotNull(TrucoWalletAdminService.ValidateCommand(record, "approve", command));
        command = Command(); record.allowedActions = Array.Empty<string>();
        Assert.IsNotNull(TrucoWalletAdminService.ValidateCommand(record, "approve", command));
        Assert.IsNotNull(TrucoWalletAdminService.ValidateCommand(Record("buy"), "pay", command));
    }
    [Test]
    public void DetailAndListRejectMissingOrForeignRecordsInsteadOfCreatingSamples()
    {
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParsePage("{\"success\":true,\"data\":{}}"));
        var json = JsonConvert.SerializeObject(new { success = true, data = Record() });
        Assert.AreEqual("request1", TrucoWalletAdminService.ParseRecord(json, "request1").request.id);
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseRecord(json, "another-request"));
        string empty = JsonConvert.SerializeObject(new { success = true, data = new { items = new List<TrucoWalletAdminService.Record>(), nextCursor = (string)null } });
        Assert.IsEmpty(TrucoWalletAdminService.ParsePage(empty).items);
        Assert.IsFalse(TrucoWalletAdminService.MatchesOperation(null, "request1", "pay", null));
    }
    [TestCase("amount")]
    [TestCase("revision")]
    [TestCase("applied")]
    public void FinancialReceiptsRejectImplicitStringToNumberOrBooleanConversions(string field)
    {
        var json = JObject.Parse(Receipt("sell", "pay", "paid"));
        if (field == "amount") json["data"]["record"]["request"]["amount"] = "50";
        else if (field == "revision") json["data"]["record"]["revision"] = "4";
        else json["data"]["operation"]["applied"] = "true";
        Assert.Throws<InvalidOperationException>(() => TrucoWalletAdminService.ParseMutation(json.ToString(), Record(), "pay", Command()));
    }
}
