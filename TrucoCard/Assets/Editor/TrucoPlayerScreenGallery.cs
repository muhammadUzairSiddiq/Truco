using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Actual Unity UI captures with explicit Editor fixtures, no authentication or transaction requests.</summary>
public static class TrucoPlayerScreenGallery
{
    const string Output = "Artifacts/UI/PlayerGallery";
    public static void CaptureAll()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run gallery capture in an isolated QA project.");
        Directory.CreateDirectory(Output);
        CaptureMenu();
        Empty(); CaptureTournaments();
        Empty();
        TrucoTournamentPracticeUI.OpenList(); Render(TrucoTournamentPracticeUI.Root, "07-practice-tournaments");
        TrucoTournamentPracticeUI.Details(4); Render(TrucoTournamentPracticeUI.Root, "08-tournament-details");
        TrucoTournamentPracticeUI.PrepareGalleryTable();
        TrucoTournamentPracticeUI.Waiting(); Render(TrucoTournamentPracticeUI.Root, "09-tournament-waiting");
        TrucoTournamentPracticeUI.Bracket(); Render(TrucoTournamentPracticeUI.Root, "10-bracket-four");
        TrucoTournamentPracticeUI.OpenTable(); Render(TrucoTournamentPracticeUI.Root, "11-practice-table");
        TrucoTournamentPracticeUI.PrepareGalleryResult(); Render(TrucoTournamentPracticeUI.Root, "12-match-result");
        TrucoTournamentPracticeUI.PrepareGalleryChampion(); Render(TrucoTournamentPracticeUI.Root, "13-champion");
        TrucoTournamentPracticeUI.PrepareGalleryTable(8); TrucoTournamentPracticeUI.Bracket(); Render(TrucoTournamentPracticeUI.Root, "14-bracket-eight");
        TrucoTournamentPracticeUI.Close(); Empty(); CaptureWallet();
        Empty(); CaptureAuth();
        Empty(); CaptureLiveTournamentStates();
        Debug.Log("[TRUCO PLAYER GALLERY] All player screens rendered to " + Output);
        EditorApplication.Exit(0);
    }
    static void Empty() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    static void CaptureMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
        var main = canvas.transform.Find("MAIN"); var nav = main.Find("Bottom Navigation Panel");
        var buttons = nav.GetComponentsInChildren<Button>(true);
        var ordered = new[] { buttons.Single(b => b.name.EndsWith("Menu")), buttons.Single(b => b.name.EndsWith("Tournament")), buttons.Single(b => b.name.Contains("Profile")), buttons.Single(b => b.name.EndsWith("Notification")) };
        var layout = nav.gameObject.AddComponent<TrucoBottomNavLayout>(); layout.Refresh();
        void Tab(string name, int index)
        {
            foreach (Transform child in main) child.gameObject.SetActive(child == nav || child.name == name);
            MainMenuNavBarVisuals.Apply(ordered[0], ordered[1], ordered[2], ordered[3], index);
        }
        Tab("Main Menu Panel", 0); Render(canvas.gameObject, "01-home");
        TrucoProfilePresentation.Ensure(main); TrucoProfilePrefsBarUi.EnsureOnProfilePanel(main);
        Tab("Profile Panel", 2); Render(canvas.gameObject, "02-profile");
        var notifications = main.Find("Notification Panel"); TrucoNotificationPanelUi.Ensure(notifications);
        Tab("Notification Panel", 3);
        TrucoNotificationPanelUi.EditorPreview(new TrucoNotificationLog.Entry[0]); Render(canvas.gameObject, "03-notifications-empty");
        TrucoNotificationPanelUi.EditorPreview(new[] {
            new TrucoNotificationLog.Entry { time = "14:30", message = "Tu próxima mesa está lista para jugar.", kind = TrucoNotificationLog.Kind.Info },
            new TrucoNotificationLog.Entry { time = "14:22", message = "El servidor confirmó el cierre de tu partida.", kind = TrucoNotificationLog.Kind.Success },
            new TrucoNotificationLog.Entry { time = "14:10", message = "Solicitud de compra pendiente de revisión.", kind = TrucoNotificationLog.Kind.Pending }
        }); Render(canvas.gameObject, "04-notifications-feed");
        var rooms = main.Find("Room List Panel"); var scroll = rooms.GetComponentInChildren<ScrollRect>(true);
        Truco1v1SceneUiWiring.PolishRoomListShell(rooms.gameObject, scroll);
        Truco1v1SceneUiWiring.EnsureScrollContentLayout(scroll.content);
        foreach (Transform child in scroll.content.Cast<Transform>().ToArray()) TrucoMenuTheme.DestroyUI(child.gameObject);
        for (int i = 0; i < 2; i++)
        {
            var row = TrucoRuntimeUiBuilders.CreateRoomRowTemplate(scroll.content);
            row.gameObject.SetActive(true); row.Bind(new Player1v1Match { _id = "editor-room-" + i, name = i == 0 ? "Mesa entre amigos" : "La revancha", entryFee = 50, prize = 90, isPublic = i == 0, type = i == 0 ? "public" : "private", withFlor = i == 0, targetScore = 30, playerCount = 1, maxPlayers = 2, players = new[] { new User { _id = "editor-host", username = "Jugador" } } }, (m, r) => { }, false);
        }
        Truco1v1SceneUiWiring.EnsureRoomListRefreshButton(rooms);
        Tab("Room List Panel", 0); Render(canvas.gameObject, "05-rooms");
        var create = main.Find("Room Creation Panel"); TrucoMenuTheme.Background(create);
        var host = Truco1v1SceneUiWiring.GetOrCreateFormHost(create, 300, 220, 24);
        TrucoRuntimeUiBuilders.EnsureCreateRoomForm(create, 1.1f, host); Truco1v1SceneUiWiring.PolishRoomCreationHeader(create, host);
        Tab("Room Creation Panel", 0); Render(canvas.gameObject, "06-create-room");
        TrucoProfilePrefsBarUi.ResetForLeavingMainMenu();
    }
    static void CaptureTournaments()
    {
        var root = new GameObject("TournamentGallery"); var safe = TrucoClubUI.Screen(root);
        var holder = TrucoClubUI.Panel("TournamentContent", safe, TrucoClubUI.Ink, Vector2.zero, Vector2.one);
        var scrollRoot = TrucoClubUI.Rect("Scroll View", holder.transform, Vector2.zero, Vector2.one);
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one); viewport.gameObject.AddComponent<RectMask2D>();
        var content = TrucoClubUI.Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1)); content.pivot = new Vector2(.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(32, 32, 30, 30); layout.spacing = 24; layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
        var overlay = holder.gameObject.AddComponent<TournamentSelectionOverlay>();
        var serialized = new SerializedObject(overlay);
        serialized.FindProperty("_contentHolder").objectReferenceValue = holder.gameObject;
        serialized.FindProperty("_tournamentsCardHolder").objectReferenceValue = content;
        serialized.FindProperty("_tournamentCardPF").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/__TrucoCard/Prefabs/TournamentCardPF.prefab").GetComponent<TournamentCardItem>();
        serialized.FindProperty("_headerBlockHeight").floatValue = 360; serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.DisplayTournamentSelectionUI(new[] {
            new Tournament { _id = "editor-cup-4", name = "Copa de la tarde", status = "registration", maxPlayers = 4, withFlor = true, targetScore = 15, entryFee = 50, prizePool = 180 },
            new Tournament { _id = "editor-cup-8", name = "La gran mesa", status = "registration", maxPlayers = 8, withFlor = false, targetScore = 30, entryFee = 100, prizePool = 720 }
        });
        Render(root, "07-live-tournament-list"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void CaptureWallet()
    {
        var buy = new TrucoWalletService.Request { id = "CP-EJEMPLO", type = "buy", amount = 100, status = "verification", bank = "Banco configurado", alias = "Alias del administrador", holder = "Titular configurado", whatsapp = "Contacto del administrador", createdAt = "31/10 · 14:10", history = new List<TrucoWalletService.Stage> { new TrucoWalletService.Stage { status = "requested", at = "14:10" }, new TrucoWalletService.Stage { status = "receipt_sent", at = "14:15" }, new TrucoWalletService.Stage { status = "verification", at = "14:22" } } };
        var sell = new TrucoWalletService.Request { id = "VT-EJEMPLO", type = "sell", amount = 50, status = "approved", bank = "Banco del usuario", alias = "Cuenta del usuario", holder = "Titular de la cuenta", createdAt = "31/10 · 13:10", history = new List<TrucoWalletService.Stage> { new TrucoWalletService.Stage { status = "requested", at = "13:10" }, new TrucoWalletService.Stage { status = "approved", at = "13:45" } } };
        var fixture = new TrucoWalletService.Snapshot { available = 297, held = 50, configuration = new TrucoWalletService.Configuration { minimumBuy = 5, minimumSell = 50, maximumAmount = 5000, buyPresets = new[] { 50, 60, 75, 100, 150, 200 }, sellPresets = new[] { 50, 100, 150, 200 }, identificationRequired = true }, requests = new List<TrucoWalletService.Request> { buy, sell } };
        Render(TrucoWalletScreen.EditorPreview(0, fixture), "15-wallet");
        Render(TrucoWalletScreen.EditorPreview(1, fixture), "16-buy");
        Render(TrucoWalletScreen.EditorPreview(3, fixture, buy), "17-purchase-status");
        Render(TrucoWalletScreen.EditorPreview(2, fixture), "18-sell");
        Render(TrucoWalletScreen.EditorPreview(3, fixture, sell), "19-sale-status");
        var root = TrucoWalletScreen.EditorPreview(4, fixture); Render(root, "20-wallet-history"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void CaptureLiveTournamentStates()
    {
        // These use production presentation with explicit local fixtures. Do not
        // register a player, request money movement, or modify live bracket state.
        var phases = new[] {
            TrucoTournamentPhase.Filling, TrucoTournamentPhase.CreatingMatches,
            TrucoTournamentPhase.LoadingTable, TrucoTournamentPhase.WaitingNextRound,
            TrucoTournamentPhase.Failed, TrucoTournamentPhase.Cancelled
        };
        string[] files = {
            "25-live-registration-waiting", "26-live-preparing", "27-live-loading",
            "28-live-next-round", "29-live-failure", "30-live-cancelled"
        };
        for (int i = 0; i < phases.Length; i++)
        {
            string detail = phases[i] == TrucoTournamentPhase.Failed
                ? "No se pudo confirmar la mesa. Volvé a intentarlo."
                : phases[i] == TrucoTournamentPhase.Cancelled
                    ? "El servidor debe confirmar el estado de tu inscripción y cualquier reembolso."
                    : null;
            var status = TrucoTournamentStatusScreen.EditorPreview(phases[i], detail);
            Render(status, files[i]); UnityEngine.Object.DestroyImmediate(status);
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var championRoot = new GameObject("TournamentChampionGalleryFixture");
        var champion = championRoot.AddComponent<TournamentChampionOverlay>();
        typeof(TournamentChampionOverlay).GetMethod("Build", flags).Invoke(champion, null);
        typeof(TournamentChampionOverlay).GetMethod("Display", flags)
            .Invoke(champion, new object[] { 720, "Copa de ocho · Vista Editor", false });
        Render(championRoot, "31-live-champion"); UnityEngine.Object.DestroyImmediate(championRoot);

        var bracketRoot = new GameObject("TournamentBracketGalleryFixture");
        var overlay = bracketRoot.AddComponent<TournamentBracketOverlay>();
        typeof(TournamentBracketOverlay).GetMethod("Build", flags).Invoke(overlay, null);
        var rounds = (Transform)typeof(TournamentBracketOverlay).GetField("_roundsHost", flags).GetValue(overlay);
        var header = (TextMeshProUGUI)typeof(TournamentBracketOverlay).GetField("_header", flags).GetValue(overlay);
        var statusLabel = (TextMeshProUGUI)typeof(TournamentBracketOverlay).GetField("_status", flags).GetValue(overlay);
        header.text = "TORNEO DE 8 JUGADORES";
        statusLabel.text = "VISTA EDITOR · Cuadro de ejemplo · Con Flor · 15 puntos";
        var bracket = new TournamentBracketState(8);
        string[] names = { "Vos", "Mateo", "Lucía", "Diego", "Sofía", "Nico", "Valen", "Tomás" };
        var participants = names.Select((name, index) => new User {
            _id = "gallery-player-" + index, username = name
        }).ToList();
        bracket.Seed(participants);
        bracket.RecordWinner(0, 0, participants[0]._id, participants[0].username);
        bracket.RecordWinner(0, 1, participants[2]._id, participants[2].username);
        bracketRoot.SetActive(true); Canvas.ForceUpdateCanvases();
        TournamentBracketGraphic.Build((RectTransform)rounds, bracket, participants[0]._id);
        Render(bracketRoot, "32-live-bracket-eight"); UnityEngine.Object.DestroyImmediate(bracketRoot);
    }
    static void CaptureAuth()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/LoginScreen.unity", OpenSceneMode.Single);
        var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
        TrucoLoginScreenUiPolish.Apply();
        var all = canvas.GetComponentsInChildren<Transform>(true);
        string[] names = { "Player Login Panel", "Player Register Panel", "OtpVerificationPanel", "ForgotPasswordPanel" };
        string[] files = { "21-login", "22-register", "23-verify-code", "24-forgot-password" };
        for (int i = 0; i < names.Length; i++)
        {
            foreach (var item in all) if (names.Contains(item.name)) item.gameObject.SetActive(item.name == names[i]);
            Render(canvas.gameObject, files[i]);
        }
    }
    public static void Render(GameObject root, string name)
    {
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        var scaler = root.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
        var cameraObject = new GameObject("GalleryCamera"); var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = TrucoMenuTheme.Paper;
        var target = new RenderTexture(480, 800, 24); camera.targetTexture = target;
        canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.scaleFactor = Mathf.Sqrt(480f / 1080 * 800f / 1920);
        Canvas.ForceUpdateCanvases();
        foreach (var layout in root.GetComponentsInChildren<TrucoBottomNavLayout>()) layout.Refresh();
        for (int i = 0; i < 3; i++) { Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform); }
        foreach (var fit in root.GetComponentsInChildren<TournamentBracketViewportFit>(true)) fit.Refresh();
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(480, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 480, 800), 0, 0); image.Apply();
        File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}
