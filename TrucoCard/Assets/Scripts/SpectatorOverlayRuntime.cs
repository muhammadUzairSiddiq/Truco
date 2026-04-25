using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SpectatorOverlayRuntime
{
    public const string AdminSceneName = "AdminScene";

    public static void Create()
    {
        if (GameObject.Find("SpectatorOverlayRoot") != null) return;
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null) return;
        var root = new GameObject("SpectatorOverlayRoot");
        root.transform.SetParent(canvas.transform, false);
        var rt = root.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = root.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.4f);
        img.raycastTarget = true;

        var title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(root.transform, false);
        var tit = title.AddComponent<Text>();
        tit.text = TrucoTextosClient.EspectandoAdmin;
        tit.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tit.fontSize = 22;
        tit.alignment = TextAnchor.UpperCenter;
        tit.color = Color.white;
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0, -20);
        trt.sizeDelta = new Vector2(500, 60);

        var leaveGo = new GameObject("Leave", typeof(RectTransform), typeof(Image), typeof(Button));
        leaveGo.transform.SetParent(root.transform, false);
        var lrt = leaveGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0.5f);
        lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta = new Vector2(220, 40);
        var ltGo = new GameObject("Text");
        ltGo.transform.SetParent(leaveGo.transform, false);
        var lt = ltGo.AddComponent<Text>();
        var ltr = ltGo.GetComponent<RectTransform>();
        ltr.anchorMin = Vector2.zero; ltr.anchorMax = Vector2.one; ltr.offsetMin = Vector2.zero; ltr.offsetMax = Vector2.zero;
        lt.text = TrucoTextosClient.VolverMenuAdmin;
        lt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        lt.alignment = TextAnchor.MiddleCenter;
        lt.color = Color.black;
        var btn = leaveGo.GetComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            SpectatorContext.Clear();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            SceneManager.LoadScene(AdminSceneName);
        });
    }
}
