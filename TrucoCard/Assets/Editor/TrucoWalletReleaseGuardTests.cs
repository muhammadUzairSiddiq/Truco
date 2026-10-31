using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Actual submit callbacks, with all endpoint overrides removed so no request can leave the test.</summary>
public sealed class TrucoWalletReleaseGuardTests
{
    [Test]
    public void HubShowsThreeMostRecentServerRequestsWithTheirOwnStatusAndTime()
    {
        var snapshot = new TrucoWalletService.Snapshot { available = 123, held = 42 };
        for (int i = 1; i <= 4; i++) snapshot.requests.Add(new TrucoWalletService.Request {
            id = "request" + i, type = i % 2 == 0 ? "sell" : "buy", amount = i * 50,
            status = i == 4 ? "paid" : "requested", createdAt = $"2026-10-0{i}T12:00:00Z"
        });
        var root = BuildWallet(snapshot, out var wallet);
        try
        {
            typeof(TrucoWalletScreen).GetMethod("Hub", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, null);
            var rows = root.GetComponentsInChildren<Button>(true).Where(button => button.name.StartsWith("RecentRequest_")).ToArray();
            CollectionAssert.AreEqual(new[] { "RecentRequest_request4", "RecentRequest_request3", "RecentRequest_request2" }, rows.Select(row => row.name).ToArray());
            StringAssert.Contains("Venta: 200 · Pagada", rows[0].GetComponentInChildren<TextMeshProUGUI>().text);
            StringAssert.Contains("04/10", rows[0].GetComponentInChildren<TextMeshProUGUI>().text);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void SaleDetailsLabelServerHeldTotalWithoutInventingPerRequestHeldCoins()
    {
        var root = BuildWallet(new TrucoWalletService.Snapshot { available = 123, held = 42 }, out var wallet);
        try
        {
            var request = new TrucoWalletService.Request { id = "sale", type = "sell", amount = 50, status = "requested" };
            typeof(TrucoWalletScreen).GetMethod("Details", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, new object[] { request });
            Assert.IsTrue(root.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == "Retenido total de la billetera: 42 Trucoins"));
            Assert.IsFalse(root.GetComponentsInChildren<TextMeshProUGUI>().Any(text => text.text == "Retenido total de la billetera: 50 Trucoins"));
        }
        finally { Object.DestroyImmediate(root); }
    }

    static GameObject BuildWallet(TrucoWalletService.Snapshot snapshot, out TrucoWalletScreen wallet)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("WalletPreviewRegression"); wallet = root.AddComponent<TrucoWalletScreen>();
        typeof(TrucoWalletScreen).GetMethod("Build", flags).Invoke(wallet, null);
        typeof(TrucoWalletScreen).GetField("_snapshot", flags).SetValue(wallet, snapshot);
        return root;
    }

    [Test]
    public void CompleteServerHistoryBuildsEveryRowAndProvidesVerticalScrolling()
    {
        var snapshot = new TrucoWalletService.Snapshot();
        for (int i = 0; i < 15; i++) snapshot.requests.Add(new TrucoWalletService.Request {
            id = "history" + i, type = "buy", amount = 50, status = "requested", createdAt = "2026-10-31T10:00:00Z"
        });
        var root = BuildWallet(snapshot, out var wallet);
        try
        {
            typeof(TrucoWalletScreen).GetMethod("History", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wallet, null);
            var scroll = root.GetComponentInChildren<ScrollRect>(true);
            Assert.IsNotNull(scroll); Assert.IsTrue(scroll.vertical); Assert.IsFalse(scroll.horizontal);
            Assert.AreEqual(15, scroll.content.GetComponentsInChildren<Button>(true).Length);
            Assert.AreEqual(15 * 130, scroll.content.sizeDelta.y);
            Assert.Less(((RectTransform)scroll.content.GetChild(14)).anchoredPosition.y, ((RectTransform)scroll.content.GetChild(0)).anchoredPosition.y);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(true, false, true, true)]
    [TestCase(true, true, false, true)]
    [TestCase(false, true, true, false)]
    [TestCase(true, false, false, false)]
    public void WalletSubmitHonorsLiveMaintenanceBeforeCreatingARequest(bool active, bool blockGame, bool blockTransactions, bool blocked)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var stateField = typeof(TrucoMaintenanceMode).GetField("_cached", flags);
        var routesField = typeof(ApiConfig).GetField("_overrides", flags);
        object previousState = stateField.GetValue(null), previousRoutes = routesField.GetValue(null);
        var root = new GameObject("WalletMaintenanceRegression");
        try
        {
            // Do not persist PlayerPrefs, display a global maintenance banner or contact any backend.
            stateField.SetValue(null, new TrucoMaintenanceMode.State {
                active = active, blockGame = blockGame, blockTransactions = blockTransactions,
                message = "QA maintenance blocks transactions"
            });
            routesField.SetValue(null, new Dictionary<string, string>());
            var wallet = root.AddComponent<TrucoWalletScreen>();
            typeof(TrucoWalletScreen).GetMethod("Build", instance).Invoke(wallet, null);
            typeof(TrucoWalletScreen).GetField("_snapshot", instance).SetValue(wallet, new TrucoWalletService.Snapshot {
                available = 100, held = 0,
                configuration = new TrucoWalletService.Configuration { minimumBuy = 5, minimumSell = 50, maximumAmount = 500 }
            });
            typeof(TrucoWalletScreen).GetMethod("Form", instance).Invoke(wallet, new object[] { "buy" });
            root.GetComponentInChildren<TMP_InputField>(true).text = "50";
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.name == "CONTINUAR") { button.onClick.Invoke(); break; }
            var submission = (TrucoWalletService.Submission)typeof(TrucoWalletScreen).GetField("_submission", instance).GetValue(wallet);
            var notice = (TextMeshProUGUI)typeof(TrucoWalletScreen).GetField("_notice", instance).GetValue(wallet);
            Assert.AreEqual(blocked ? 0 : 50, submission.amount,
                "Blocked transactions must return before preparing any request, including when global game maintenance is enabled.");
            Assert.AreEqual(blocked, notice.text == "QA maintenance blocks transactions");
        }
        finally
        {
            stateField.SetValue(null, previousState); routesField.SetValue(null, previousRoutes);
            Object.DestroyImmediate(root);
        }
    }
}
