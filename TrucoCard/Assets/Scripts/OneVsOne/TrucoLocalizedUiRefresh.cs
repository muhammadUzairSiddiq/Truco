using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Refreshes open UI when <see cref="TrucoLocalization.OnLanguageChanged"/> fires.</summary>
public static class TrucoLocalizedUiRefresh
{
    public static void ApplyAll()
    {
        RefreshRoomListChrome();
        RefreshCreatePanels();
        foreach (var row in Object.FindObjectsByType<OneVsOneRoomRowView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            row.RefreshFromLivePhoton();
        foreach (var lo in Object.FindObjectsByType<LoadingOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            lo.ReapplyTheme();
        foreach (var no in Object.FindObjectsByType<NotificationOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            no.ReapplyTheme();
    }

    static void RefreshRoomListChrome()
    {
        var list = Object.FindObjectOfType<OneVsOneRoomListController>(true);
        if (list == null) return;
        var root = list.gameObject;
        SetTmpByName(root.transform, "Title", TrucoTextosClient.SalasDisponibles);
        SetButtonLabel(root.transform, "CreateRoom Button", TrucoTextosClient.CrearSala);
        SetButtonLabel(root.transform, "Refresh", TrucoTextosClient.ActualizarLista);
        SetHeaderByName(root.transform, "Top Panel", TrucoTextosClient.Partida1v1);
        var createRoot = FindDeep(root.transform.parent, "Room Creation Panel");
        if (createRoot != null)
            SetHeaderByName(createRoot, "Top Panel (1)", TrucoTextosClient.CrearSala);
    }

    static void RefreshCreatePanels()
    {
        foreach (var panel in Object.FindObjectsByType<OneVsOneCreateRoomPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            panel.RefreshLocalizedLabels();
    }

    static void SetHeaderByName(Transform root, string panelName, string text)
    {
        var top = FindDeep(root, panelName);
        if (top == null) return;
        var title = FindDeep(top, "Title") ?? FindDeep(top, "Text");
        var tmp = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        if (tmp != null) tmp.text = text;
    }

    static void SetTmpByName(Transform root, string name, string text)
    {
        var t = FindDeep(root, name);
        var tmp = t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        if (tmp != null) tmp.text = text;
    }

    static void SetButtonLabel(Transform root, string btnName, string text)
    {
        var b = FindDeep(root, btnName);
        if (b == null) return;
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = text;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var f = FindDeep(t.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}
