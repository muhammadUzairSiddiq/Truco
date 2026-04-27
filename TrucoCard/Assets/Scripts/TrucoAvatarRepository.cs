using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolves 12 portrait sprites: optional array, or scans Avatar Selection Panel, or one sliced sheet.</summary>
public class TrucoAvatarRepository : MonoBehaviour
{
    public static TrucoAvatarRepository Instance { get; private set; }

    [Tooltip("If set and length >= 12, these are used in order. Else auto-discover at runtime.")]
    [SerializeField] private Sprite[] _portraits = new Sprite[0];

    [Header("Auto-discover (Main Menu)")]
    [SerializeField] private string _avatarPanelPath = "Profile Panel/Avatar Selection Panel";

    private Sprite[] _cached;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
    }

    public Sprite GetPortrait(int index)
    {
        index = Mathf.Clamp(index, 0, PlayerAvatarData.Count - 1);
        EnsureCache();
        if (_cached == null || index >= _cached.Length) return null;
        return _cached[index];
    }

    public void EnsureCache()
    {
        if (_cached != null && _cached.Length >= PlayerAvatarData.Count) return;
        if (_portraits != null && _portraits.Length >= PlayerAvatarData.Count)
        {
            _cached = _portraits;
            return;
        }
        if (TryDiscoverFromUi(out var list))
        {
            _cached = list;
            return;
        }
        _cached = null;
    }

    bool TryDiscoverFromUi(out Sprite[] list)
    {
        list = null;
        Transform root = null;
        var allT = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allT.Length; i++)
        {
            var t = allT[i];
            if (t != null && t.name == "Avatar Selection Panel" && t.gameObject.scene.IsValid())
            {
                root = t;
                break;
            }
        }
        if (root == null && !string.IsNullOrEmpty(_avatarPanelPath))
        {
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!sc.isLoaded) continue;
                var gos = sc.GetRootGameObjects();
                for (int g = 0; g < gos.Length; g++)
                {
                    var found = gos[g].transform.Find(_avatarPanelPath);
                    if (found != null) { root = found; break; }
                }
                if (root != null) break;
            }
        }
        if (root == null) return false;
        var buttons = new List<Button>();
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            if (b == null) continue;
            if (!b.name.StartsWith("Avatar Button", StringComparison.Ordinal)) continue;
            var img = b.GetComponent<Image>() ?? b.GetComponentInChildren<Image>(true);
            if (img == null || img.sprite == null) continue;
            buttons.Add(b);
        }
        if (buttons.Count < PlayerAvatarData.Count) return false;
        buttons.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
        var sp = new Sprite[PlayerAvatarData.Count];
        for (int i = 0; i < PlayerAvatarData.Count; i++)
        {
            var im = buttons[i].GetComponent<Image>() ?? buttons[i].GetComponentInChildren<Image>(true);
            sp[i] = im != null ? im.sprite : null;
        }
        if (sp[0] == null) return false;
        list = sp;
        return true;
    }

    static Transform FindDeepChild(Transform t, Func<Transform, bool> pred)
    {
        if (t == null) return null;
        if (pred(t)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeepChild(t.GetChild(i), pred);
            if (r != null) return r;
        }
        return null;
    }
}
