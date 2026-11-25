using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MH.Multiplayer;
using Newtonsoft.Json.Linq;
using Photon.Realtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using LaunchAction = MH.Multiplayer.MultiplayerController.AssignedTableLaunchAction;
using Snapshot = TrucoTournamentService.Snapshot;

/// <summary>Runs the client gates against synthetic API and Photon inputs, then renders the real tournament UI.</summary>
[InitializeOnLoad]
public static class TrucoFourPlayerEvidence
{
    const string TournamentId = "offline-four-player-qa";
    const string Output = "Artifacts/UI/PlayerGallery";
    const string CaptureRequestPath = "Temp/TrucoFourPlayerEvidence.request";
    const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Names = { "Lucía", "Mateo", "Sofía", "Diego" };

    static TrucoFourPlayerEvidence() => EditorApplication.update += CheckCaptureRequest;

    static void CheckCaptureRequest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating
            || !File.Exists(CaptureRequestPath)) return;
        File.Delete(CaptureRequestPath);
        Run();
    }

    [MenuItem("Truco/Capture Four-Player QA Evidence")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Capture four-player QA evidence outside Play Mode.");
        Directory.CreateDirectory(Output);
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool preserveOpenScenes = !Application.isBatchMode;
        var evidenceScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            preserveOpenScenes ? NewSceneMode.Additive : NewSceneMode.Single);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(evidenceScene);
        try
        {
            var waiting = VerifyRegistration();
            var visibleTournaments = VerifyListVisibility();
            var assignments = VerifySemifinals(waiting);
            VerifyRoomAndHandGates(assignments);
            var final = VerifyFinal(assignments);
            Capture(waiting, assignments, final);
            CaptureTournamentList(visibleTournaments);
            File.WriteAllText(Path.Combine(Output, "four-player-evidence.txt"),
                "Offline Unity 4-player scenario with synthetic snapshots and Photon actor properties.\n" +
                "PASS: 0–3 confirmed registrations cannot start or assign a match.\n" +
                "PASS: Transitioning events remain visible to the fourth player and existing entrants remain recoverable.\n" +
                "PASS: One published semifinal never unlocks a four-player bracket.\n" +
                "PASS: Four confirmed registrations can start, then all four perspectives resolve to exactly two semifinals.\n" +
                "PASS: The fourth perspective can recover its table from the confirmed match row even without localAssignment.\n" +
                "PASS: Each simulated table rejects missing, inactive, foreign and stale-readiness peers.\n" +
                "PASS: Neither simulated seat loads until the master publishes its current match launch.\n" +
                "PASS: Both simulated seats recover a distinct three-card hand from the same valid packet.\n" +
                "PASS: Both simulated seats require current scene and hand readiness tokens.\n" +
                "PASS: Only the semifinal winners receive the final.\n" +
                "Production V1 parser, client validation, Photon property gates and UI were exercised.\n" +
                "No API request, Photon cloud connection, live card deal or live player was used.\n");
            Debug.Log("[Four-player evidence] Verified offline scenario and captured four images in " + Output);
        }
        finally
        {
            SetPresentation(null);
            if (preserveOpenScenes)
            {
                if (previousScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(evidenceScene, true);
            }
        }
    }

    static Snapshot VerifyRegistration()
    {
        Snapshot last = null;
        for (int confirmed = 0; confirmed < 4; confirmed++)
        {
            last = Read(Registration(confirmed, confirmed + 1, confirmed == 0 ? "not_registered" : "registered"), 0, last);
            Require(!MultiplayerController.CanStartTournament(last) && TrucoTournamentService.AssignedTable(last) == null,
                $"Only {confirmed} confirmed players. The lobby must wait without an assignment.");
        }
        var fourthBeforeJoin = Read(Registration(3, 5, "not_registered"), 3);
        Require(TrucoTournamentService.AssignedTable(fourthBeforeJoin) == null,
            "The fourth player's view cannot have a table before registration.");
        var withUnpaidSeat = Registration(4, 6, "registered");
        withUnpaidSeat.participants.Last().entryPaid = false;
        Require(!MultiplayerController.CanStartTournament(Read(withUnpaidSeat, 0)), "An unpaid fourth seat cannot start.");
        return last;
    }

    static Snapshot[] VerifySemifinals(Snapshot waiting)
    {
        var partialStart = Registration(4, 11, "playing");
        partialStart.status = "playing";
        SetReady(partialStart.matches[0], 0, 1);
        partialStart.localAssignment = Assign(partialStart.matches[0]);
        var pending = Read(partialStart, 0);
        Require(!TrucoTournamentService.HasCompleteOpeningRound(pending) && TrucoTournamentService.AssignedTable(pending) == null,
            "One published semifinal cannot unlock a four-player bracket.");

        var perspectives = new Snapshot[4];
        for (int player = 0; player < perspectives.Length; player++)
        {
            var registered = Read(Registration(4, 10, "registered"), player, player < 3 ? waiting : null);
            Require(MultiplayerController.CanStartTournament(registered), "Four confirmed seats can request the start.");
            var playing = Registration(4, 12, "playing");
            playing.status = "playing";
            SetReady(playing.matches[0], 0, 1);
            SetReady(playing.matches[1], 2, 3);
            if (player != 3) playing.localAssignment = Assign(playing.matches[player / 2]);
            var accepted = Read(playing, player, player == 0 ? pending : registered);
            var table = TrucoTournamentService.AssignedTable(accepted);
            Require(table != null && table.matchIndex == player / 2 && table.playerIds.Contains(Id(player)),
                $"Seat {player} must resolve to its own semifinal.");
            Require(TournamentStartupValidation.IsValidTableAssignment(table, TournamentId, 4, Id(player),
                    TrucoTournamentService.ToBracket(accepted)), "The assigned table must match the authoritative bracket.");
            perspectives[player] = accepted;
        }
        var rooms = perspectives.Select(s => TrucoTournamentService.AssignedTable(s)).GroupBy(t => t.roomName).ToArray();
        Require(rooms.Length == 2 && rooms.All(r => r.Count() == 2), "Exactly two independent rooms must seat two players each.");
        return perspectives;
    }

    static Tournament[] VerifyListVisibility()
    {
        var rows = new JArray(new JObject {
            ["_id"] = "fixture-open", ["name"] = "Copa abierta", ["status"] = "registration",
            ["maxPlayers"] = 4, ["withFlor"] = true, ["targetScore"] = 15,
            ["players"] = new JArray(Enumerable.Range(0, 3).Select(i => Id(i + 1)))
        }, new JObject {
            ["_id"] = TournamentId, ["name"] = "Torneo de prueba local", ["status"] = "playing",
            ["maxPlayers"] = 4, ["withFlor"] = false, ["targetScore"] = 30,
            ["players"] = new JArray(Enumerable.Range(0, 4).Select(Id))
        });
        var list = ApiController.TryParseTournamentListJson(rows.ToString());
        Require(list != null && list.Count == 2, "The legacy players list must parse correctly.");
        Require(SessionTournament.IsBrowsable(list[0], Id(0)) && !list[0].HasPlayer(Id(0)) && list[0].participants.Count == 3,
            "The fourth player must see the open tournament without already being enrolled.");
        Require(SessionTournament.IsBrowsable(list[1], Id(0)) && list[1].HasPlayer(Id(0)) && list[1].participants.Count == 4,
            "A registered player must find and resume the in-progress tournament.");
        Require(SessionTournament.IsBrowsable(list[1], "unregistered-player"),
            "An in-progress tournament should remain visible during a transition.");
        return list.OrderByDescending(t => t.HasPlayer(Id(0))).ToArray();
    }

    static void VerifyRoomAndHandGates(Snapshot[] assignments)
    {
        for (int seat = 0; seat < assignments.Length; seat++)
        {
            var table = TrucoTournamentService.AssignedTable(assignments[seat]);
            string localId = Id(seat);
            string peerId = table.playerIds.Single(id => id != localId);
            var local = new FixtureActor(localId, 1 + seat % 2, true, table.matchId);
            var remote = new FixtureActor(peerId, 2 - seat % 2, false, table.matchId);
            bool isMaster = seat % 2 == 0;
            var actors = new[] { local, remote };
            Require(!MultiplayerController.HasReadyAssignedPair(new[] { local }, table, localId), "One actor cannot launch a table.");
            foreach (var rejected in new[]
            {
                new FixtureActor(peerId, 2 - seat % 2, false, table.matchId, inactive: true),
                new FixtureActor(Id((seat + 2) % 4), 2 - seat % 2, false, table.matchId),
                new FixtureActor(peerId, 2 - seat % 2, false, "different-match")
            })
                Require(!MultiplayerController.HasReadyAssignedPair(new[] { local, rejected }, table, localId),
                    "An inactive, foreign or stale-readiness peer must not launch a table.");
            bool exactReadyPair = MultiplayerController.HasReadyAssignedPair(actors, table, localId);
            Require(exactReadyPair, "Both active assigned actors must agree on readiness for this match.");
            Require(MultiplayerController.GetAssignedTableLaunchAction(isMaster, false, table.matchId, table.matchId) == LaunchAction.Wait,
                "A cached room launch cannot replace an active peer.");
            Require(MultiplayerController.GetAssignedTableLaunchAction(isMaster, true, null, table.matchId)
                    == (isMaster ? LaunchAction.Publish : LaunchAction.Wait),
                "The master publishes the launch, and the other peer waits for it.");
            Require(MultiplayerController.GetAssignedTableLaunchAction(isMaster, true, table.matchId, table.matchId) == LaunchAction.Load,
                "Both peers enter gameplay after the launch is observed.");
            VerifyDealOwnership(actors, local);

            string hand = table.matchId + ":1";
            foreach (var actor in actors)
                Require(!TrucoTournamentHandReady.HasLoadedCurrentHand(actor.IsInactive, actor.CustomProperties, hand)
                    && !TrucoTournamentHandReady.HasCurrentPreparation(actor.IsInactive, actor.CustomProperties, hand),
                    "Room readiness must not imply that a hand has been loaded or dealt.");
            foreach (var actor in actors) actor.CustomProperties[TrucoTournamentHandReady.SceneProperty] = hand;
            local.CustomProperties[TrucoTournamentHandReady.Property] = hand;
            remote.CustomProperties[TrucoTournamentHandReady.Property] = table.matchId + ":0";
            Require(actors.All(a => TrucoTournamentHandReady.HasLoadedCurrentHand(a.IsInactive, a.CustomProperties, hand)),
                "Both seats must acknowledge the gameplay scene for this hand.");
            Require(!actors.All(a => TrucoTournamentHandReady.HasCurrentPreparation(a.IsInactive, a.CustomProperties, hand)),
                "A local deal and a stale remote hand must hold the clock.");
            remote.CustomProperties[TrucoTournamentHandReady.Property] = hand;
            Require(actors.All(a => TrucoTournamentHandReady.HasCurrentPreparation(a.IsInactive, a.CustomProperties, hand)),
                "Both seats must acknowledge the current hand before the clock can start.");
        }
    }

    static void VerifyDealOwnership(FixtureActor[] actors, FixtureActor local)
    {
        var first = new DeckCardsWrapper { actorNumber = 1, handRound = 1, cards = Cards(CardSuit.Swords) };
        var second = new DeckCardsWrapper { actorNumber = 2, handRound = 1, cards = Cards(CardSuit.Clubs) };
        // In production these are the two JSON payloads carried to the remote seat.
        first = JsonUtility.FromJson<DeckCardsWrapper>(JsonUtility.ToJson(first));
        second = JsonUtility.FromJson<DeckCardsWrapper>(JsonUtility.ToJson(second));
        Require(TrucoHandSeats.TryRestore(first, second, 1, 1, actors, default, out var ownership),
            "The same deal packet must resolve from either semifinal perspective.");
        var localCards = ownership.SeatForActor(local.ActorNumber) == 1 ? first : second;
        Require(localCards.actorNumber == local.ActorNumber && localCards.cards.Count == 3,
            "Each assigned actor must resolve its own three cards.");
        Require(!TrucoHandSeats.TryRestore(first, second, 2, 1, actors, ownership, out _),
            "A stale packet cannot deal another hand.");
    }

    static List<DeckCards> Cards(CardSuit suit) => Enumerable.Range(1, 3)
        .Select(rank => new DeckCards { rank = rank, suit = suit }).ToList();

    static Snapshot VerifyFinal(Snapshot[] previous)
    {
        Snapshot finalist = null;
        for (int seat = 0; seat < previous.Length; seat++)
        {
            var next = Read(previous[seat], seat);
            next.revision++;
            next.matches[0].status = next.matches[1].status = "finished";
            next.matches[0].winnerId = Id(0);
            next.matches[1].winnerId = Id(3);
            next.participants[1].status = next.participants[2].status = "eliminated";
            SetReady(next.matches[2], 0, 3);
            bool qualified = seat == 0 || seat == 3;
            next.localStatus = qualified ? "playing" : "eliminated";
            next.localAssignment = qualified ? Assign(next.matches[2]) : null;
            var accepted = Read(next, seat, previous[seat]);
            var assigned = TrucoTournamentService.AssignedTable(accepted);
            Require(qualified ? assigned != null && assigned.round == 1 : assigned == null,
                "Only the two semifinal winners must receive the final assignment.");
            if (seat == 0) finalist = accepted;
        }
        return finalist;
    }

    static void Capture(Snapshot waiting, Snapshot[] assignments, Snapshot final)
    {
        SetPresentation(waiting);
        var status = TrucoTournamentStatusScreen.EditorPreview(TrucoTournamentPhase.Filling);
        CaptureAndDestroy(status, "four-player-01-three-registered", "PRUEBA LOCAL · 3/4 · SIN MESA", .18f);

        foreach (var (snapshot, file, banner) in new[]
        {
            (assignments[0], "four-player-02-two-semifinals", "PRUEBA LOCAL · 4/4 · DOS SEMIFINALES"),
            (final, "four-player-03-finalists", "PRUEBA LOCAL · FINALISTAS CONFIRMADOS")
        })
        {
            SetPresentation(snapshot);
            TournamentBracketOverlay.Show(false);
            var bracket = UnityEngine.Object.FindFirstObjectByType<TournamentBracketOverlay>();
            CaptureAndDestroy(bracket.gameObject, file, banner, .075f);
        }
    }

    static void CaptureAndDestroy(GameObject root, string filename, string label, float bottom)
    {
        var footer = TrucoClubUI.Panel("Offline evidence label", root.transform, TrucoClubUI.Ink,
            new Vector2(.03f, bottom), new Vector2(.97f, bottom + .045f));
        TrucoClubUI.Text(footer.transform, label, 26, TrucoClubUI.Gold, Vector2.zero, Vector2.one,
            true, TextAlignmentOptions.Center);
        TrucoPlayerScreenGallery.Render(root, filename);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static void CaptureTournamentList(Tournament[] tournaments)
    {
        var fixtureAccount = new GameObject("Offline fixture account").AddComponent<SessionUser>();
        fixtureAccount.UpdateUserData(new User { _id = Id(0), username = Names[0] });
        var sessionProperty = typeof(ApiController).GetProperty("GetSessionUser", StaticFields);
        sessionProperty.SetValue(null, fixtureAccount);
        try
        {
            var root = new GameObject("Verified tournament list");
            var safe = TrucoClubUI.Screen(root);
            var holder = TrucoClubUI.Panel("TournamentContent", safe, TrucoClubUI.Ink, Vector2.zero, Vector2.one);
            var scrollRoot = TrucoClubUI.Rect("Scroll View", holder.transform, Vector2.zero, Vector2.one);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            var viewport = TrucoClubUI.Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = TrucoClubUI.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 30, 30);
            layout.spacing = 24;
            layout.childControlHeight = layout.childControlWidth = layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            var overlay = holder.gameObject.AddComponent<TournamentSelectionOverlay>();
            var config = new SerializedObject(overlay);
            config.FindProperty("_contentHolder").objectReferenceValue = holder.gameObject;
            config.FindProperty("_tournamentsCardHolder").objectReferenceValue = content;
            config.FindProperty("_tournamentCardPF").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/__TrucoCard/Prefabs/TournamentCardPF.prefab").GetComponent<TournamentCardItem>();
            config.FindProperty("_headerBlockHeight").floatValue = 360;
            config.ApplyModifiedPropertiesWithoutUndo();
            overlay.DisplayTournamentSelectionUI(tournaments);
            CaptureAndDestroy(root, "four-player-04-visible-and-resumable", "PRUEBA LOCAL · INSCRIPCIÓN Y REINGRESO", .12f);
        }
        finally
        {
            sessionProperty.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(fixtureAccount.gameObject);
        }
    }

    static void SetPresentation(Snapshot snapshot)
    {
        var controller = typeof(MultiplayerController);
        controller.GetProperty("CurrentSnapshot", StaticFields).SetValue(null, snapshot);
        controller.GetProperty("BracketState", StaticFields).SetValue(null,
            snapshot == null ? null : TrucoTournamentService.ToBracket(snapshot));
        controller.GetField("_currentTournamentRequest", StaticFields).SetValue(null,
            snapshot == null ? null : new TournamentRequest(TournamentId, 4, snapshot.entryFee, snapshot.name,
                snapshot.advertisedPrize, snapshot.withFlor, snapshot.targetScore));
    }

    static Snapshot Registration(int count, int revision, string localStatus) => new Snapshot {
        tournamentId = TournamentId, name = "Torneo de prueba local", revision = revision,
        status = "registration", localStatus = localStatus, maxPlayers = 4, withFlor = false, targetScore = 30,
        entryFee = 3, advertisedPrize = 11,
        participants = Names.Take(count).Select((name, index) => new TrucoTournamentService.Participant {
            playerId = Id(index), name = name, position = index, registrationId = "fixture-seat-" + index,
            status = "registered", entryPaid = true
        }).ToList(),
        matches = new List<TrucoTournamentService.Match> {
            new() { round = 0, index = 0, status = "waiting" }, new() { round = 0, index = 1, status = "waiting" },
            new() { round = 1, index = 0, status = "waiting" }
        },
        refunds = new List<TrucoTournamentService.Refund>(),
        wallet = new TrucoTournamentService.Wallet { available = 10, revision = 1 }
    };

    static void SetReady(TrucoTournamentService.Match match, int first, int second)
    {
        match.matchId = $"fixture-{match.round}-{match.index}";
        match.roomName = TournamentBracketState.MatchRoomName(TournamentId, match.round, match.index);
        match.status = "ready";
        match.firstPlayerId = Id(first);
        match.secondPlayerId = Id(second);
    }

    static TrucoTournamentService.Assignment Assign(TrucoTournamentService.Match match) => new() {
        matchId = match.matchId, roomName = match.roomName, round = match.round, index = match.index,
        playerIds = new List<string> { match.firstPlayerId, match.secondPlayerId }
    };

    static Snapshot Read(Snapshot snapshot, int seat, Snapshot previous = null)
    {
        var receipt = new JObject { ["success"] = true, ["data"] = new JObject {
            ["schemaVersion"] = 1, ["snapshot"] = JObject.FromObject(snapshot)
        } };
        return TrucoTournamentService.ReadReceipt(receipt.ToString(), TournamentId, Id(seat), previous);
    }

    static string Id(int seat) => "fixture-player-" + seat;

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Four-player evidence] " + message);
    }

    sealed class FixtureActor : Player
    {
        internal FixtureActor(string userId, int actor, bool local, string readyMatch, bool inactive = false)
            : base(userId, actor, local, new Hashtable { [ActorProperties.UserId] = userId,
                [MultiplayerController.TournamentSeatReadyProperty] = readyMatch })
        {
            IsInactive = inactive;
        }
    }
}
