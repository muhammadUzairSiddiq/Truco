using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

public class TrucoChampionPayoutPresentationTests
{
    [TestCase(false)] [TestCase(true)]
    public void AdvertisedPrizeIsNotPresentedAsTheConfirmedPaidAmount(bool paymentConfirmed)
    {
        var root = new GameObject("ChampionReceiptPresentation");
        try
        {
            var overlay = root.AddComponent<TournamentChampionOverlay>();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(TournamentChampionOverlay).GetMethod("Build", flags).Invoke(overlay, null);
            typeof(TournamentChampionOverlay).GetMethod("Display", flags).Invoke(overlay, new object[] { 720, "Copa", paymentConfirmed });
            var label = (TextMeshProUGUI)typeof(TournamentChampionOverlay).GetField("_prize", flags).GetValue(overlay);
            StringAssert.Contains("Premio previsto", label.text);
            StringAssert.DoesNotContain("recibiste", label.text);
            Assert.AreEqual(paymentConfirmed, label.text.Contains("El servidor confirmó el pago"));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
