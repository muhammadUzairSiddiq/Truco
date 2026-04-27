using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hook avatar grid + top-bar portrait Images on main menu / profile.</summary>
public static class AvatarUiBinder
{
    public static void HookProfileAndMainMenu(Transform main)
    {
        if (main == null) return;
        var repo = TrucoAvatarRepository.Instance ?? Object.FindObjectOfType<TrucoAvatarRepository>(true);
        if (repo != null) repo.EnsureCache();

        var selPanel = FindDeep(main, "Avatar Selection Panel");
        if (selPanel != null)
        {
            var buttons = new List<Button>();
            foreach (var b in selPanel.GetComponentsInChildren<Button>(true))
            {
                if (b != null && b.name.StartsWith("Avatar Button"))
                    buttons.Add(b);
            }
            buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            for (int i = 0; i < buttons.Count && i < PlayerAvatarData.Count; i++)
            {
                int idx = i;
                var btn = buttons[i];
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectAvatar(idx, main));
            }
        }

        ApplyAvatarToUi(main, PlayerAvatarData.SelectedIndex);
    }

    static void SelectAvatar(int index, Transform main)
    {
        PlayerAvatarData.SelectedIndex = index;
        ApplyAvatarToUi(main, index);
        TrucoPunPlayerAvatarUtil.ApplyLocalPlayerAvatar();
    }

    static void ApplyAvatarToUi(Transform main, int index)
    {
        var repo = TrucoAvatarRepository.Instance;
        if (repo == null) repo = Object.FindObjectOfType<TrucoAvatarRepository>(true);
        if (repo != null) repo.EnsureCache();
        var sp = repo != null ? repo.GetPortrait(index) : null;
        foreach (var img in main.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            if (img.name != "Avatar Icon") continue;
            if (sp != null) img.sprite = sp;
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
