using UnityEngine;

/// <summary>Local avatar index (0–11), persisted. Used for UI + Photon custom property "avatarIndex".</summary>
public static class PlayerAvatarData
{
    public const int Count = 12;
    const string PrefsKey = "truco_avatar_index_v1";

    public static int SelectedIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, Count - 1);
        set
        {
            PlayerPrefs.SetInt(PrefsKey, Mathf.Clamp(value, 0, Count - 1));
            PlayerPrefs.Save();
        }
    }
}
