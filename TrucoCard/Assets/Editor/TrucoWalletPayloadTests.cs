using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoWalletPayloadTests
{
    static JObject Snapshot()
    {
        var value = new TrucoWalletService.Snapshot {
            available = 123, held = 50,
            configuration = new TrucoWalletService.Configuration { minimumBuy = 5, minimumSell = 50, maximumAmount = 500,
                buyPresets = new[] { 50, 100 }, sellPresets = new[] { 50, 100 }, identificationRequired = true }
        };
        return JObject.Parse(JsonConvert.SerializeObject(new { success = true, data = value }));
    }
    [Test]
    public void ValidServerSnapshotPreservesSeparateAvailableAndHeldBalances()
    {
        var result = TrucoWalletService.ParseSnapshot(Snapshot().ToString());
        Assert.AreEqual(123, result.available); Assert.AreEqual(50, result.held);
        Assert.AreEqual(5, result.configuration.minimumBuy);
    }
    [TestCase("negative-held")]
    [TestCase("decimal-balance")]
    [TestCase("string-balance")]
    [TestCase("invalid-limits")]
    [TestCase("invalid-preset")]
    [TestCase("missing-config")]
    public void MalformedFinancialValuesAndConfigurationCannotAppearConfirmed(string fault)
    {
        var json = Snapshot(); var data = json["data"];
        if (fault == "negative-held") data["held"] = -1;
        else if (fault == "decimal-balance") data["available"] = 123.7m;
        else if (fault == "string-balance") data["available"] = "123";
        else if (fault == "invalid-limits") data["configuration"]["maximumAmount"] = 4;
        else if (fault == "invalid-preset") data["configuration"]["sellPresets"] = new JArray(1);
        else data["configuration"] = null;
        Assert.Throws<InvalidOperationException>(() => TrucoWalletService.ParseSnapshot(json.ToString()));
    }
    [Test]
    public void BareSuccessOrDecimalRequestAmountCannotConfirmARequest()
    {
        Assert.Throws<InvalidOperationException>(() => TrucoWalletService.ParseRequest("{\"success\":true,\"data\":{}}"));
        var json = JObject.Parse(JsonConvert.SerializeObject(new { success = true, data = new TrucoWalletService.Request {
            id = "r1", type = "buy", amount = 50, status = "requested", createdAt = "2026-10-31T10:00:00Z"
        } }));
        Assert.AreEqual(50, TrucoWalletService.ParseRequest(json.ToString(), "r1").amount);
        json["data"]["amount"] = 50.1m;
        Assert.Throws<InvalidOperationException>(() => TrucoWalletService.ParseRequest(json.ToString(), "r1"));
    }
}
