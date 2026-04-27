using TMPro;
using UnityEngine;

/// <summary>Rellena los TMP "username" en Main Menu / Profile con <see cref="ApiController.GetSessionUser"/>.</summary>
public static class UsernameMainMenuBinder
{
    public static void ApplyToScene()
    {
        var u = ApiController.GetSessionUser?.Data;
        string name = u != null && !string.IsNullOrWhiteSpace(u.username) ? u.username.Trim() : null;
        if (string.IsNullOrEmpty(name) && u != null && !string.IsNullOrWhiteSpace(u.email))
            name = u.email.Split('@')[0].Trim();
        if (string.IsNullOrEmpty(name)) name = "—";

        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (t.gameObject.name != "username" && t.transform.name != "username") continue;
            t.text = name;
        }
    }
}
