using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Wires MainMenu: bottom navigation, single active tab, 1v1 room list, avatars. Call <see cref="Initialize"/> from <see cref="MainMenuManager"/> early.</summary>
public static class MainMenuViewCoordinator
{
    static bool _done;
    static bool _navWired;
    static Transform _mainRoot;
    static Action _showTournamentTab;
    static Action _showMenuTab;
    static GameObject _bottomNav;

    public static bool IsNavigationWired => _navWired;

    /// <summary>Call when leaving the main menu (e.g. logout) so the next visit runs <see cref="Initialize"/> again.</summary>
    public static void ResetStateForLeavingMainMenu()
    {
        _done = false;
        _navWired = false;
        _mainRoot = null;
        _showTournamentTab = null;
        _showMenuTab = null;
        _bottomNav = null;
    }

    /// <summary>
    /// The bottom bar may be reparented to the app overlay (DDOL) for sorting; it will survive scene changes unless destroyed.
    /// Call whenever the main menu is not the active experience (login, gameplay, etc.).
    /// </summary>
    public static void DestroyBottomNavIfPresent()
    {
        if (_bottomNav != null)
        {
            if (_bottomNav) UnityEngine.Object.Destroy(_bottomNav);
            _bottomNav = null;
        }
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (t.name != "Bottom Navigation Panel") continue;
            UnityEngine.Object.Destroy(t.gameObject);
        }
        ResetStateForLeavingMainMenu();
    }

    public static void TriggerTournamentFromMain() => _showTournamentTab?.Invoke();

    /// <summary>Back from tournament list overlay: show main menu and nav highlight (same as bottom &quot;Menu&quot; tab).</summary>
    public static void ReturnToMenuFromTournament() => _showMenuTab?.Invoke();

    /// <summary>Call after tournament UI opens so the bottom bar stays above overlay content.</summary>
    public static void EnsureTournamentNavPriority()
    {
        if (_bottomNav != null) EnsureBottomNavOnTop(_bottomNav);
    }

    /// <summary>Must not use the first Canvas in the project — DDOL (e.g. AppManager) often has a Canvas without MAIN.</summary>
    public static void Initialize()
    {
        if (_done) return;
        if (SceneManager.GetActiveScene().name != "MainMenu") return;

        Transform main = FindMainInActiveMenuScene();
        if (main == null) return;
        _mainRoot = main;
        _done = true;

        if (UnityEngine.Object.FindObjectOfType<TrucoAvatarRepository>(true) == null)
        {
            var g = new GameObject("TrucoAvatarRepository");
            g.AddComponent<TrucoAvatarRepository>();
        }

        var mainMenuPanel = FindChildDeep(main, "Main Menu Panel")?.gameObject;
        var profilePanel = FindChildDeep(main, "Profile Panel")?.gameObject;
        var notificationPanel = FindChildDeep(main, "Notification Panel")?.gameObject;
        var roomListPanel = FindChildDeep(main, "Room List Panel")?.gameObject;
        var roomCreatePanel = FindChildDeep(main, "Room Creation Panel")?.gameObject;
        var bottomNav = FindBottomNavigationPanel(main);

        var bottomNavT = bottomNav != null ? bottomNav.transform : null;
        Button navMenu = FindButtonDeep(main, "Nav Button - Menu");
        Button navTournament = FindButtonDeep(main, "Nav Button - Tournament");
        Button navProfile = FindButtonDeep(main, "Nav Button - Profile")
                           ?? FindButtonDeep(main, "Nav Button -  Profile");
        Button navNotification = FindButtonDeep(main, "Nav Button - Notification");
        if (bottomNavT != null)
        {
            var ordered = bottomNavT.GetComponentsInChildren<Button>(true);
            if (navMenu == null && ordered.Length > 0) navMenu = ordered[0];
            if (navTournament == null && ordered.Length > 1) navTournament = ordered[1];
            if (navProfile == null && ordered.Length > 2) navProfile = ordered[2];
            if (navNotification == null && ordered.Length > 3) navNotification = ordered[3];
        }

        Button mesa1vs1 = FindButtonDeep(main, "Mesa 1vs1");

        if (bottomNav != null)
        {
            _bottomNav = bottomNav;
            EnsureBottomNavOnTop(bottomNav);
        }

        void ApplyNav(int index)
        {
            MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, index);
        }

        void ShowOnlyMainBlock(GameObject on)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(on == mainMenuPanel);
            if (profilePanel != null) profilePanel.SetActive(on == profilePanel);
            if (notificationPanel != null) notificationPanel.SetActive(on == notificationPanel);
        }

        void ShowMenuTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(mainMenuPanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(0);
        }

        void ShowProfileTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(profilePanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(2);
        }

        void ShowNotificationTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(notificationPanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(3);
        }

        void ShowTournamentTab()
        {
            Deactivate1v1OverlaysInMain(main);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
            if (notificationPanel != null) notificationPanel.SetActive(false);
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(1);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.DisplayTournamentSelectionUI();
        }

        _showTournamentTab = ShowTournamentTab;
        _showMenuTab = ShowMenuTab;
        WireButton(navMenu, ShowMenuTab);
        WireButton(navTournament, ShowTournamentTab);
        WireButton(navProfile, ShowProfileTab);
        WireButton(navNotification, ShowNotificationTab);
        _navWired = navMenu != null && navTournament != null && navProfile != null && navNotification != null;
        if (!_navWired)
            Debug.LogWarning(
                "MainMenuViewCoordinator: not all 4 bottom nav buttons were found. " +
                "Expected names under 'Bottom Navigation Panel': " +
                "Nav Button - Menu, Nav Button - Tournament, Nav Button - Profile, Nav Button - Notification " +
                "(or four Button components in child order).");

        WireOneVsOneRoomFlow(main, roomListPanel, roomCreatePanel, bottomNav, ShowMenuTab);

        if (mesa1vs1 != null)
        {
            mesa1vs1.onClick.RemoveAllListeners();
            mesa1vs1.onClick.AddListener(() =>
            {
                if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
                if (roomListPanel != null) roomListPanel.SetActive(true);
                if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
                ApplyNav(0);
                var list = UnityEngine.Object.FindObjectOfType<OneVsOneRoomListController>(true);
                list?.Open();
            });
        }

        ShowMenuTab();
        AvatarUiBinder.HookProfileAndMainMenu(main);
    }

    static void WireOneVsOneRoomFlow(Transform main, GameObject roomList, GameObject roomCreate, GameObject bottomNav, Action onExitToMenu)
    {
        if (roomList == null) return;

        var scroll = roomList.GetComponentInChildren<ScrollRect>(true);
        Transform content = scroll != null && scroll.content != null ? scroll.content : null;
        Truco1v1SceneUiWiring.PolishRoomListShell(roomList, scroll);
        if (content != null) Truco1v1SceneUiWiring.EnsureScrollContentLayout(content);

        var listCtrl = roomList.GetComponent<OneVsOneRoomListController>();
        if (listCtrl == null)
            listCtrl = roomList.AddComponent<OneVsOneRoomListController>();

        Truco1v1SceneUiWiring.TryBorrowSpritesFromRoomList(roomList, out var woodPanelSprite, out var joinButtonSprite);
        if (content != null)
        {
            var titleT = FindChildDeep(roomList.transform, "Title")?.GetComponent<TextMeshProUGUI>();
            if (titleT != null && (string.IsNullOrEmpty(titleT.text) || titleT.text == "Mi perfil"))
            {
                titleT.text = TrucoTextosClient.SalasDisponibles;
                titleT.color = new Color(0.98f, 0.97f, 0.95f, 1f);
            }
        }

        if (content != null)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var ch = content.GetChild(i);
                if (ch != null && ch.name == "RoomRow_Template")
                    UnityEngine.Object.Destroy(ch.gameObject);
            }
        }
        OneVsOneRoomRowView rowTemplate = content != null
            ? content.GetComponentInChildren<OneVsOneRoomRowView>(true)
            : null;
        if (rowTemplate == null && content != null)
            rowTemplate = TrucoRuntimeUiBuilders.CreateRoomRowTemplate(
                content, null, null, null);

        var createPanelGo = roomCreate;
        OneVsOneCreateRoomPanel createPanel = null;
        if (createPanelGo != null)
        {
            var rImg = createPanelGo.GetComponent<Image>();
            if (rImg != null)
            {
                rImg.enabled = true;
                rImg.color = TrucoUiTheme.TournamentListScreenBg;
            }
            var formHost = Truco1v1SceneUiWiring.GetOrCreateFormHost(createPanelGo.transform, 300f, 180f, 20f);
            createPanel = TrucoRuntimeUiBuilders.EnsureCreateRoomForm(
                createPanelGo.transform, 1.1f, formHost);
            Truco1v1SceneUiWiring.PolishRoomCreationHeader(createPanelGo.transform, formHost);
        }

        var back = FindButtonDeep(roomList.transform, "Back Button");
        var createBtn = FindButtonDeep(roomList.transform, "CreateRoom Button");
        var refresh = FindButtonDeep(roomList.transform, "Refresh");

        var flow = OneVsOnePhotonFlow.EnsureInstance();
        var mm = UnityEngine.Object.FindObjectOfType<MatchMakingPanel>(true);

        listCtrl.ApplyRuntimeWiring(
            roomList,
            content,
            rowTemplate,
            createPanel,
            refresh,
            createBtn,
            back,
            null, null, null, null,
            mm != null ? mm.gameObject : null,
            flow);
        if (scroll != null) scroll.horizontal = false;

        if (back != null)
        {
            back.onClick.RemoveAllListeners();
            back.onClick.AddListener(() =>
            {
                listCtrl.Close();
                if (roomList != null) roomList.SetActive(false);
                var mainP = FindChildDeep(main, "Main Menu Panel")?.gameObject;
                if (mainP != null) mainP.SetActive(true);
                onExitToMenu?.Invoke();
                if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            });
        }

        if (roomCreate != null)
        {
            var backCreate = FindButtonDeep(roomCreate.transform, "Back Button (1)");
            if (backCreate != null && createPanel != null)
            {
                backCreate.onClick.RemoveAllListeners();
                backCreate.onClick.AddListener(() => { createPanel.TriggerCancelFromChrome(); });
            }
        }
    }

    public     static void EnsureBottomNavOnTop(GameObject bottomNav)
    {
        if (bottomNav == null) return;
        // Tournament / loading / notifications run on a separate root canvas (e.g. Init scene, sorting order 2+).
        // The MainMenu canvas is often order 0, so a nested Canvas here still draws *below* the app overlay.
        var targetParent = FindTopScreenSpaceOverlayRootRect();
        if (targetParent != null && bottomNav.transform.parent != targetParent)
            bottomNav.transform.SetParent(targetParent, true);
        bottomNav.transform.SetAsLastSibling();
        FixBottomNavigationBarLayout(bottomNav);
        var c = bottomNav.GetComponent<Canvas>();
        if (c == null) c = bottomNav.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = 32000;
        if (bottomNav.GetComponent<GraphicRaycaster>() == null)
            bottomNav.AddComponent<GraphicRaycaster>();
    }

    static GameObject FindBottomNavigationPanel(Transform main)
    {
        if (_bottomNav != null) return _bottomNav;
        var t = FindChildDeep(main, "Bottom Navigation Panel");
        if (t != null) return t.gameObject;
        var g = GameObject.Find("Bottom Navigation Panel");
        if (g == null) return null;
        return g.scene == SceneManager.GetActiveScene() ? g : null;
    }

    static RectTransform FindTopScreenSpaceOverlayRootRect()
    {
        Canvas best = null;
        var bestOrder = int.MinValue;
        var all = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c == null) continue;
            if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (IsParentedUnderAnotherCanvas(c.transform)) continue;
            if (c.sortingOrder < bestOrder) continue;
            bestOrder = c.sortingOrder;
            best = c;
        }
        return best != null ? best.transform as RectTransform : null;
    }

    static bool IsParentedUnderAnotherCanvas(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
        {
            if (p.GetComponent<Canvas>() != null) return true;
        }
        return false;
    }

    /// <summary>Recenters the bar (fixes a bad Pos X pushing the strip off-screen) and clears Z.</summary>
    static void FixBottomNavigationBarLayout(GameObject bottomNav)
    {
        var rt = bottomNav.GetComponent<RectTransform>();
        if (rt == null) return;
        var ap = rt.anchoredPosition;
        if (Mathf.Abs(ap.x) > 0.5f) rt.anchoredPosition = new Vector2(0f, ap.y);
        var lp = rt.localPosition;
        if (Mathf.Abs(lp.x) > 0.5f || Mathf.Abs(lp.z) > 0.5f) rt.localPosition = new Vector3(0f, lp.y, 0f);
    }

    /// <summary>Call from <c>Start</c> if the first <see cref="Initialize"/> ran before MAIN was findable, or a nav <see cref="Button"/> was missing.</summary>
    public static void TryCompleteNavigationIfNeeded()
    {
        if (_navWired) return;
        if (SceneManager.GetActiveScene().name != "MainMenu") return;
        if (!_done)
        {
            Initialize();
            return;
        }
        if (_mainRoot == null) return;
        RetryBottomNavWiring();
    }

    static void Deactivate1v1OverlaysInMain(Transform main)
    {
        if (main == null) return;
        var r = FindChildDeep(main, "Room List Panel")?.gameObject;
        var c = FindChildDeep(main, "Room Creation Panel")?.gameObject;
        if (r != null) r.SetActive(false);
        if (c != null) c.SetActive(false);
    }

    static void RetryBottomNavWiring()
    {
        var main = _mainRoot;
        if (main == null) return;

        var mainMenuPanel = FindChildDeep(main, "Main Menu Panel")?.gameObject;
        var profilePanel = FindChildDeep(main, "Profile Panel")?.gameObject;
        var notificationPanel = FindChildDeep(main, "Notification Panel")?.gameObject;
        var roomListPanelR = FindChildDeep(main, "Room List Panel")?.gameObject;
        var bottomNav = FindBottomNavigationPanel(main);
        var bottomNavT = bottomNav != null ? bottomNav.transform : null;

        Button navMenu = FindButtonDeep(main, "Nav Button - Menu");
        Button navTournament = FindButtonDeep(main, "Nav Button - Tournament");
        Button navProfile = FindButtonDeep(main, "Nav Button - Profile")
                            ?? FindButtonDeep(main, "Nav Button -  Profile");
        Button navNotification = FindButtonDeep(main, "Nav Button - Notification");
        if (bottomNavT != null)
        {
            var ordered = bottomNavT.GetComponentsInChildren<Button>(true);
            if (navMenu == null && ordered.Length > 0) navMenu = ordered[0];
            if (navTournament == null && ordered.Length > 1) navTournament = ordered[1];
            if (navProfile == null && ordered.Length > 2) navProfile = ordered[2];
            if (navNotification == null && ordered.Length > 3) navNotification = ordered[3];
        }

        if (bottomNav != null)
        {
            _bottomNav = bottomNav;
            EnsureBottomNavOnTop(bottomNav);
        }

        void ApplyNav(int index)
        {
            MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, index);
        }

        void ShowOnlyMainBlock(GameObject on)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(on == mainMenuPanel);
            if (profilePanel != null) profilePanel.SetActive(on == profilePanel);
            if (notificationPanel != null) notificationPanel.SetActive(on == notificationPanel);
        }

        void ShowMenuTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(mainMenuPanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(0);
        }

        void ShowProfileTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(profilePanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(2);
        }

        void ShowNotificationTab()
        {
            Deactivate1v1OverlaysInMain(main);
            ShowOnlyMainBlock(notificationPanel);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.HideTournamentSelectionUI();
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(3);
        }

        void ShowTournamentTab()
        {
            Deactivate1v1OverlaysInMain(main);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (profilePanel != null) profilePanel.SetActive(false);
            if (notificationPanel != null) notificationPanel.SetActive(false);
            if (bottomNav != null) EnsureBottomNavOnTop(bottomNav);
            ApplyNav(1);
            if (TournamentManager.Instance != null)
                TournamentManager.Instance.DisplayTournamentSelectionUI();
        }

        _showTournamentTab = ShowTournamentTab;
        _showMenuTab = ShowMenuTab;
        WireButton(navMenu, ShowMenuTab);
        WireButton(navTournament, ShowTournamentTab);
        WireButton(navProfile, ShowProfileTab);
        WireButton(navNotification, ShowNotificationTab);
        _navWired = navMenu != null && navTournament != null && navProfile != null && navNotification != null;
        if (_navWired)
        {
            if (roomListPanelR != null && roomListPanelR.activeSelf)
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 0);
            else if (mainMenuPanel != null && mainMenuPanel.activeSelf)
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 0);
            else if (profilePanel != null && profilePanel.activeSelf)
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 2);
            else if (notificationPanel != null && notificationPanel.activeSelf)
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 3);
            else if (mainMenuPanel != null && !mainMenuPanel.activeSelf
                     && (profilePanel == null || !profilePanel.activeSelf)
                     && (notificationPanel == null || !notificationPanel.activeSelf))
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 1);
            else
                MainMenuNavBarVisuals.Apply(navMenu, navTournament, navProfile, navNotification, 0);
        }
    }

    static Transform FindMainInActiveMenuScene()
    {
        var scene = SceneManager.GetActiveScene();
        var gos = scene.GetRootGameObjects();
        for (int i = 0; i < gos.Length; i++)
        {
            var t = gos[i].transform;
            var canvases = t.GetComponentsInChildren<Canvas>(true);
            for (int c = 0; c < canvases.Length; c++)
            {
                if (canvases[c] == null) continue;
                if (canvases[c].gameObject.scene != scene) continue;
                var main = FindChildDeep(canvases[c].transform, "MAIN");
                if (main != null) return main;
            }
        }
        var byName = GameObject.Find("MAIN");
        if (byName != null && byName.scene == scene)
            return byName.transform;
        return null;
    }

    static void WireButton(Button b, Action a)
    {
        if (b == null || a == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => a());
    }

    static Button FindButtonDeep(Transform root, string name)
    {
        var t = FindChildDeep(root, name);
        if (t == null) return null;
        var b = t.GetComponent<Button>();
        if (b != null) return b;
        return t.GetComponentInChildren<Button>(true);
    }

    static Transform FindChildDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindChildDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
