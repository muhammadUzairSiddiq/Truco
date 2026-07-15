using UnityEngine;

/// <summary>Legacy entry point — language + region UI lives on the profile avatar screen.</summary>
public static class TrucoLanguageToggleUi
{
    public static void ResetForLeavingMainMenu() =>
        TrucoProfilePrefsBarUi.ResetForLeavingMainMenu();

    public static void EnsureOnMainMenu(Transform mainRoot) =>
        TrucoProfilePrefsBarUi.EnsureOnProfilePanel(mainRoot);

    public static void EnsureOnProfilePanel(Transform mainRoot) =>
        TrucoProfilePrefsBarUi.EnsureOnProfilePanel(mainRoot);
}
