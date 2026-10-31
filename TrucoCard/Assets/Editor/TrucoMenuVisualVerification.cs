using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders the actual menu scene, keeping its original home artwork and header.</summary>
public static class TrucoMenuVisualVerification
{
    public static void Capture()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch QA project.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var root = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
        var main = root.transform.Find("MAIN");
        var home = main.Find("Main Menu Panel");
        var nav = main.Find("Bottom Navigation Panel");
        foreach (Transform child in main) child.gameObject.SetActive(child == home || child == nav);
        var buttons = nav.GetComponentsInChildren<Button>(true);
        var menu = buttons.Single(b => b.name == "Nav Button - Menu");
        var tournament = buttons.Single(b => b.name == "Nav Button - Tournament");
        var profile = buttons.Single(b => b.name.Contains("Profile"));
        var notice = buttons.Single(b => b.name == "Nav Button - Notification");
        var layout = nav.gameObject.AddComponent<TrucoBottomNavLayout>();
        root.GetComponent<CanvasScaler>().enabled = false;
        var cameraObject = new GameObject("MenuCaptureCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(248, 246, 239, 255);
        root.renderMode = RenderMode.ScreenSpaceCamera;
        root.worldCamera = camera; root.planeDistance = 1;
        Directory.CreateDirectory("Artifacts/UI/MenuRepair");
        CaptureSize(480, 800, "home-480x800");
        CaptureSize(1080, 1920, "home-1080x1920");
        CaptureSize(720, 1600, "home-tall");
        Debug.Log("[TRUCO MENU QA] Restored home and all selected-tab states rendered.");
        EditorApplication.Exit(0);

        void CaptureSize(int width, int height, string name)
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            root.scaleFactor = Mathf.Sqrt(width / 1080f * height / 1920f);
            Canvas.ForceUpdateCanvases();
            layout.Refresh();
            for (int selected = 0; selected < 4; selected++)
            {
                MainMenuNavBarVisuals.Apply(menu, tournament, profile, notice, selected);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
                foreach (var text in root.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) text.ForceMeshUpdate();
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes($"Artifacts/UI/MenuRepair/{name}-tab{selected}.png", image.EncodeToPNG());
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
            }
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
