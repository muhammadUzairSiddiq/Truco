using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using NUnit.Framework;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine.TestTools;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Opt-in live transport tests. Uses independent Photon clients in a unique app-version namespace;
/// no game API, accounts, wallet or gameplay results. NOT a replacement for APK acceptance tests.
/// </summary>
public class TrucoPhotonCloneSmokeTests
{
    [UnityTest] public IEnumerator TwoIndependentClients() => Run(2);
    [UnityTest] public IEnumerator FourIndependentClients() => Run(4);
    [UnityTest] public IEnumerator EightIndependentClients() => Run(8);

    static IEnumerator Run(int count)
    {
        if (Environment.GetEnvironmentVariable("TRUCO_RUN_PHOTON_QA") != "1")
            Assert.Ignore("Opt-in live Photon transport test; set TRUCO_RUN_PHOTON_QA=1 in the isolated QA process.");
        var source = PhotonNetwork.PhotonServerSettings.AppSettings;
        Assert.IsFalse(string.IsNullOrEmpty(source.AppIdRealtime), "QA requires a configured Photon application.");
        string run = "qa-" + Guid.NewGuid().ToString("N");
        var clients = new List<LoadBalancingClient>();
        try
        {
            for (int i = 0; i < count; i++)
            {
                var settings = source.CopyTo(new AppSettings());
                settings.AppVersion = run;
                var client = new LoadBalancingClient(settings.Protocol);
                client.AuthValues = new AuthenticationValues(run + "-" + i);
                clients.Add(client);
                Assert.IsTrue(client.ConnectUsingSettings(settings), "Connection request was rejected");
            }
            yield return Until(clients, () => clients.All(c => c.State == ClientState.ConnectedToMasterServer), "connect all clients");
            for (int i = 0; i < count; i += 2)
                Assert.IsTrue(clients[i].OpCreateRoom(new EnterRoomParams {
                    RoomName = run + "-pair-" + i / 2,
                    RoomOptions = new RoomOptions { MaxPlayers = 2, IsVisible = false, PlayerTtl = 15000,
                        EmptyRoomTtl = 0, CustomRoomProperties = new Hashtable { ["withFlor"] = false, ["targetScore"] = 30 } }
                }));
            yield return Until(clients, () => clients.Where((c, i) => i % 2 == 0).All(c => c.InRoom), "create all opening rooms");
            for (int i = 1; i < count; i += 2)
                Assert.IsTrue(clients[i].OpJoinRoom(new EnterRoomParams { RoomName = run + "-pair-" + i / 2 }));
            yield return Until(clients, () => clients.All(c => c.InRoom && c.CurrentRoom.PlayerCount == 2), "pair every opening room");
            foreach (var client in clients)
            {
                Assert.AreEqual(false, client.CurrentRoom.CustomProperties["withFlor"]);
                Assert.AreEqual(30, client.CurrentRoom.CustomProperties["targetScore"]);
            }
            clients[0].CurrentRoom.SetCustomProperties(new Hashtable { ["qa-sync"] = run });
            yield return Until(clients, () => (string)clients[1].CurrentRoom.CustomProperties["qa-sync"] == run, "synchronize room state");
            clients[1].Disconnect();
            yield return Until(clients, () => clients[1].State == ClientState.Disconnected, "disconnect second seat");
            Assert.IsTrue(clients[1].ReconnectAndRejoin(), "ReconnectAndRejoin request rejected");
            yield return Until(clients, () => clients[1].InRoom, "rejoin same room");
            Assert.AreEqual(run + "-pair-0", clients[1].CurrentRoom.Name);
            Assert.AreEqual(run, clients[1].CurrentRoom.CustomProperties["qa-sync"]);
        }
        finally
        {
            foreach (var client in clients) client.Disconnect();
        }
    }

    static IEnumerator Until(List<LoadBalancingClient> clients, Func<bool> condition, string step)
    {
        double deadline = EditorApplication.timeSinceStartup + 45;
        while (!condition())
        {
            foreach (var client in clients) client.Service();
            if (EditorApplication.timeSinceStartup >= deadline)
                Assert.Fail("Timed out: " + step + ". States: " + string.Join(", ", clients.Select(c => c.State.ToString())));
            yield return null;
        }
    }
}
