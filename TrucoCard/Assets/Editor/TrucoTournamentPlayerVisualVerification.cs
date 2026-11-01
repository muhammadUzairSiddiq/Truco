using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders production screens without AI, server fixtures, registration or HTTP.</summary>
public static class TrucoTournamentPlayerVisualVerification
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated QA project.");
        Directory.CreateDirectory("Artifacts/UI/PlayerGallery");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("PlayerTournamentVisibility");
        var safe = TrucoClubUI.Screen(root);
        var holder = TrucoClubUI.Panel("TournamentContent", safe, TrucoClubUI.Ink, Vector2.zero, Vector2.one);
        var scrollRoot = TrucoClubUI.Rect("Scroll View", holder.transform, Vector2.zero, Vector2.one);
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        var viewport = TrucoClubUI.Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = TrucoClubUI.Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
        content.pivot = new Vector2(.5f, 1);
        content.gameObject.AddComponent<VerticalLayoutGroup>();
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false;
        var overlay = holder.gameObject.AddComponent<TournamentSelectionOverlay>();
        var serialized = new SerializedObject(overlay);
        serialized.FindProperty("_contentHolder").objectReferenceValue = holder.gameObject;
        serialized.FindProperty("_tournamentsCardHolder").objectReferenceValue = content;
        serialized.FindProperty("_tournamentCardPF").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/__TrucoCard/Prefabs/TournamentCardPF.prefab").GetComponent<TournamentCardItem>();
        serialized.FindProperty("_headerBlockHeight").floatValue = 360;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        overlay.DisplayLoading(); TrucoPlayerScreenGallery.Render(root, "player-tournaments-loading");
        overlay.DisplayUnavailable(); TrucoPlayerScreenGallery.Render(root, "player-tournaments-unavailable");
        overlay.DisplayTournamentSelectionUI(Array.Empty<Tournament>());
        TrucoPlayerScreenGallery.Render(root, "player-tournaments-empty");
        foreach (int count in new[] { 4, 8 })
        {
            TournamentBracketOverlay.ShowFormat(count);
            var bracket = UnityEngine.Object.FindFirstObjectByType<TournamentBracketOverlay>();
            TrucoPlayerScreenGallery.Render(bracket.gameObject, "player-bracket-format-" + count);
            UnityEngine.Object.DestroyImmediate(bracket.gameObject);
        }
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("[PLAYER TOURNAMENT VISIBILITY] Captured production loading/error/empty/4/8 format screens. No AI or network calls.");
        EditorApplication.Exit(0);
    }
}
