using System.Collections.Generic;
using NUnit.Framework;

/// <summary>EditMode verification for 1v1 create → join → gameplay lobby rules.</summary>
public class TrucoLobbyFlowEditModeTests
{
    const string HostUserId = "user_host_test";
    const string GuestUserId = "user_guest_test";
    const string MatchIdA = "507f1f77bcf86cd799439011";
    const string MatchIdB = "507f1f77bcf86cd799439012";

    [SetUp]
    public void SetUp()
    {
        OneVsOneMatchSession.Clear();
        TrucoActiveHostMatchStore.Clear();
        TrucoRoomPersistence.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        OneVsOneMatchSession.Clear();
        TrucoActiveHostMatchStore.Clear();
        TrucoRoomPersistence.Clear();
    }

    // --- Photon room name (join "Game does not exist" fix) ---

    [Test]
    public void ResolvePhotonRoomName_NeverUsesDisplayName()
    {
        var m = new Player1v1Match
        {
            _id = MatchIdA,
            name = "Sala player 2",
            roomName = "Sala player 2"
        };
        Assert.AreEqual("tr1_" + MatchIdA, m.ResolvePhotonRoomName());
    }

    [Test]
    public void ResolvePhotonRoomName_PrefersApiPhotonRoomName()
    {
        var m = new Player1v1Match
        {
            _id = MatchIdA,
            photonRoomName = "tr1_" + MatchIdA,
            name = "Sala display"
        };
        Assert.AreEqual("tr1_" + MatchIdA, m.ResolvePhotonRoomName());
    }

    [Test]
    public void ResolvePhotonRoomNameOrDefault_FallsBackToMatchId()
    {
        Assert.AreEqual("tr1_" + MatchIdA, ((Player1v1Match)null).ResolvePhotonRoomNameOrDefault(MatchIdA));
    }

    [Test]
    public void JoinParseHintWithoutPhotonName_GetsTr1Default()
    {
        var hint = new Player1v1Match { _id = MatchIdA, name = "Sala X" };
        string json = "{\"ok\":true,\"success\":true}";
        var parsed = ApiController.TryParsePlayerJoinMatchJson(json, hint, out _);
        Assert.NotNull(parsed);
        Assert.AreEqual("tr1_" + MatchIdA, parsed.ResolvePhotonRoomName());
    }

    // --- Pre-game session must survive list refresh (room vanish fix) ---

    [Test]
    public void Reconcile_PreservesSession_WhenPreGameMatchActive_EvenIfListEmpty()
    {
        OneVsOneMatchSession.SetHostContext(MatchIdA, "tr1_" + MatchIdA, 10, true);
        OneVsOneMatchLifecycle.ReconcilePersistedLobbyState(new List<Player1v1Match>());
        Assert.AreEqual(MatchIdA, OneVsOneMatchSession.CurrentMatchId);
        Assert.IsTrue(OneVsOneMatchSession.IsHost);
    }

    [Test]
    public void Reconcile_PreservesGuestSession_WhenJoiningPhoton()
    {
        OneVsOneMatchSession.SetGuestContext(MatchIdA, "tr1_" + MatchIdA, 10, true);
        OneVsOneMatchLifecycle.ReconcilePersistedLobbyState(new List<Player1v1Match>());
        Assert.AreEqual(MatchIdA, OneVsOneMatchSession.CurrentMatchId);
        Assert.IsFalse(OneVsOneMatchSession.IsHost);
    }

