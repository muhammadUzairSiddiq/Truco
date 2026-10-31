using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class TrucoProfileContractTests
{
    static JObject Valid() => JObject.Parse("{\"success\":true,\"user\":{\"_id\":\"user-one\",\"wallet\":{\"balance\":47}}}");

    [TestCase(0)]
    [TestCase(47)]
    [TestCase(int.MaxValue)]
    public void ExplicitIntegerBalanceIsPreserved(int balance)
    {
        var json = Valid(); json["user"]["wallet"]["balance"] = balance;
        Assert.IsTrue(TrucoProfileContract.TryParse(json.ToString(), out var user));
        Assert.AreEqual("user-one", user._id);
        Assert.AreEqual(balance, user.wallet.balance);
    }

    [TestCase("missing-wallet")]
    [TestCase("null-wallet")]
    [TestCase("missing-balance")]
    [TestCase("null-balance")]
    [TestCase("fraction")]
    [TestCase("negative")]
    [TestCase("overflow")]
    [TestCase("string")]
    [TestCase("missing-id")]
    [TestCase("numeric-id")]
    [TestCase("blank-id")]
    [TestCase("failed-response")]
    public void InvalidProfileCannotSupplyAnInventedBalance(string fault)
    {
        var json = Valid(); var profile = (JObject)json["user"]; var wallet = (JObject)profile["wallet"];
        if (fault == "missing-wallet") profile.Remove("wallet");
        else if (fault == "null-wallet") profile["wallet"] = null;
        else if (fault == "missing-balance") wallet.Remove("balance");
        else if (fault == "null-balance") wallet["balance"] = null;
        else if (fault == "fraction") wallet["balance"] = 1.5m;
        else if (fault == "negative") wallet["balance"] = -1;
        else if (fault == "overflow") wallet["balance"] = (long)int.MaxValue + 1;
        else if (fault == "string") wallet["balance"] = "47";
        else if (fault == "missing-id") profile.Remove("_id");
        else if (fault == "numeric-id") profile["_id"] = 7;
        else if (fault == "blank-id") profile["_id"] = " ";
        else json["success"] = false;
        Assert.IsFalse(TrucoProfileContract.TryParse(json.ToString(), out var user));
        Assert.IsNull(user);
    }

    [Test]
    public void ExistingIdAliasFallbackIsPreserved()
    {
        var json = Valid(); var profile = (JObject)json["user"];
        profile.Remove("_id"); profile["id"] = "alias-user";
        Assert.IsTrue(TrucoProfileContract.TryParse(json.ToString(), out var user));
        Assert.AreEqual("alias-user", user._id);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"success\":true,\"user\":null}")]
    public void EmptyProfileCannotBeSuccessful(string json)
    {
        Assert.IsFalse(TrucoProfileContract.TryParse(json, out var user));
        Assert.IsNull(user);
    }
}
