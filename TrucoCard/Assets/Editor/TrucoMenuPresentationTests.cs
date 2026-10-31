using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TrucoMenuPresentationTests
{
    Scene _scene;
    Transform _main;
    [SetUp]
    public void OpenOriginalMenu()
    {
        _scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Additive);
        _main = _scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
            .First(c => c.isRootCanvas).transform.Find("MAIN");
    }
    [TearDown] public void CloseMenu() => EditorSceneManager.CloseScene(_scene, true);

    [Test]
    public void OriginalHomeHeaderAndActionsRemainVisible()
    {
        var home = _main.Find("Main Menu Panel");
        foreach (string child in new[] { "Top Panel (1)", "logo", "Mesa 1vs1", "TournamentsBtn" })
            Assert.IsTrue(home.Find(child).gameObject.activeSelf, child);
        Assert.IsNotNull(home.Find("Top Panel (1)/Avatar/Avatar Icon").GetComponent<Image>().sprite);
        Assert.IsNotNull(home.Find("Top Panel (1)/username"));
        Assert.IsNotNull(home.Find("Top Panel (1)/Coins Value"));
        Assert.IsNull(home.Find("ClubHome"));
    }

    [Test]
    public void AllOriginalIconNamesFitAndRefreshDoesNotDuplicateCaptions()
    {
        var nav = _main.Find("Bottom Navigation Panel");
        var layout = nav.gameObject.AddComponent<TrucoBottomNavLayout>();
        layout.Refresh(); layout.Refresh();
        var buttons = nav.GetComponentsInChildren<Button>(true);
        Assert.AreEqual(4, buttons.Length);
        foreach (var button in buttons)
        {
            var icon = button.GetComponentsInChildren<Image>(true).Single(i => i.name.StartsWith("Icon"));
            Assert.IsTrue(icon.preserveAspect, button.name);
            Assert.LessOrEqual(icon.rectTransform.sizeDelta.y, 90, button.name);
            Assert.IsNotNull(icon.sprite, button.name);
            Assert.AreEqual(1, button.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Count(t => t.name == "NavigationCaption"));
            Assert.IsFalse(icon.raycastTarget);
        }
        Assert.IsNull(nav.GetComponent<Image>().sprite, "Navigation uses a solid brown background.");
        Assert.AreEqual(TrucoMenuTheme.Brown, nav.GetComponent<Image>().color);
    }

    [Test]
    public void SelectionUpdatesEveryOriginalIconAndKeepsActions()
    {
        var nav = _main.Find("Bottom Navigation Panel");
        nav.gameObject.AddComponent<TrucoBottomNavLayout>().Refresh();
        var buttons = nav.GetComponentsInChildren<Button>(true);
        var ordered = new[] { buttons.Single(b => b.name.EndsWith("Menu")), buttons.Single(b => b.name.EndsWith("Tournament")),
            buttons.Single(b => b.name.Contains("Profile")), buttons.Single(b => b.name.EndsWith("Notification")) };
        int clicks = 0;
        foreach (var button in ordered) button.onClick.AddListener(() => clicks++);
        for (int selected = 0; selected < 4; selected++)
        {
            MainMenuNavBarVisuals.Apply(ordered[0], ordered[1], ordered[2], ordered[3], selected);
            for (int i = 0; i < ordered.Length; i++)
            {
                var icon = ordered[i].GetComponentsInChildren<Image>().Single(g => g.name.StartsWith("Icon"));
                Assert.AreEqual(i == selected ? Color.white : TrucoUiTheme.NavIconOffTab, icon.color);
                Assert.That(icon.transform.localScale.x, Is.EqualTo(i == selected ? 1.12f : .9f).Within(.001));
            }
        }
        foreach (var button in ordered) button.onClick.Invoke();
        Assert.AreEqual(4, clicks, "Styling must retain the original action listeners.");
    }

    [Test]
    public void ProfilePreservesAvatarActionsWithoutOversizedPortraits()
    {
        TrucoProfilePresentation.Ensure(_main);
        var profile = _main.Find("Profile Panel");
        var avatars = profile.Find("Avatar Selection Panel").GetComponentsInChildren<Button>(true);
        Assert.AreEqual(12, avatars.Length);
        foreach (var avatar in avatars) Assert.AreEqual(Vector3.one, avatar.transform.localScale);
        Assert.IsNotNull(profile.Find("Top Panel/username"));
        Assert.IsNotNull(profile.Find("logout Button").GetComponent<Button>());
    }
}