    [Test]
    public void ShouldPreserveActivePreGameSession_TrueOnlyBeforeGameStart()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.ShouldPreserveActivePreGameSession(true, false));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldPreserveActivePreGameSession(true, true));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldPreserveActivePreGameSession(false, false));
    }

    // --- Host lifecycle / navigation ---

    [Test]
    public void IsHostWaitingForGuest_OnlyWhenHostWithMatchId()
    {
        Assert.IsFalse(OneVsOneMatchLifecycle.IsHostWaitingForGuest());
        OneVsOneMatchSession.SetHostContext(MatchIdA, "tr1_" + MatchIdA, 5, true);
        Assert.IsTrue(OneVsOneMatchLifecycle.IsHostWaitingForGuest());
        OneVsOneMatchSession.MarkGameStarted();
        Assert.IsFalse(OneVsOneMatchLifecycle.IsHostWaitingForGuest());
    }

    [Test]
    public void ShouldConfirmDeleteRoom_OnlyForHostWaiting()
    {
        Assert.IsFalse(OneVsOneMatchLifecycle.ShouldConfirmDeleteRoomOnLeave());
        OneVsOneMatchSession.SetHostContext(MatchIdA, "tr1_" + MatchIdA, 5, true);
        Assert.IsTrue(OneVsOneMatchLifecycle.ShouldConfirmDeleteRoomOnLeave());
        OneVsOneMatchSession.SetGuestContext(MatchIdA, "tr1_" + MatchIdA, 5, true);
        Assert.IsFalse(OneVsOneMatchLifecycle.ShouldConfirmDeleteRoomOnLeave());
    }

    // --- One room per host (double-create fix) ---

    [Test]
    public void GetExtraHostedMatchIds_CancelsDuplicates_KeepsSessionMatch()
    {
        var list = new List<Player1v1Match>
        {
            HostRoom(MatchIdA),
            HostRoom(MatchIdB)
        };
        var extras = OneVsOneLobbyFlowRules.GetExtraHostedMatchIds(list, MatchIdA, HostUserId);
        Assert.AreEqual(1, extras.Count);
        Assert.AreEqual(MatchIdB, extras[0]);
    }

    [Test]
    public void GetExtraHostedMatchIds_NoExtrasWhenSingleRoom()
    {
        var list = new List<Player1v1Match> { HostRoom(MatchIdA) };
        var extras = OneVsOneLobbyFlowRules.GetExtraHostedMatchIds(list, null, HostUserId);
        Assert.AreEqual(0, extras.Count);
    }

    // --- Photon join retry while host creates room ---

    [Test]
    public void ShouldRetryPhotonJoin_WhenGameDoesNotExist()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.ShouldRetryPhotonJoin(32758, "Game does not exist", 0, 20));
        Assert.IsTrue(OneVsOneLobbyFlowRules.ShouldRetryPhotonJoin(0, "Game does not exist", 0, 20));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldRetryPhotonJoin(32758, "Game does not exist", 20, 20));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldRetryPhotonJoin(0, "Room full", 0, 20));
    }

    // --- Gameplay launch gate ---

    [Test]
    public void IsReadyToLaunchGameplay_RequiresTwoPlayers()
    {
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsReadyToLaunchGameplay(1, 2));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsReadyToLaunchGameplay(2, 2));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsReadyToLaunchGameplay(3, 2));
    }

    // --- List visibility ---

    [Test]
    public void ShouldShowInLobbyList_HidesFullRoom()
    {
        var m = HostRoom(MatchIdA);
        m.players = new[]
        {
            new User { _id = HostUserId },
            new User { _id = GuestUserId }
        };
        m.currentPlayers = 2;
        Assert.IsFalse(m.ShouldShowInLobbyList());
    }

    [Test]
    public void IsWrongPasswordApiError_DetectsPasswordFailures()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Invalid password"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Contraseña incorrecta"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Wrong room code"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Match is full"));
        // Bare "invalid" must NOT map to password — create-room false positive.
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Invalid request"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Incorrect region"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsWrongPasswordApiError("Token invalid"));
    }

    [Test]
    public void ForCreateRoom_NeverShowsWrongPassword()
    {
        Assert.AreNotEqual(TrucoTextosClient.ContrasenaIncorrecta,
            TrucoUserFacingErrors.ForCreateRoom("Invalid request"));
        Assert.AreNotEqual(TrucoTextosClient.ContrasenaIncorrecta,
            TrucoUserFacingErrors.ForCreateRoom("Incorrect password"));
        Assert.AreEqual(TrucoTextosClient.ErrorCrearSala,
            TrucoUserFacingErrors.ForCreateRoom("Invalid request"));
        Assert.AreEqual(TrucoTextosClient.YaTienesSala,
            TrucoUserFacingErrors.ForCreateRoom("Player already has an active room"));
        Assert.AreEqual(TrucoTextosClient.SalaYaExiste,
            TrucoUserFacingErrors.ForCreateRoom("Room already exists"));
    }

    [Test]
    public void IsRoomAlreadyExistsApiError_DistinctFromActiveRoom()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError("Room already exists"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError("duplicate room"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError("Sala ya existe"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsRoomAlreadyExistsApiError("Player already has an active room"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsAlreadyHasRoomApiError("Player already has an active room"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsAlreadyHasRoomApiError("Room already exists"));
    }

    [Test]
    public void ForCreateRoom_MapsConnectionAndSessionErrors()
    {
        Assert.AreEqual(TrucoTextosClient.ErrorConexion,
            TrucoUserFacingErrors.ForCreateRoom("Network timeout"));
        Assert.AreEqual(TrucoTextosClient.SesionExpirada,
            TrucoUserFacingErrors.ForCreateRoom("Session expired"));
        Assert.AreEqual(TrucoTextosClient.SaldoInsuficiente,
            TrucoUserFacingErrors.ForCreateRoom("Insufficient balance"));
    }

    [Test]
    public void ForJoinRoom_ShowsWrongPasswordOnlyForPasswordErrors()
    {
        Assert.AreEqual(TrucoTextosClient.ContrasenaIncorrecta,
            TrucoUserFacingErrors.ForJoinRoom("Invalid password"));
        Assert.AreNotEqual(TrucoTextosClient.ContrasenaIncorrecta,
            TrucoUserFacingErrors.ForJoinRoom("Invalid request"));
        Assert.AreEqual(TrucoTextosClient.ErrorConexion,
            TrucoUserFacingErrors.ForJoinRoom("connection failed"));
        Assert.AreEqual(TrucoTextosClient.SesionExpirada,
            TrucoUserFacingErrors.ForJoinRoom("token expired"));
    }

    [Test]
    public void ShouldShowResumeInLobbyList_GuestParticipantNotInPhoton()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.ShouldShowResumeInLobbyList(true, true, false, false));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldShowResumeInLobbyList(true, true, false, true));
        Assert.IsFalse(OneVsOneLobbyFlowRules.ShouldShowResumeInLobbyList(true, false, false, false));
    }

    [Test]
    public void ResolvePhotonRoomName_IgnoresRoomNameField()
    {
        var m = new Player1v1Match
        {
            _id = MatchIdA,
            roomName = "wrong_display_name",
            name = "Sala player 2"
        };
        Assert.AreEqual("tr1_" + MatchIdA, m.ResolvePhotonRoomName());
        Assert.AreNotEqual(m.roomName, m.ResolvePhotonRoomName());
    }

    [Test]
    public void IsMatchFullApiError_DetectsEnglishAndSpanish()
    {
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsMatchFullApiError("Match is full"));
        Assert.IsTrue(OneVsOneLobbyFlowRules.IsMatchFullApiError("Sala llena"));
        Assert.IsFalse(OneVsOneLobbyFlowRules.IsMatchFullApiError("Network error"));
    }

    [Test]
    public void TryParseSingleMatchJson_ParsesSuccessMatchWrapper()
    {
        const string json = "{\"success\":true,\"match\":{\"_id\":\"abc123\",\"name\":\"Sala test\",\"status\":\"active\",\"players\":[]}}";
        var m = ApiController.TryParseSingleMatchJson(json);
        Assert.IsNotNull(m);
        Assert.AreEqual("abc123", m._id);
        Assert.AreEqual("Sala test", m.name);
    }

    [Test]
    public void GetAllHostedMatchIds_ReturnsEveryHostedRow()
    {
        var list = new List<Player1v1Match> { HostRoom(MatchIdA), HostRoom(MatchIdB) };
        var ids = OneVsOneLobbyFlowRules.GetAllHostedMatchIds(list, HostUserId);
        Assert.AreEqual(2, ids.Count);
        CollectionAssert.Contains(ids, MatchIdA);
        CollectionAssert.Contains(ids, MatchIdB);
    }

    [Test]
    public void MatchBelongsToUser_IncludesCreatorWithoutPlayersArray()
    {
        var ghost = new Player1v1Match { _id = MatchIdA, status = "active", createdBy = HostUserId, players = null };
        Assert.IsTrue(OneVsOneLobbyFlowRules.MatchBelongsToUser(ghost, HostUserId));
        Assert.IsFalse(OneVsOneLobbyFlowRules.MatchBelongsToUser(ghost, GuestUserId));
    }

    static Player1v1Match HostRoom(string id) =>
        new Player1v1Match
        {
            _id = id,
            status = "active",
            createdBy = HostUserId,
            players = new[] { new User { _id = HostUserId } }
        };
}
