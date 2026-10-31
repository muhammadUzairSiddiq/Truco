using NUnit.Framework;
using UnityEngine;

public class TrucoClubFeatureTests
{
    static TrucoWalletService.Configuration Config => new TrucoWalletService.Configuration { minimumBuy = 5, minimumSell = 50, maximumAmount = 5000, identificationRequired = true };
    [TestCase(0, false)] [TestCase(4, false)] [TestCase(5, true)] [TestCase(5000, true)] [TestCase(5001, false)]
    public void BuyHonorsServerLimits(int amount, bool valid)
    {
        var request = new TrucoWalletService.Submission { type = "buy", amount = amount };
        Assert.AreEqual(valid, TrucoWalletService.Validate(request, Config, 0) == null);
    }
    [Test]
    public void SaleRequiresAvailableBalanceAndCompletePayoutFields()
    {
        var request = new TrucoWalletService.Submission { type = "sell", amount = 50 };
        Assert.IsNotNull(TrucoWalletService.Validate(request, Config, 100));
        request.bank = "Bank"; request.holder = "Test"; request.alias = "test.account"; request.whatsapp = "+123456789"; request.identification = "QA";
        Assert.IsNull(TrucoWalletService.Validate(request, Config, 50));
        Assert.IsNotNull(TrucoWalletService.Validate(request, Config, 49));
        Assert.IsNotNull(TrucoWalletService.Validate(request, null, 100));
    }
    [Test]
    public void UnknownWalletStatusNeverClaimsSuccess()
    {
        Assert.AreEqual("Estado pendiente de confirmación", TrucoWalletService.StatusLabel("unknown"));
        Assert.AreEqual("Acreditada", TrucoWalletService.StatusLabel("credited"));
    }
    [Test]
    public void ProductionRoomCannotBeDrivenByDevelopmentBot()
    {
        var settings = TrucoDevelopmentSettings.Current;
        Assert.IsNotNull(settings, "Development SO must be present in Resources.");
        var previous = settings.mode;
        try
        {
            settings.mode = TrucoExecutionMode.DevelopmentAI;
            Assert.IsFalse(TrucoDevelopmentSettings.CanDriveRoom("TMatch_real_R0_M0"));
            Assert.IsFalse(TrucoDevelopmentSettings.CanDriveRoom("OneVsOne_real"));
            Assert.IsFalse(TrucoDevelopmentSettings.CanDriveRoom(null));
            Assert.IsTrue(TrucoDevelopmentSettings.CanDriveRoom("TRUCO_AI_QA_test"));
            settings.mode = TrucoExecutionMode.Multiplayer;
            Assert.IsFalse(TrucoDevelopmentSettings.CanDriveRoom("TRUCO_AI_QA_test"));
        }
        finally { settings.mode = previous; }
    }
}
