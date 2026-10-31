using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoAdminOperationContractTests
{
    static TrucoAdminOperationService.Command Command(string action) => new TrucoAdminOperationService.Command {
        action = action, tournamentId = "t-one", championId = "champion-one", userId = "user-one", amount = 27,
        reason = "reviewed by admin", idempotencyKey = "admin-key-one"
    };
    static JObject Response(TrucoAdminOperationService.Command command)
    {
        return JObject.Parse(JsonConvert.SerializeObject(new { success = true, data = new { operation = new {
            id = "op-one", action = command.action, idempotencyKey = command.idempotencyKey, applied = true,
            actorId = "admin-one", at = "2026-10-31T12:00:00Z", reason = command.reason,
            tournamentId = command.tournamentId, championId = command.championId,
            userId = command.userId, amount = command.amount, balanceAfter = 127, ledgerTransactionId = "ledger-one",
            status = "closed", refundsApplied = true, refundedTotal = 45
        } } }));
    }

    [TestCase("set_champion")]
    [TestCase("force_close_tournament")]
    [TestCase("credit_coins")]
    public void MatchingAppliedReceiptConfirmsExactlyRequestedOperation(string action)
    {
        var expected = Command(action);
        var receipt = TrucoAdminOperationService.ParseReceipt(Response(expected).ToString(), expected);
        Assert.AreEqual("op-one", receipt.id); Assert.IsTrue(receipt.applied);
    }

    [TestCase("bare-success")]
    [TestCase("unapplied")]
    [TestCase("string-applied")]
    [TestCase("wrong-key")]
    [TestCase("wrong-action")]
    [TestCase("wrong-reason")]
    [TestCase("wrong-tournament")]
    [TestCase("wrong-champion")]
    [TestCase("missing-actor")]
    [TestCase("invalid-time")]
    [TestCase("nested-rejection")]
    public void UnconfirmedOrDifferentChampionOperationIsRejected(string fault)
    {
        var expected = Command("set_champion"); var json = Response(expected); var operation = (JObject)json["data"]["operation"];
        if (fault == "bare-success") ((JObject)json["data"]).Remove("operation");
        else if (fault == "unapplied") operation["applied"] = false;
        else if (fault == "string-applied") operation["applied"] = "true";
        else if (fault == "wrong-key") operation["idempotencyKey"] = "other";
        else if (fault == "wrong-action") operation["action"] = "credit_coins";
        else if (fault == "wrong-reason") operation["reason"] = "other";
        else if (fault == "wrong-tournament") operation["tournamentId"] = "other";
        else if (fault == "wrong-champion") operation["championId"] = "other";
        else if (fault == "missing-actor") operation.Remove("actorId");
        else if (fault == "invalid-time") operation["at"] = "unknown";
        else json["data"]["success"] = false;
        Assert.Throws<InvalidOperationException>(() => TrucoAdminOperationService.ParseReceipt(json.ToString(), expected));
    }

    [TestCase("different-user")]
    [TestCase("different-amount")]
    [TestCase("fractional-amount")]
    [TestCase("negative-balance")]
    [TestCase("overflow-balance")]
    [TestCase("missing-ledger")]
    public void CreditRequiresExactIntegerAmountAndCommittedLedgerReference(string fault)
    {
        var expected = Command("credit_coins"); var json = Response(expected); var operation = (JObject)json["data"]["operation"];
        if (fault == "different-user") operation["userId"] = "other";
        else if (fault == "different-amount") operation["amount"] = 28;
        else if (fault == "fractional-amount") operation["amount"] = 27.1m;
        else if (fault == "negative-balance") operation["balanceAfter"] = -1;
        else if (fault == "overflow-balance") operation["balanceAfter"] = (long)int.MaxValue + 1;
        else operation.Remove("ledgerTransactionId");
        Assert.Throws<InvalidOperationException>(() => TrucoAdminOperationService.ParseReceipt(json.ToString(), expected));
    }

    [TestCase("unconfirmed-refunds")]
    [TestCase("not-closed")]
    [TestCase("missing-total")]
    public void ForceCloseRequiresConfirmedRefundSettlement(string fault)
    {
        var expected = Command("force_close_tournament"); var json = Response(expected); var operation = (JObject)json["data"]["operation"];
        if (fault == "unconfirmed-refunds") operation["refundsApplied"] = false;
        else if (fault == "not-closed") operation["status"] = "closing";
        else operation.Remove("refundedTotal");
        Assert.Throws<InvalidOperationException>(() => TrucoAdminOperationService.ParseReceipt(json.ToString(), expected));
    }
}
