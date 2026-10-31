using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated static layout captures; no login, wallet submission or Photon connection.</summary>
public static class TrucoVisualVerification
{
    public static void Capture()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("Run capture in an isolated batch QA project.");
        foreach (string path in new[] { "Assets/Resources/TrucoUI/Club/MenuBackground.png", "Assets/Resources/TrucoUI/Club/TournamentHero.png" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.maxTextureSize = 2048; importer.SaveAndReimport();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("TournamentPreview"); var safe = TrucoClubUI.Screen(root);
        TrucoClubUI.Text(safe, "TORNEOS", 48, TrucoClubUI.Gold, new Vector2(.06f, .89f), new Vector2(.94f, .97f), true);
        var hero = TrucoClubUI.Panel("Hero", safe, Color.white, new Vector2(.06f, .64f), new Vector2(.94f, .88f)); hero.sprite = TrucoClubUI.Hero;
        TrucoClubUI.Text(hero.transform, "TU CAMINO\nA LA FINAL", 40, TrucoClubUI.Cream, new Vector2(.03f, .1f), new Vector2(.58f, .94f), true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/__TrucoCard/Prefabs/TournamentCardPF.prefab");
        for (int i = 0; i < 2; i++)
        {
            var card = Object.Instantiate(prefab, safe); var rt = (RectTransform)card.transform;
            rt.anchorMin = new Vector2(.06f, .32f - i * .28f); rt.anchorMax = new Vector2(.94f, .61f - i * .28f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            card.GetComponent<TournamentCardItem>().Init(new Tournament { _id = "preview" + i, name = i == 0 ? "Copa de la tarde" : "La gran mesa", maxPlayers = i == 0 ? 4 : 8, targetScore = 30, withFlor = i == 0, entryFee = 50, prizePool = 180, status = "registration" });
        }
        CaptureCanvas(root, "tournaments", 1080, 1920);
        Object.DestroyImmediate(root);
        Debug.Log("[TRUCO VISUAL QA] Captures written to Artifacts/UI");
        EditorApplication.Exit(0);
    }
    static void CaptureCanvas(GameObject root, string name, int width, int height)
    {
        var camGo = new GameObject("CaptureCamera"); var camera = camGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = TrucoClubUI.Ink;
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
        foreach (var text in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) text.ForceMeshUpdate();
        camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
        Directory.CreateDirectory("Artifacts/UI"); File.WriteAllBytes("Artifacts/UI/" + name + ".png", texture.EncodeToPNG());
        RenderTexture.active = old; Object.DestroyImmediate(texture); camera.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
    }
}
